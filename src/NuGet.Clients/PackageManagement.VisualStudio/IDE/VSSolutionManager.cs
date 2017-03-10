// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Configuration;
using NuGet.PackageManagement.Telemetry;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISolutionManager))]
    [Export(typeof(IVsSolutionManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VSSolutionManager : IVsSolutionManager, IVsSelectionEvents
    {
        private static readonly INuGetProjectContext EmptyNuGetProjectContext = new EmptyNuGetProjectContext();

        private SolutionEvents _solutionEvents;
        private CommandEvents _solutionSaveEvent;
        private CommandEvents _solutionSaveAsEvent;
        private IVsMonitorSelection _vsMonitorSelection;
        private uint _solutionLoadedUICookie;
        private IVsSolution _vsSolution;
       
        private readonly IServiceProvider _serviceProvider;
        private readonly IProjectSystemCache _projectSystemCache;
        private readonly NuGetProjectFactory _projectSystemFactory;
        private readonly ICredentialServiceProvider _credentialServiceProvider;
        private readonly Common.ILogger _logger;

        private bool _initialized;
        private bool _cacheInitialized;

        //add solutionOpenedRasied to make sure ProjectRename and ProjectAdded event happen after solutionOpened event
        private bool _solutionOpenedRaised;

        private string _solutionDirectoryBeforeSaveSolution;

        public INuGetProjectContext NuGetProjectContext { get; set; }

        public NuGetProject DefaultNuGetProject
        {
            get
            {
                EnsureInitialize();

                if (string.IsNullOrEmpty(DefaultNuGetProjectName))
                {
                    return null;
                }

                NuGetProject defaultNuGetProject;
                _projectSystemCache.TryGetNuGetProject(DefaultNuGetProjectName, out defaultNuGetProject);
                return defaultNuGetProject;
            }
        }

        public string DefaultNuGetProjectName { get; set; }

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRemoved;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectRenamed;

        public event EventHandler<NuGetProjectEventArgs> NuGetProjectUpdated;

        public event EventHandler<NuGetProjectEventArgs> AfterNuGetProjectRenamed;

        public event EventHandler<NuGetEventArgs<string>> AfterNuGetCacheUpdated;

        public event EventHandler SolutionClosed;

        public event EventHandler SolutionClosing;

        public event EventHandler SolutionOpened;

        public event EventHandler SolutionOpening;

        public event EventHandler<ActionsExecutedEventArgs> ActionsExecuted;

        [ImportingConstructor]
        internal VSSolutionManager(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IProjectSystemCache projectSystemCache,
            NuGetProjectFactory projectSystemFactory,
            ICredentialServiceProvider credentialServiceProvider,
            [Import(typeof(VisualStudioActivityLogger))]
            Common.ILogger logger)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (projectSystemCache == null)
            {
                throw new ArgumentNullException(nameof(projectSystemCache));
            }

            if (projectSystemFactory == null)
            {
                throw new ArgumentNullException(nameof(projectSystemFactory));
            }
            if (credentialServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(credentialServiceProvider));
            }
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _serviceProvider = serviceProvider;
            _projectSystemCache = projectSystemCache;
            _projectSystemFactory = projectSystemFactory;
            _credentialServiceProvider = credentialServiceProvider;
            _logger = logger;
        }

        private async Task InitializeAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                HttpHandlerResourceV3.CredentialService = _credentialServiceProvider.GetCredentialService();
                _vsSolution = _serviceProvider.GetService<SVsSolution, IVsSolution>();
                _vsMonitorSelection = _serviceProvider.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();

                var solutionLoadedGuid = VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_guid;
                _vsMonitorSelection.GetCmdUIContextCookie(ref solutionLoadedGuid, out _solutionLoadedUICookie);

                uint cookie;
                var hr = _vsMonitorSelection.AdviseSelectionEvents(this, out cookie);
                ErrorHandler.ThrowOnFailure(hr);
                var dte = _serviceProvider.GetDTE();
                // Keep a reference to SolutionEvents so that it doesn't get GC'ed. Otherwise, we won't receive events.
                _solutionEvents = dte.Events.SolutionEvents;
                _solutionEvents.BeforeClosing += OnBeforeClosing;
                _solutionEvents.AfterClosing += OnAfterClosing;
                _solutionEvents.ProjectAdded += OnEnvDTEProjectAdded;
                _solutionEvents.ProjectRemoved += OnEnvDTEProjectRemoved;
                _solutionEvents.ProjectRenamed += OnEnvDTEProjectRenamed;

                var vSStd97CmdIDGUID = VSConstants.GUID_VSStandardCommandSet97.ToString("B");
                var solutionSaveID = (int)VSConstants.VSStd97CmdID.SaveSolution;
                var solutionSaveAsID = (int)VSConstants.VSStd97CmdID.SaveSolutionAs;

                _solutionSaveEvent = dte.Events.CommandEvents[vSStd97CmdIDGUID, solutionSaveID];
                _solutionSaveAsEvent = dte.Events.CommandEvents[vSStd97CmdIDGUID, solutionSaveAsID];

                _solutionSaveEvent.BeforeExecute += SolutionSaveAs_BeforeExecute;
                _solutionSaveEvent.AfterExecute += SolutionSaveAs_AfterExecute;
                _solutionSaveAsEvent.BeforeExecute += SolutionSaveAs_BeforeExecute;
                _solutionSaveAsEvent.AfterExecute += SolutionSaveAs_AfterExecute;

                _projectSystemCache.CacheUpdated += NuGetCacheUpdate_After;
            });
        }

        public async Task<NuGetProject> UpdateNuGetProjectToPackageRef(NuGetProject oldProject)
        {
#if VS14
            // do nothing for VS 2015 and simply return the existing NuGetProject
            if (NuGetProjectUpdated != null)
            {
                NuGetProjectUpdated(this, new NuGetProjectEventArgs(oldProject));
            }

            return await Task.FromResult(oldProject);
#else
            if (oldProject == null)
            {
                throw new ArgumentException(
                    ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty,
                    nameof(oldProject));
            }

            var projectName = GetNuGetProjectSafeName(oldProject);
            var dteProject = GetDTEProject(projectName);

            ProjectNames oldEnvDTEProjectName;
            _projectSystemCache.TryGetProjectNames(projectName, out oldEnvDTEProjectName);

            RemoveEnvDTEProjectFromCache(projectName);

            var nuGetProject = await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var settings = ServiceLocator.GetInstance<ISettings>();

                var context = new ProjectSystemProviderContext(
                    EmptyNuGetProjectContext,
                    () => PackagesFolderPathUtility.GetPackagesFolderPath(this, settings));

                return new LegacyCSProjPackageReferenceProject(
                    new EnvDTEProjectAdapter(dteProject),
                    VsHierarchyUtility.GetProjectId(dteProject));
            });

            var added = _projectSystemCache.AddProject(oldEnvDTEProjectName, dteProject, nuGetProject);

            if (NuGetProjectUpdated != null)
            {
                NuGetProjectUpdated(this, new NuGetProjectEventArgs(nuGetProject));
            }

            return nuGetProject;
#endif
        }

        public NuGetProject GetNuGetProject(string nuGetProjectSafeName)
        {
            if (string.IsNullOrEmpty(nuGetProjectSafeName))
            {
                throw new ArgumentException(
                    ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty,
                    nameof(nuGetProjectSafeName));
            }

            EnsureInitialize();

            NuGetProject nuGetProject = null;
            // Project system cache could be null when solution is not open.
            if (_projectSystemCache != null)
            {
                _projectSystemCache.TryGetNuGetProject(nuGetProjectSafeName, out nuGetProject);
            }
            return nuGetProject;
        }

        // Return short name if it's non-ambiguous.
        // Return CustomUniqueName for projects that have ambigous names (such as same project name under different solution folder)
        // Example: return Folder1/ProjectA if there are both ProjectA under Folder1 and Folder2
        public string GetNuGetProjectSafeName(NuGetProject nuGetProject)
        {
            if (nuGetProject == null)
            {
                throw new ArgumentNullException("nuGetProject");
            }

            EnsureInitialize();

            // Try searching for simple names first
            string name = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            if (GetNuGetProject(name) == nuGetProject)
            {
                return name;
            }

            return NuGetProject.GetUniqueNameOrName(nuGetProject);
        }

        public EnvDTE.Project GetDTEProject(string nuGetProjectSafeName)
        {
            if (string.IsNullOrEmpty(nuGetProjectSafeName))
            {
                throw new ArgumentException(ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty, "nuGetProjectSafeName");
            }

            EnsureInitialize();

            EnvDTE.Project dteProject;
            _projectSystemCache.TryGetDTEProject(nuGetProjectSafeName, out dteProject);
            return dteProject;
        }

        public IEnumerable<NuGetProject> GetNuGetProjects()
        {
            EnsureInitialize();

            var projects = _projectSystemCache.GetNuGetProjects();

            return projects;
        }

        public void SaveProject(NuGetProject nuGetProject)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var safeName = GetNuGetProjectSafeName(nuGetProject);
                EnvDTEProjectUtility.Save(GetDTEProject(safeName));
            });
        }

        private IEnumerable<EnvDTE.Project> GetEnvDTEProjects()
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            EnsureInitialize();

            var dteProjects = _projectSystemCache.GetEnvDTEProjects();

            return dteProjects;
        }

        /// <summary>
        /// IsSolutionOpen is true, if the dte solution is open
        /// and is saved as required
        /// </summary>
        public bool IsSolutionOpen
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var dte = _serviceProvider.GetDTE();
                    return dte != null &&
                           dte.Solution != null &&
                           dte.Solution.IsOpen;
                });
            }
        }

        public bool IsSolutionAvailable
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (!IsSolutionOpen)
                    {
                        // Solution is not open. Return false.
                        return false;
                    }

                    EnsureInitialize();

                    if (!DoesSolutionRequireAnInitialSaveAs())
                    {
                        // Solution is open and 'Save As' is not required. Return true.
                        return true;
                    }
                    
                    var projects = _projectSystemCache.GetNuGetProjects();
                    if (!projects.Any() || projects.Any(project => !(project is INuGetIntegratedProject)))
                    {
                        // Solution is open, but not saved. That is, 'Save as' is required.
                        // And, there are no projects or there is a packages.config based project. Return false.
                        return false;
                    }

                    // Solution is open and not saved. And, only contains project.json based projects.
                    // Check if globalPackagesFolder is a full path. If so, solution is available.

                    var settings = ServiceLocator.GetInstance<Configuration.ISettings>();
                    var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

                    return Path.IsPathRooted(globalPackagesFolder);
                });
            }
        }

        public async Task<IEnumerable<string>> GetDeferredProjectsFilePathAsync()
        {
#if VS14
            // Not applicable for Dev14 so always return empty list.
            return await Task.FromResult(Enumerable.Empty<string>());
#else
            return await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var projectPaths = new List<string>();
                IEnumHierarchies enumHierarchies;
                var guid = Guid.Empty;
                var hr = _vsSolution.GetProjectEnum((uint)__VSENUMPROJFLAGS3.EPF_DEFERRED, ref guid, out enumHierarchies);

                ErrorHandler.ThrowOnFailure(hr);

                // Loop all projects found
                if (enumHierarchies != null)
                {
                    // Loop projects found
                    var hierarchy = new IVsHierarchy[1];
                    uint fetched = 0;
                    while (enumHierarchies.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1)
                    {
                        string projectPath;
                        hierarchy[0].GetCanonicalName(VSConstants.VSITEMID_ROOT, out projectPath);

                        if (!string.IsNullOrEmpty(projectPath))
                        {
                            projectPaths.Add(projectPath);
                        }
                    }
                }

                return projectPaths;
            });
#endif
        }

        public async Task<bool> SolutionHasDeferredProjectsAsync()
        {
#if VS14
            // for Dev14 always return false since DPL not exists there.
            return await Task.FromResult(false);
#else
            return await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // check if solution is DPL enabled or not. 
                if (!IsSolutionDPLEnabled)
                {
                    return false;
                }

                // Get deferred projects count of current solution
                var value = GetVSSolutionProperty((int)(__VSPROPID7.VSPROPID_DeferredProjectCount));
                return (int)value != 0;
            });
#endif
        }

        public bool IsSolutionDPLEnabled
        {
            get
            {
#if VS14
                // for Dev14 always return false since DPL not exists there.
                return false;
#else
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    EnsureInitialize();
                    var vsSolution7 = _vsSolution as IVsSolution7;

                    if (vsSolution7 != null && vsSolution7.IsSolutionLoadDeferred())
                    {
                        return true;
                    }

                    return false;
                });
#endif
            }
        }

        public bool IsSolutionFullyLoaded
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    EnsureInitialize();
                    var value = GetVSSolutionProperty((int)(__VSPROPID4.VSPROPID_IsSolutionFullyLoaded));
                    return (bool)value;
                });
            }
        }

        public void EnsureSolutionIsLoaded()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                EnsureInitialize();
                var vsSolution4 = _vsSolution as IVsSolution4;

                if (vsSolution4 != null)
                {
                    // ignore result and continue. Since results may be incomplete if user canceled.
                    vsSolution4.EnsureSolutionIsLoaded((uint)__VSBSLFLAGS.VSBSLFLAGS_None);
                }
            });
        }

        public string SolutionDirectory
        {
            get
            {
                if (!IsSolutionOpen)
                {
                    return null;
                }

                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    string solutionFilePath = await GetSolutionFilePathAsync();

                    if (String.IsNullOrEmpty(solutionFilePath))
                    {
                        return null;
                    }
                    return Path.GetDirectoryName(solutionFilePath);
                });
            }
        }

        private async Task<string> GetSolutionFilePathAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Use .Properties.Item("Path") instead of .FullName because .FullName might not be
            // available if the solution is just being created
            string solutionFilePath = null;

            var dte = _serviceProvider.GetDTE();
            var property = dte.Solution.Properties.Item("Path");
            if (property == null)
            {
                return null;
            }
            try
            {
                // When using a temporary solution, (such as by saying File -> New File), querying this value throws.
                // Since we wouldn't be able to do manage any packages at this point, we return null. Consumers of this property typically
                // use a String.IsNullOrEmpty check either way, so it's alright.
                solutionFilePath = (string)property.Value;
            }
            catch (COMException)
            {
                return null;
            }

            return solutionFilePath;
        }

        /// <summary>
        /// Checks whether the current solution is saved to disk, as opposed to be in memory.
        /// </summary>
        private bool DoesSolutionRequireAnInitialSaveAs()
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            // Check if user is doing File - New File without saving the solution.
            var value = GetVSSolutionProperty((int)(__VSPROPID.VSPROPID_IsSolutionSaveAsRequired));
            if ((bool)value)
            {
                return true;
            }

            // Check if user unchecks the "Tools - Options - Project & Soltuions - Save new projects when created" option
            value = GetVSSolutionProperty((int)(__VSPROPID2.VSPROPID_DeferredSaveSolution));
            return (bool)value;
        }

        private object GetVSSolutionProperty(int propId)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            object value;
            int hr = _vsSolution.GetProperty(propId, out value);

            ErrorHandler.ThrowOnFailure(hr);

            return value;
        }

        private void OnSolutionExistsAndFullyLoaded()
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (SolutionOpening != null)
            {
                SolutionOpening(this, EventArgs.Empty);
            }

            // although the SolutionOpened event fires, the solution may be only in memory (e.g. when
            // doing File - New File). In that case, we don't want to act on the event.
            if (!IsSolutionOpen)
            {
                return;
            }

            EnsureNuGetAndEnvDTEProjectCache();

            if (SolutionOpened != null)
            {
                SolutionOpened(this, EventArgs.Empty);
            }

            _solutionOpenedRaised = true;
        }

        private void OnAfterClosing()
        {
            if (SolutionClosed != null)
            {
                SolutionClosed(this, EventArgs.Empty);
            }
        }

        private void OnBeforeClosing()
        {
            DefaultNuGetProjectName = null;
            _projectSystemCache.Clear();
            _cacheInitialized = false;

            SolutionClosing?.Invoke(this, EventArgs.Empty);

            _solutionOpenedRaised = false;
        }

        private void SolutionSaveAs_BeforeExecute(
            string Guid,
            int ID,
            object CustomIn,
            object CustomOut,
            ref bool CancelDefault)
        {
            _solutionDirectoryBeforeSaveSolution = SolutionDirectory;
        }

        private void SolutionSaveAs_AfterExecute(string Guid, int ID, object CustomIn, object CustomOut)
        {
            // If SolutionDirectory before solution save was null
            // Or, if SolutionDirectory before solution save is different from the current one
            // Reset cache among other things
            if (string.IsNullOrEmpty(_solutionDirectoryBeforeSaveSolution)
                || !string.Equals(
                    _solutionDirectoryBeforeSaveSolution,
                    SolutionDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Call OnBeforeClosing() to reset the project cache among other things
                // After that, call OnSolutionExistsAndFullyLoaded() to load cache, raise events and more

                OnBeforeClosing();

                NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    OnSolutionExistsAndFullyLoaded();
                });
            }
        }

        private void OnEnvDTEProjectRenamed(EnvDTE.Project envDTEProject, string oldName)
        {
            // This is a solution event. Should be on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!String.IsNullOrEmpty(oldName) && IsSolutionOpen && _solutionOpenedRaised)
            {
                EnsureNuGetAndEnvDTEProjectCache();

                if (EnvDTEProjectUtility.IsSupported(envDTEProject))
                {
                    RemoveEnvDTEProjectFromCache(oldName);
                    AddEnvDTEProjectToCache(envDTEProject);
                    NuGetProject nuGetProject;
                    _projectSystemCache.TryGetNuGetProject(envDTEProject.Name, out nuGetProject);

                    if (NuGetProjectRenamed != null)
                    {
                        NuGetProjectRenamed(this, new NuGetProjectEventArgs(nuGetProject));
                    }

                    // VSSolutionManager susbscribes to this Event, in order to update the caption on the DocWindow Tab.
                    // This needs to fire after NugetProjectRenamed so that PackageManagerModel has been updated with
                    // the right project context.
                    AfterNuGetProjectRenamed?.Invoke(this, new NuGetProjectEventArgs(nuGetProject));

                }
                else if (EnvDTEProjectUtility.IsSolutionFolder(envDTEProject))
                {
                    // In the case where a solution directory was changed, project FullNames are unchanged.
                    // We only need to invalidate the projects under the current tree so as to sync the CustomUniqueNames.
                    foreach (var item in EnvDTEProjectUtility.GetSupportedChildProjects(envDTEProject))
                    {
                        RemoveEnvDTEProjectFromCache(item.FullName);
                        AddEnvDTEProjectToCache(item);
                    }
                }
            }
        }

        private void OnEnvDTEProjectRemoved(EnvDTE.Project envDTEProject)
        {
            // This is a solution event. Should be on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            RemoveEnvDTEProjectFromCache(envDTEProject.FullName);
            NuGetProject nuGetProject;
            _projectSystemCache.TryGetNuGetProject(envDTEProject.Name, out nuGetProject);

            if (NuGetProjectRemoved != null)
            {
                NuGetProjectRemoved(this, new NuGetProjectEventArgs(nuGetProject));
            }
        }

        private void OnEnvDTEProjectAdded(EnvDTE.Project envDTEProject)
        {
            // This is a solution event. Should be on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsSolutionOpen
                && EnvDTEProjectUtility.IsSupported(envDTEProject)
                && !EnvDTEProjectUtility.IsParentProjectExplicitlyUnsupported(envDTEProject)
                && _solutionOpenedRaised)
            {
                EnsureNuGetAndEnvDTEProjectCache();
                AddEnvDTEProjectToCache(envDTEProject);
                NuGetProject nuGetProject;
                _projectSystemCache.TryGetNuGetProject(envDTEProject.Name, out nuGetProject);

                if (NuGetProjectAdded != null)
                {
                    NuGetProjectAdded(this, new NuGetProjectEventArgs(nuGetProject));
                }
            }
        }

        private void SetDefaultProjectName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // when a new solution opens, we set its startup project as the default project in NuGet Console
            var dte = _serviceProvider.GetDTE();
            var solutionBuild = (SolutionBuild2)dte.Solution.SolutionBuild;
            if (solutionBuild.StartupProjects != null)
            {
                IEnumerable<object> startupProjects = (IEnumerable<object>)solutionBuild.StartupProjects;
                string startupProjectName = startupProjects.Cast<string>().FirstOrDefault();
                if (!String.IsNullOrEmpty(startupProjectName))
                {
                    ProjectNames envDTEProjectName;
                    if (_projectSystemCache.TryGetProjectNames(startupProjectName, out envDTEProjectName))
                    {
                        DefaultNuGetProjectName = _projectSystemCache.IsAmbiguous(envDTEProjectName.ShortName) ?
                            envDTEProjectName.CustomUniqueName :
                            envDTEProjectName.ShortName;
                    }
                }
            }
        }

        private void EnsureNuGetAndEnvDTEProjectCache()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_cacheInitialized && IsSolutionOpen)
            {
                try
                {
                    var dte = _serviceProvider.GetDTE();
                    var supportedProjects = EnvDTESolutionUtility.GetAllEnvDTEProjects(dte)
                        .Where(project => EnvDTEProjectUtility.IsSupported(project));

                    foreach (var project in supportedProjects)
                    {
                        try
                        {
                            AddEnvDTEProjectToCache(project);
                        }
                        catch (Exception e)
                        {
                            // Ignore failed projects.
                            _logger.LogWarning($"The project {project.Name} failed to initialize as a NuGet project.");
                            _logger.LogError(e.ToString());
                        }

                        // Consider that the cache is initialized only when there are any projects to add.
                        _cacheInitialized = true;
                    }

                    SetDefaultProjectName();
                }
                catch
                {
                    _projectSystemCache.Clear();
                    _cacheInitialized = false;
                    DefaultNuGetProjectName = null;

                    throw;
                }
            }
        }

        private void AddEnvDTEProjectToCache(EnvDTE.Project envDTEProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!EnvDTEProjectUtility.IsSupported(envDTEProject))
            {
                return;
            }

            ProjectNames oldEnvDTEProjectName;
            _projectSystemCache.TryGetProjectNameByShortName(EnvDTEProjectUtility.GetName(envDTEProject), out oldEnvDTEProjectName);

            // Create the NuGet project first. If this throws we bail out and do not change the cache.
            var nuGetProject = CreateNuGetProject(envDTEProject);

            // Then create the project name from the project.
            var newEnvDTEProjectName = ProjectNames.FromDTEProject(envDTEProject);

            // Finally, try to add the project to the cache.
            var added = _projectSystemCache.AddProject(newEnvDTEProjectName, envDTEProject, nuGetProject);

            if (added)
            {
                // Emit project specific telemetry as we are adding the project to the cache.
                // This ensures we do not emit the events over and over while the solution is
                // open.
                NuGetProjectTelemetryService.Instance.EmitNuGetProject(nuGetProject);
            }

            if (string.IsNullOrEmpty(DefaultNuGetProjectName) ||
                newEnvDTEProjectName.ShortName.Equals(DefaultNuGetProjectName, StringComparison.OrdinalIgnoreCase))
            {
                DefaultNuGetProjectName = oldEnvDTEProjectName != null ?
                    oldEnvDTEProjectName.CustomUniqueName :
                    newEnvDTEProjectName.ShortName;
            }
        }

        private void RemoveEnvDTEProjectFromCache(string name)
        {
            // Do nothing if the cache hasn't been set up
            if (_projectSystemCache == null)
            {
                return;
            }

            ProjectNames envDTEProjectName;
            _projectSystemCache.TryGetProjectNames(name, out envDTEProjectName);

            // Remove the project from the cache
            _projectSystemCache.RemoveProject(name);

            if (!_projectSystemCache.ContainsKey(DefaultNuGetProjectName))
            {
                DefaultNuGetProjectName = null;
            }

            // for LightSwitch project, the main project is not added to _projectCache, but it is called on removal.
            // in that case, projectName is null.
            if (envDTEProjectName != null
                && envDTEProjectName.CustomUniqueName.Equals(DefaultNuGetProjectName, StringComparison.OrdinalIgnoreCase)
                && !_projectSystemCache.IsAmbiguous(envDTEProjectName.ShortName))
            {
                DefaultNuGetProjectName = envDTEProjectName.ShortName;
            }
        }

        private void EnsureInitialize()
        {
            try
            {
                // If already initialized, need not be on the UI thread
                if (!_initialized)
                {
                    _initialized = true;

                    NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        await InitializeAsync();

                        var dte = _serviceProvider.GetDTE();
                        if (dte.Solution.IsOpen)
                        {
                            OnSolutionExistsAndFullyLoaded();
                        }
                    });
                }
                else
                {
                    // Check if the cache is initialized.
                    // It is possible that the cache is not initialized, since,
                    // the solution was not saved and/or there were no projects in the solution
                    if (!_cacheInitialized && _solutionOpenedRaised)
                    {
                        NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                        {
                            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            EnsureNuGetAndEnvDTEProjectCache();
                        });
                    }
                }
            }
            catch (Exception e)
            {
                // ignore errors
                Debug.Fail(e.ToString());
                _logger.LogError(e.ToString());
            }
        }

        private NuGetProject CreateNuGetProject(Project envDTEProject, INuGetProjectContext projectContext = null)
        {
            var settings = ServiceLocator.GetInstance<ISettings>();

            var context = new ProjectSystemProviderContext(
                projectContext ?? EmptyNuGetProjectContext,
                () => PackagesFolderPathUtility.GetPackagesFolderPath(this, settings));

            NuGetProject result;
            if (_projectSystemFactory.TryCreateNuGetProject(envDTEProject, context, out result))
            {
                return result;
            }

            return null;
        }

        // REVIEW: This might be inefficient, see what we can do with caching projects until references change
        internal static IEnumerable<EnvDTE.Project> GetDependentEnvDTEProjects(IDictionary<string, List<EnvDTE.Project>> dependentEnvDTEProjectsDictionary, EnvDTE.Project envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            if (envDTEProject == null)
            {
                throw new ArgumentNullException(nameof(envDTEProject));
            }

            List<Project> dependents;
            if (dependentEnvDTEProjectsDictionary.TryGetValue(EnvDTEProjectUtility.GetUniqueName(envDTEProject), out dependents))
            {
                return dependents;
            }

            return Enumerable.Empty<EnvDTE.Project>();
        }

        internal async Task<IDictionary<string, List<EnvDTE.Project>>> GetDependentEnvDTEProjectsDictionaryAsync()
        {
            // Get all of the projects in the solution and build the reverse graph. i.e.
            // if A has a project reference to B (A -> B) the this will return B -> A
            // We need to run this on the ui thread so that it doesn't freeze for websites. Since there might be a
            // large number of references.
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            EnsureInitialize();

            var dependentEnvDTEProjectsDictionary = new Dictionary<string, List<Project>>();
            var envDTEProjects = GetEnvDTEProjects();

            foreach (EnvDTE.Project envDTEProj in envDTEProjects)
            {
                if (EnvDTEProjectUtility.SupportsReferences(envDTEProj))
                {
                    foreach (var referencedProject in EnvDTEProjectUtility.GetReferencedProjects(envDTEProj))
                    {
                        AddDependentProject(dependentEnvDTEProjectsDictionary, referencedProject, envDTEProj);
                    }
                }
            }

            return dependentEnvDTEProjectsDictionary;
        }

        private static void AddDependentProject(IDictionary<string, List<EnvDTE.Project>> dependentEnvDTEProjectsDictionary,
            EnvDTE.Project envDTEProject, EnvDTE.Project dependentEnvDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            string uniqueName = EnvDTEProjectUtility.GetUniqueName(envDTEProject);

            List<EnvDTE.Project> dependentEnvDTEProjects;
            if (!dependentEnvDTEProjectsDictionary.TryGetValue(uniqueName, out dependentEnvDTEProjects))
            {
                dependentEnvDTEProjects = new List<EnvDTE.Project>();
                dependentEnvDTEProjectsDictionary[uniqueName] = dependentEnvDTEProjects;
            }
            dependentEnvDTEProjects.Add(dependentEnvDTEProject);
        }

        /// <summary>
        /// This method is invoked when ProjectSystemCache fires a CacheUpdated event.
        /// This method inturn invokes AfterNuGetCacheUpdated event which is consumed by PackageManagerControl.xaml.cs
        /// </summary>
        /// <param name="sender">Event sender object</param>
        /// <param name="e">Event arguments. This will be EventArgs.Empty</param>
        private void NuGetCacheUpdate_After(object sender, NuGetEventArgs<string> e)
        {
            // The AfterNuGetCacheUpdated event is raised on a separate Task to prevent blocking of the caller.
            // E.g. - If Restore updates the cache entries on CPS nomination, then restore should not be blocked till UI is restored.
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => FireNuGetCacheUpdatedEventAsync(e));
        }

        private async Task FireNuGetCacheUpdatedEventAsync(NuGetEventArgs<string> e)
        {
            try
            {
                // Await a delay of 100 mSec to batch multiple cache updated events.
                // This ensures the minimum duration between 2 consecutive UI refresh, caused by cache update, to be 100 mSec.
                await Task.Delay(100);
                // Check if the cache is still dirty
                if (_projectSystemCache.TestResetDirtyFlag())
                {
                    // Fire the event only if the cache is dirty
                    AfterNuGetCacheUpdated?.Invoke(this, e);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }


        #region IVsSelectionEvents

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            if (dwCmdUICookie == _solutionLoadedUICookie
                && fActive == 1)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                OnSolutionExistsAndFullyLoaded();
            }

            return VSConstants.S_OK;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            return VSConstants.S_OK;
        }

        public int OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        public void OnActionsExecuted(IEnumerable<ResolvedAction> actions)
        {
            if (ActionsExecuted != null)
            {
                ActionsExecuted(this, new ActionsExecutedEventArgs(actions));
            }
        }

#endregion IVsSelectionEvents

#region IVsSolutionManager

        public async Task<NuGetProject> GetOrCreateProjectAsync(EnvDTE.Project project, INuGetProjectContext projectContext)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectSafeName = await EnvDTEProjectUtility.GetCustomUniqueNameAsync(project);
            var nuGetProject = GetNuGetProject(projectSafeName);

            // if the project does not exist in the solution (this is true for new templates)
            // create it manually
            if (nuGetProject == null)
            {
                nuGetProject = CreateNuGetProject(project, projectContext);
            }

            return nuGetProject;
        }

#endregion
    }
}