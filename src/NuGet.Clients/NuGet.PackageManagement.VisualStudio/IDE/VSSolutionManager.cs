// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Configuration;
using NuGet.PackageManagement.Telemetry;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
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
        private readonly IVsProjectAdapterProvider _vsProjectAdapterProvider;
        private readonly Common.ILogger _logger;
        private readonly Lazy<ISettings> _settings;

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

                _projectSystemCache.TryGetNuGetProject(DefaultNuGetProjectName, out var defaultNuGetProject);
                return defaultNuGetProject;
            }
        }

        public string DefaultNuGetProjectName { get; set; }

        #region Events

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

        #endregion Events

        [ImportingConstructor]
        internal VSSolutionManager(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IProjectSystemCache projectSystemCache,
            NuGetProjectFactory projectSystemFactory,
            ICredentialServiceProvider credentialServiceProvider,
            IVsProjectAdapterProvider vsProjectAdapterProvider,
            [Import("VisualStudioActivityLogger")]
            Common.ILogger logger,
            Lazy<ISettings> settings)
        {
            Assumes.Present(serviceProvider);
            Assumes.Present(projectSystemCache);
            Assumes.Present(projectSystemFactory);
            Assumes.Present(credentialServiceProvider);
            Assumes.Present(vsProjectAdapterProvider);
            Assumes.Present(logger);
            Assumes.Present(settings);

            _serviceProvider = serviceProvider;
            _projectSystemCache = projectSystemCache;
            _projectSystemFactory = projectSystemFactory;
            _credentialServiceProvider = credentialServiceProvider;
            _vsProjectAdapterProvider = vsProjectAdapterProvider;
            _logger = logger;
            _settings = settings;
        }

        private async Task InitializeAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = _serviceProvider.GetDTE();
            UserAgent.SetUserAgentString(
                    new UserAgentStringBuilder().WithVisualStudioSKU(dte.GetFullVsVersionString()));

            HttpHandlerResourceV3.CredentialService = _credentialServiceProvider.GetCredentialService();

            _vsSolution = _serviceProvider.GetService<SVsSolution, IVsSolution>();
            _vsMonitorSelection = _serviceProvider.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();

            var solutionLoadedGuid = VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_guid;
            _vsMonitorSelection.GetCmdUIContextCookie(ref solutionLoadedGuid, out _solutionLoadedUICookie);

            uint cookie;
            var hr = _vsMonitorSelection.AdviseSelectionEvents(this, out cookie);
            ErrorHandler.ThrowOnFailure(hr);

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
        }

        public NuGetProject GetNuGetProject(string nuGetProjectSafeName)
        {
            if (string.IsNullOrEmpty(nuGetProjectSafeName))
            {
                throw new ArgumentException(
                    Strings.Argument_Cannot_Be_Null_Or_Empty,
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
            var name = nuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            if (GetNuGetProject(name) == nuGetProject)
            {
                return name;
            }

            return NuGetProject.GetUniqueNameOrName(nuGetProject);
        }

        public IEnumerable<NuGetProject> GetNuGetProjects()
        {
            EnsureInitialize();

            // In certain cases project cache is populated with incomplete project data
            // Filter out null entries here.
            var projects = _projectSystemCache
                .GetNuGetProjects()
                .Where(p => p != null)
                .ToList();

            return projects;
        }

        public bool IsAllProjectsNominated()
        {
#if VS14
            // for VS14, always return true since nominations don't apply there.
            return true;
#else
            var netCoreProjects = GetNuGetProjects().OfType<NetCorePackageReferenceProject>().ToList();

            foreach (var project in netCoreProjects)
            {
                // check if this .Net core project is nominated or not.
                DependencyGraphSpec projectRestoreInfo;
                if (!_projectSystemCache.TryGetProjectRestoreInfo(project.MSBuildProjectPath, out projectRestoreInfo) ||
                    projectRestoreInfo == null)
                {
                    // there are projects still to be nominated.
                    return false;
                }
            }

            // return true if all the net core projects have been nominated.
            return true;
#endif
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

                    var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(_settings.Value);

                    return Path.IsPathRooted(globalPackagesFolder);
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
                    var solutionFilePath = await GetSolutionFilePathAsync();

                    if (string.IsNullOrEmpty(solutionFilePath))
                    {
                        return null;
                    }
                    return Path.GetDirectoryName(solutionFilePath);
                });
            }
        }

        private IEnumerable<IVsHierarchy> GetDeferredProjects()
        {
#if VS14
            // Not applicable for Dev14 so always return empty list.
            return Enumerable.Empty<IVsHierarchy>();
#else
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectIVsHierarchys = new List<IVsHierarchy>();

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
                    projectIVsHierarchys.Add(hierarchy[0]);
                }
            }

            return projectIVsHierarchys;
#endif
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
            ThreadHelper.ThrowIfNotOnUIThread();

            object value;
            var hr = _vsSolution.GetProperty(propId, out value);

            ErrorHandler.ThrowOnFailure(hr);

            return value;
        }

        private void OnSolutionExistsAndFullyLoaded()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SolutionOpening?.Invoke(this, EventArgs.Empty);

            // although the SolutionOpened event fires, the solution may be only in memory (e.g. when
            // doing File - New File). In that case, we don't want to act on the event.
            if (!IsSolutionOpen)
            {
                return;
            }

            NuGetUIThreadHelper.JoinableTaskFactory.Run(() => EnsureNuGetAndVsProjectAdapterCacheAsync());

            SolutionOpened?.Invoke(this, EventArgs.Empty);

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
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (!string.IsNullOrEmpty(oldName) && IsSolutionOpen && _solutionOpenedRaised)
                {
                    await EnsureNuGetAndVsProjectAdapterCacheAsync();

                    if (EnvDTEProjectUtility.IsSupported(envDTEProject))
                    {
                        RemoveVsProjectAdapterFromCache(oldName);

                        var vsProjectAdapter = await _vsProjectAdapterProvider.CreateAdapterForFullyLoadedProjectAsync(envDTEProject);
                        AddVsProjectAdapterToCache(vsProjectAdapter);

                        _projectSystemCache.TryGetNuGetProject(envDTEProject.Name, out var nuGetProject);

                        NuGetProjectRenamed?.Invoke(this, new NuGetProjectEventArgs(nuGetProject));

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
                            RemoveVsProjectAdapterFromCache(item.FullName);

                            var vsProjectAdapter = await _vsProjectAdapterProvider.CreateAdapterForFullyLoadedProjectAsync(item);
                            AddVsProjectAdapterToCache(vsProjectAdapter);
                        }
                    }
                }
            });
        }

        private void OnEnvDTEProjectRemoved(EnvDTE.Project envDTEProject)
        {
            // This is a solution event. Should be on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            RemoveVsProjectAdapterFromCache(envDTEProject.FullName);
            NuGetProject nuGetProject;
            _projectSystemCache.TryGetNuGetProject(envDTEProject.Name, out nuGetProject);

            NuGetProjectRemoved?.Invoke(this, new NuGetProjectEventArgs(nuGetProject));
        }

        private void OnEnvDTEProjectAdded(EnvDTE.Project envDTEProject)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (IsSolutionOpen
                    && EnvDTEProjectUtility.IsSupported(envDTEProject)
                    && !EnvDTEProjectUtility.IsParentProjectExplicitlyUnsupported(envDTEProject)
                    && _solutionOpenedRaised)
                {
                    await EnsureNuGetAndVsProjectAdapterCacheAsync();
                    var vsProjectAdapter = await _vsProjectAdapterProvider.CreateAdapterForFullyLoadedProjectAsync(envDTEProject);
                    AddVsProjectAdapterToCache(vsProjectAdapter);
                    NuGetProject nuGetProject;
                    _projectSystemCache.TryGetNuGetProject(envDTEProject.Name, out nuGetProject);

                    NuGetProjectAdded?.Invoke(this, new NuGetProjectEventArgs(nuGetProject));
                }
            });
        }

        private void SetDefaultProjectName()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IEnumerable<object> startupProjects;

            try
            {
                // when a new solution opens, we set its startup project as the default project in NuGet Console
                var dte = _serviceProvider.GetDTE();
                var solutionBuild = dte.Solution.SolutionBuild as SolutionBuild2;
                startupProjects = solutionBuild?.StartupProjects as IEnumerable<object>;
            }
            catch (COMException)
            {
                // get_StartupProjects misbehaves for certain project types, so ignore this failure
                return;
            }

            var startupProjectName = startupProjects?.Cast<string>().FirstOrDefault();
            if (!string.IsNullOrEmpty(startupProjectName))
            {
                if (_projectSystemCache.TryGetProjectNames(startupProjectName, out var projectName))
                {
                    DefaultNuGetProjectName = _projectSystemCache.IsAmbiguous(projectName.ShortName) ?
                        projectName.CustomUniqueName :
                        projectName.ShortName;
                }
            }
        }

        private async Task EnsureNuGetAndVsProjectAdapterCacheAsync()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_cacheInitialized && IsSolutionOpen)
            {
                try
                {
                    var deferedProjects = GetDeferredProjects();

                    foreach (var project in deferedProjects)
                    {
                        try
                        {
                            var vsProjectAdapter = await _vsProjectAdapterProvider.CreateAdapterForDeferredProjectAsync(project);
                            AddVsProjectAdapterToCache(vsProjectAdapter);
                        }
                        catch (Exception e)
                        {
                            // Ignore failed projects.
                            _logger.LogWarning($"The project {project} failed to initialize as a NuGet project.");
                            _logger.LogError(e.ToString());
                        }

                        // Consider that the cache is initialized only when there are any projects to add.
                        _cacheInitialized = true;
                    }

                    var dte = _serviceProvider.GetDTE();

                    var supportedProjects = EnvDTESolutionUtility
                        .GetAllEnvDTEProjects(dte)
                        .Where(EnvDTEProjectUtility.IsSupported);

                    foreach (var project in supportedProjects)
                    {
                        try
                        {
                            var vsProjectAdapter = await _vsProjectAdapterProvider.CreateAdapterForFullyLoadedProjectAsync(project);
                            AddVsProjectAdapterToCache(vsProjectAdapter);
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

        private void AddVsProjectAdapterToCache(IVsProjectAdapter vsProjectAdapter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!vsProjectAdapter.IsSupported)
            {
                return;
            }

            _projectSystemCache.TryGetProjectNameByShortName(vsProjectAdapter.ProjectName, out ProjectNames oldProjectName);

            // Create the NuGet project first. If this throws we bail out and do not change the cache.
            var nuGetProject = CreateNuGetProject(vsProjectAdapter);

            // Then create the project name from the project.
            var newProjectName = vsProjectAdapter.ProjectNames;

            // Finally, try to add the project to the cache.
            var added = _projectSystemCache.AddProject(newProjectName, vsProjectAdapter, nuGetProject);

            if (added && nuGetProject != null)
            {
                // Emit project specific telemetry as we are adding the project to the cache.
                // This ensures we do not emit the events over and over while the solution is
                // open.
                NuGetProjectTelemetryService.Instance.EmitNuGetProject(nuGetProject);
            }

            if (string.IsNullOrEmpty(DefaultNuGetProjectName) ||
                newProjectName.ShortName.Equals(DefaultNuGetProjectName, StringComparison.OrdinalIgnoreCase))
            {
                DefaultNuGetProjectName = oldProjectName != null ?
                    oldProjectName.CustomUniqueName :
                    newProjectName.ShortName;
            }
        }

        private void RemoveVsProjectAdapterFromCache(string name)
        {
            // Do nothing if the cache hasn't been set up
            if (_projectSystemCache == null)
            {
                return;
            }

            _projectSystemCache.TryGetProjectNames(name, out var projectNames);

            // Remove the project from the cache
            _projectSystemCache.RemoveProject(name);

            if (!_projectSystemCache.ContainsKey(DefaultNuGetProjectName))
            {
                DefaultNuGetProjectName = null;
            }

            // for LightSwitch project, the main project is not added to _projectCache, but it is called on removal.
            // in that case, projectName is null.
            if (projectNames != null
                && projectNames.CustomUniqueName.Equals(DefaultNuGetProjectName, StringComparison.OrdinalIgnoreCase)
                && !_projectSystemCache.IsAmbiguous(projectNames.ShortName))
            {
                DefaultNuGetProjectName = projectNames.ShortName;
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
                            await EnsureNuGetAndVsProjectAdapterCacheAsync();
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

        private NuGetProject CreateNuGetProject(IVsProjectAdapter project, INuGetProjectContext projectContext = null)
        {
            var context = new ProjectProviderContext(
                projectContext ?? EmptyNuGetProjectContext,
                () => PackagesFolderPathUtility.GetPackagesFolderPath(this, _settings.Value));

            if (_projectSystemFactory.TryCreateNuGetProject(project, context, out var result))
            {
                return result;
            }

            return null;
        }

        internal async Task<IDictionary<string, List<IVsProjectAdapter>>> GetDependentProjectsDictionaryAsync()
        {
            // Get all of the projects in the solution and build the reverse graph. i.e.
            // if A has a project reference to B (A -> B) the this will return B -> A
            // We need to run this on the ui thread so that it doesn't freeze for websites. Since there might be a
            // large number of references.
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            EnsureInitialize();

            var dependentProjectsDictionary = new Dictionary<string, List<IVsProjectAdapter>>();
            var vsProjectAdapters = GetAllVsProjectAdapters();

            foreach (var vsProjectAdapter in vsProjectAdapters)
            {
                var referencedProjects = await vsProjectAdapter.GetReferencedProjectsAsync();
                foreach (var projectProjectPath in referencedProjects)
                {
                    var result = _projectSystemCache.TryGetVsProjectAdapter(projectProjectPath, out IVsProjectAdapter vsReferencedProject);
                    if (result)
                    {
                        AddDependentProject(dependentProjectsDictionary, vsReferencedProject, vsProjectAdapter);
                    }
                }
            }

            return dependentProjectsDictionary;
        }

        private static void AddDependentProject(
            IDictionary<string, List<IVsProjectAdapter>> dependentProjectsDictionary,
            IVsProjectAdapter vsProjectAdapter,
            IVsProjectAdapter dependentVsProjectAdapter)
        {
            var uniqueName = vsProjectAdapter.UniqueName;

            if (!dependentProjectsDictionary.TryGetValue(uniqueName, out List<IVsProjectAdapter> dependentProjects))
            {
                dependentProjects = new List<IVsProjectAdapter>();
                dependentProjectsDictionary[uniqueName] = dependentProjects;
            }
            dependentProjects.Add(dependentVsProjectAdapter);
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

            var projectSafeName = await EnvDTEProjectInfoUtility.GetCustomUniqueNameAsync(project);
            var nuGetProject = GetNuGetProject(projectSafeName);

            // if the project does not exist in the solution (this is true for new templates)
            // create it manually
            if (nuGetProject == null)
            {
                var vsProjectAdapter = await _vsProjectAdapterProvider.CreateAdapterForFullyLoadedProjectAsync(project);
                nuGetProject = CreateNuGetProject(vsProjectAdapter, projectContext);
            }

            return nuGetProject;
        }

        public IVsProjectAdapter GetVsProjectAdapter(string nuGetProjectSafeName)
        {
            Assumes.NotNullOrEmpty(nuGetProjectSafeName);

            EnsureInitialize();

            _projectSystemCache.TryGetVsProjectAdapter(nuGetProjectSafeName, out var vsProjectAdapter);
            return vsProjectAdapter;
        }

        public IVsProjectAdapter GetVsProjectAdapter(NuGetProject nuGetProject)
        {
            Assumes.Present(nuGetProject);

            EnsureInitialize();

            var nuGetProjectSafeName = GetNuGetProjectSafeName(nuGetProject);

            _projectSystemCache.TryGetVsProjectAdapter(nuGetProjectSafeName, out var vsProjectAdapter);
            return vsProjectAdapter;
        }

        public async Task<bool> IsSolutionFullyLoadedAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            EnsureInitialize();
            var value = GetVSSolutionProperty((int)(__VSPROPID4.VSPROPID_IsSolutionFullyLoaded));
            return (bool)value;
        }

        public IEnumerable<IVsProjectAdapter> GetAllVsProjectAdapters()
        {
            EnsureInitialize();
            return _projectSystemCache.GetVsProjectAdapters();
        }

        public async Task<NuGetProject> UpgradeProjectToPackageReferenceAsync(NuGetProject oldProject)
        {
#if VS14
            // do nothing for VS 2015 and simply return the existing NuGetProject
            if (NuGetProjectUpdated != null)
            {
                NuGetProjectUpdated(this, new NuGetProjectEventArgs(oldProject));
            }

            return await Task.FromResult(oldProject);
#else
            Assumes.Present(oldProject);

            var projectName = GetNuGetProjectSafeName(oldProject);
            var vsProjectAdapter = GetVsProjectAdapter(projectName);

            _projectSystemCache.TryGetProjectNames(projectName, out var projectNames);

            RemoveVsProjectAdapterFromCache(projectName);

            var context = new ProjectProviderContext(
                EmptyNuGetProjectContext,
                () => PackagesFolderPathUtility.GetPackagesFolderPath(this, _settings.Value));

            var nuGetProject = await _projectSystemFactory.CreateNuGetProjectAsync<LegacyPackageReferenceProject>(
                vsProjectAdapter, context);

            var added = _projectSystemCache.AddProject(projectNames, vsProjectAdapter, nuGetProject);

            if (DefaultNuGetProjectName == null)
            {
                DefaultNuGetProjectName = projectName;
            }

            NuGetProjectUpdated?.Invoke(this, new NuGetProjectEventArgs(nuGetProject));

            return nuGetProject;
#endif
        }

        #endregion
    }
}