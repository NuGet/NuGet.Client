// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace NuGetConsole.Host.PowerShell.Implementation
{
    internal abstract class PowerShellHost : IHost, IPathExpansion, IDisposable
    {
        private readonly AsyncSemaphore _initScriptsLock = new AsyncSemaphore(1);
        private readonly string _name;
        private readonly IRunspaceManager _runspaceManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly ISolutionManager _solutionManager;
        private readonly ISettings _settings;
        private readonly ISourceControlManagerProvider _sourceControlManagerProvider;
        private readonly ICommonOperations _commonOperations;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;
        private const string ActivePackageSourceKey = "activePackageSource";
        private const string SyncModeKey = "IsSyncMode";
        private const string PackageManagementContextKey = "PackageManagementContext";
        private const string DTEKey = "DTE";
        private const string CancellationTokenKey = "CancellationTokenKey";
        private string _activePackageSource;
        private readonly DTE _dte;

        private IConsole _activeConsole;
        private NuGetPSHost _nugetHost;
        // indicates whether this host has been initialized.
        // null = not initilized, true = initialized successfully, false = initialized unsuccessfully
        private bool? _initialized;
        // store the current (non-truncated) project names displayed in the project name combobox
        private string[] _projectSafeNames;

        // store the current command typed so far
        private ComplexCommand _complexCommand;

        // store the current CancellationToken. This will be set on the private data
        private CancellationToken _token;

        private List<SourceRepository> _sourceRepositories;

        protected PowerShellHost(string name, IRunspaceManager runspaceManager)
        {
            _runspaceManager = runspaceManager;

            // TODO: Take these as ctor arguments
            _sourceRepositoryProvider = ServiceLocator.GetInstance<ISourceRepositoryProvider>();
            _solutionManager = ServiceLocator.GetInstance<ISolutionManager>();
            _settings = ServiceLocator.GetInstance<ISettings>();
            _deleteOnRestartManager = ServiceLocator.GetInstance<IDeleteOnRestartManager>();

            _dte = ServiceLocator.GetInstance<DTE>();
            _sourceControlManagerProvider = ServiceLocator.GetInstanceSafe<ISourceControlManagerProvider>();
            _commonOperations = ServiceLocator.GetInstanceSafe<ICommonOperations>();
            PackageManagementContext = new PackageManagementContext(_sourceRepositoryProvider, _solutionManager,
                _settings, _sourceControlManagerProvider, _commonOperations);

            _name = name;
            IsCommandEnabled = true;

            InitializeSources();

            _sourceRepositoryProvider.PackageSourceProvider.PackageSourcesChanged += PackageSourceProvider_PackageSourcesChanged;
        }

        private void InitializeSources()
        {
            _sourceRepositories = _sourceRepositoryProvider
                .GetRepositories()
                .Where(repo => repo.PackageSource.IsEnabled)
                .ToList();

            _activePackageSource = _sourceRepositoryProvider.PackageSourceProvider.ActivePackageSourceName;

            // check if active package source name is valid
            var activeSource = _sourceRepositories.FirstOrDefault(
                repo => StringComparer.CurrentCultureIgnoreCase.Equals(repo.PackageSource.Name, _activePackageSource))
                               ?? _sourceRepositories.FirstOrDefault();

            _activePackageSource = activeSource?.PackageSource.Name;
        }

        #region Properties

        protected Pipeline ExecutingPipeline { get; set; }

        /// <summary>
        /// The host is associated with a particular console on a per-command basis.
        /// This gets set every time a command is executed on this host.
        /// </summary>
        protected IConsole ActiveConsole
        {
            get { return _activeConsole; }
            set
            {
                _activeConsole = value;
                if (_nugetHost != null)
                {
                    _nugetHost.ActiveConsole = value;
                }
            }
        }

        public bool IsCommandEnabled { get; private set; }

        protected RunspaceDispatcher Runspace { get; private set; }

        private ComplexCommand ComplexCommand
        {
            get
            {
                if (_complexCommand == null)
                {
                    _complexCommand = new ComplexCommand((allLines, lastLine) =>
                        {
                            Collection<PSParseError> errors;
                            PSParser.Tokenize(allLines, out errors);

                            // If there is a parse error token whose END is past input END, consider
                            // it a multi-line command.
                            if (errors.Count > 0)
                            {
                                if (errors.Any(e => (e.Token.Start + e.Token.Length) >= allLines.Length))
                                {
                                    return false;
                                }
                            }

                            return true;
                        });
                }
                return _complexCommand;
            }
        }

        public string Prompt
        {
            get { return ComplexCommand.IsComplete ? EvaluatePrompt() : ">> "; }
        }

        public PackageManagementContext PackageManagementContext { get; set; }

        #endregion

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private string EvaluatePrompt()
        {
            var prompt = "PM>";

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                try
                {
                    // Execute the prompt function from a worker thread, so that the UI thread is not blocked waiting
                    // on it. Note that a default prompt function as defined in Profile.ps1 will simply return
                    // a string "PM>". This will always work. However, a custom "prompt" function might call
                    // Write-Host and NuGet will explicity switch to the main thread using JTF.
                    // If the main thread was blocked then, it will consistently result in a hang.
                    var output = await Task.Run(() =>
                                        Runspace.Invoke("prompt", null, outputResults: false).FirstOrDefault());
                    if (output != null)
                    {
                        var result = output.BaseObject.ToString();
                        if (!string.IsNullOrEmpty(result))
                        {
                            prompt = result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExceptionHelper.WriteToActivityLog(ex);
                }
                return prompt;
            });
        }

        /// <summary>
        /// Doing all necessary initialization works before the console accepts user inputs
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public void Initialize(IConsole console)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    ActiveConsole = console;
                    if (_initialized.HasValue)
                    {
                        if (_initialized.Value
                            && console.ShowDisclaimerHeader)
                        {
                            DisplayDisclaimerAndHelpText();
                        }
                    }
                    else
                    {
                        try
                        {
                            Tuple<RunspaceDispatcher, NuGetPSHost> result = _runspaceManager.GetRunspace(console, _name);
                            Runspace = result.Item1;
                            _nugetHost = result.Item2;

                            _initialized = true;

                            if (console.ShowDisclaimerHeader)
                            {
                                DisplayDisclaimerAndHelpText();
                            }

                            UpdateWorkingDirectory();
                            await ExecuteInitScriptsAsync();

                            // Hook up solution events
                            _solutionManager.SolutionOpened += (o, e) =>
                                {
                                    // Solution opened event is raised on the UI thread
                                    // Go off the UI thread before calling likely expensive call of ExecuteInitScriptsAsync
                                    // Also, it uses semaphores, do not call it from the UI thread
                                    Task.Run(delegate
                                        {
                                            UpdateWorkingDirectory();
                                            return ExecuteInitScriptsAsync();
                                        });
                                };
                            _solutionManager.SolutionClosed += (o, e) => UpdateWorkingDirectory();
                            _solutionManager.NuGetProjectAdded += (o, e) => UpdateWorkingDirectoryAndAvailableProjects();
                            _solutionManager.NuGetProjectRenamed += (o, e) => UpdateWorkingDirectoryAndAvailableProjects();
                            _solutionManager.NuGetProjectRemoved += (o, e) =>
                                {
                                    UpdateWorkingDirectoryAndAvailableProjects();
                                    // When the previous default project has been removed, _solutionManager.DefaultNuGetProjectName becomes null
                                    if (_solutionManager.DefaultNuGetProjectName == null)
                                    {
                                        // Change default project to the first one in the collection
                                        SetDefaultProjectIndex(0);
                                    }
                                };
                            // Set available private data on Host
                            SetPrivateDataOnHost(false);
                        }
                        catch (Exception ex)
                        {
                            // catch all exception as we don't want it to crash VS
                            _initialized = false;
                            IsCommandEnabled = false;
                            ReportError(ex);

                            ExceptionHelper.WriteToActivityLog(ex);
                        }
                    }
                });
        }

        private void UpdateWorkingDirectoryAndAvailableProjects()
        {
            UpdateWorkingDirectory();
            GetAvailableProjects();
        }

        private void UpdateWorkingDirectory()
        {
            if (Runspace.RunspaceAvailability == RunspaceAvailability.Available)
            {
                // if there is no solution open, we set the active directory to be user profile folder
                string targetDir = _solutionManager.IsSolutionOpen ?
                    _solutionManager.SolutionDirectory :
                    Environment.GetEnvironmentVariable("USERPROFILE");

                Runspace.ChangePSDirectory(targetDir);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want execution of init scripts to crash our console.")]
        private async Task ExecuteInitScriptsAsync()
        {
            // Fix for Bug 1426 Disallow ExecuteInitScripts from being executed concurrently by multiple threads.
            using (await _initScriptsLock.EnterAsync())
            {
                if (!_solutionManager.IsSolutionOpen)
                {
                    return;
                }

                Debug.Assert(_settings != null);
                if (_settings == null)
                {
                    return;
                }

                // invoke init.ps1 files in the order of package dependency.
                // if A -> B, we invoke B's init.ps1 before A's.
                var sortedPackages = new List<PackageIdentity>();

                var packagesFolderPackages = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
                var globalPackages = new HashSet<PackageIdentity>(PackageIdentity.Comparer);

                var projects = _solutionManager.GetNuGetProjects().ToList();
                var packageManager = new NuGetPackageManager(
                    _sourceRepositoryProvider,
                    _settings,
                    _solutionManager,
                    _deleteOnRestartManager);

                foreach (var project in projects)
                {
                    // Skip project K projects.
                    if (project is ProjectKNuGetProjectBase)
                    {
                        continue;
                    }

                    var buildIntegratedProject = project as BuildIntegratedNuGetProject;

                    if (buildIntegratedProject != null)
                    {
                        var packages = BuildIntegratedProjectUtility.GetOrderedProjectDependencies(buildIntegratedProject);
                        sortedPackages.AddRange(packages);
                        globalPackages.UnionWith(packages);
                    }
                    else
                    {
                        var installedRefs = await project.GetInstalledPackagesAsync(CancellationToken.None);

                        if (installedRefs != null
                            && installedRefs.Any())
                        {
                            // This will be an empty list if packages have not been restored
                            var installedPackages = await packageManager.GetInstalledPackagesInDependencyOrder(project, CancellationToken.None);
                            sortedPackages.AddRange(installedPackages);
                            packagesFolderPackages.UnionWith(installedPackages);
                        }
                    }
                }

                // Get the path to the Packages folder.
                var packagesFolderPath = packageManager.PackagesFolderSourceRepository.PackageSource.Source;
                var packagePathResolver = new PackagePathResolver(packagesFolderPath);

                var globalFolderPath = SettingsUtility.GetGlobalPackagesFolder(_settings);
                var globalPathResolver = new VersionFolderPathResolver(globalFolderPath);

                var finishedPackages = new HashSet<PackageIdentity>(PackageIdentity.Comparer);

                foreach (var package in sortedPackages)
                {
                    // Packages may occur under multiple projects, but we only need to run it once.
                    if (!finishedPackages.Contains(package))
                    {
                        finishedPackages.Add(package);

                        try
                        {
                            string pathToPackage = null;

                            // If the package exists in both the global and packages folder, use the packages folder copy.
                            if (packagesFolderPackages.Contains(package))
                            {
                                // Local package in the packages folder
                                pathToPackage = packagePathResolver.GetInstalledPath(package);
                            }
                            else
                            {
                                // Global package
                                pathToPackage = globalPathResolver.GetInstallPath(package.Id, package.Version);
                            }

                            if (!string.IsNullOrEmpty(pathToPackage))
                            {
                                var toolsPath = Path.Combine(pathToPackage, "tools");
                                var scriptPath = Path.Combine(toolsPath, PowerShellScripts.Init);

                                if (Directory.Exists(toolsPath))
                                {
                                    AddPathToEnvironment(toolsPath);
                                    Runspace.ExecuteScript(pathToPackage, scriptPath, package);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // if execution of Init scripts fails, do not let it crash our console
                            ReportError(ex);

                            ExceptionHelper.WriteToActivityLog(ex);
                        }
                    }
                }
            }
        }

        private static void AddPathToEnvironment(string path)
        {
            if (Directory.Exists(path))
            {
                string environmentPath = Environment.GetEnvironmentVariable("path", EnvironmentVariableTarget.Process);
                environmentPath = environmentPath + ";" + path;
                Environment.SetEnvironmentVariable("path", environmentPath, EnvironmentVariableTarget.Process);
            }
        }

        protected abstract bool ExecuteHost(string fullCommand, string command, params object[] inputs);

        public bool Execute(IConsole console, string command, params object[] inputs)
        {
            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageManagerConsoleCommandExecutionBegin);
            ActiveConsole = console;

            string fullCommand;
            if (ComplexCommand.AddLine(command, out fullCommand)
                && !string.IsNullOrEmpty(fullCommand))
            {
                return ExecuteHost(fullCommand, command, inputs);
            }

            return false; // constructing multi-line command
        }

        protected static void OnExecuteCommandEnd()
        {
            NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.PackageManagerConsoleCommandExecutionEnd);
        }

        public void Abort()
        {
            ExecutingPipeline?.StopAsync();
            ComplexCommand.Clear();
        }

        protected void SetPrivateDataOnHost(bool isSync)
        {
            SetPropertyValueOnHost(SyncModeKey, isSync);
            SetPropertyValueOnHost(PackageManagementContextKey, PackageManagementContext);
            SetPropertyValueOnHost(ActivePackageSourceKey, ActivePackageSource);
            SetPropertyValueOnHost(DTEKey, _dte);
            SetPropertyValueOnHost(CancellationTokenKey, _token);
        }

        private void SetPropertyValueOnHost(string propertyName, object value)
        {
            if (_nugetHost != null)
            {
                PSPropertyInfo property = _nugetHost.PrivateData.Properties[propertyName];
                if (property == null)
                {
                    property = new PSNoteProperty(propertyName, value);
                    _nugetHost.PrivateData.Properties.Add(property);
                }
                else
                {
                    property.Value = value;
                }
            }
        }

        public void SetDefaultRunspace()
        {
            Runspace.MakeDefault();
        }

        private void DisplayDisclaimerAndHelpText()
        {
            WriteLine(Resources.Console_DisclaimerText);
            WriteLine();

            WriteLine(String.Format(CultureInfo.CurrentCulture, Resources.PowerShellHostTitle, _nugetHost.Version));
            WriteLine();

            WriteLine(Resources.Console_HelpText);
            WriteLine();
        }

        protected void ReportError(ErrorRecord record)
        {
            WriteErrorLine(Runspace.ExtractErrorFromErrorRecord(record));
        }

        protected void ReportError(Exception exception)
        {
            exception = ExceptionHelper.Unwrap(exception);
            WriteErrorLine(exception.Message);
        }

        private void WriteErrorLine(string message)
        {
            ActiveConsole?.Write(message + Environment.NewLine, Colors.Red, null);
        }

        private void WriteLine(string message = "")
        {
            ActiveConsole?.WriteLine(message);
        }

        public string ActivePackageSource
        {
            get { return _activePackageSource; }
            set
            {
                _activePackageSource = value;
                var source = _sourceRepositories
                    .FirstOrDefault(s =>
                        StringComparer.CurrentCultureIgnoreCase.Equals(_activePackageSource, s.PackageSource.Name));
                if (source != null)
                {
                    _sourceRepositoryProvider.PackageSourceProvider.SaveActivePackageSource(source.PackageSource);
                }
            }
        }

        public string[] GetPackageSources()
        {
            return _sourceRepositories.Select(repo => repo.PackageSource.Name).ToArray();
        }

        private void PackageSourceProvider_PackageSourcesChanged(object sender, EventArgs e)
        {
            _sourceRepositories = _sourceRepositoryProvider
                .GetRepositories()
                .Where(repo => repo.PackageSource.IsEnabled)
                .ToList();

            string oldActiveSource = ActivePackageSource;
            SetNewActiveSource(oldActiveSource);
        }

        private void SetNewActiveSource(string oldActiveSource)
        {
            if (!_sourceRepositories.Any())
            {
                ActivePackageSource = string.Empty;
            }
            else
            {
                if (oldActiveSource == null)
                {
                    // use the first enabled source as the active source
                    ActivePackageSource = _sourceRepositories.First().PackageSource.Name;
                }
                else
                {
                    var s = _sourceRepositories.FirstOrDefault(
                        p => StringComparer.CurrentCultureIgnoreCase.Equals(p.PackageSource.Name, oldActiveSource));
                    if (s == null)
                    {
                        // the old active source does not exist any more. In this case,
                        // use the first eneabled source as the active source.
                        ActivePackageSource = _sourceRepositories.First().PackageSource.Name;
                    }
                    else
                    {
                        // the old active source still exists. Keep it as the active source.
                        ActivePackageSource = s.PackageSource.Name;
                    }
                }
            }
        }

        public string DefaultProject
        {
            get
            {
                Debug.Assert(_solutionManager != null);
                if (_solutionManager.DefaultNuGetProject == null)
                {
                    return null;
                }

                return GetDisplayName(_solutionManager.DefaultNuGetProject);
            }
        }

        public void SetDefaultProjectIndex(int selectedIndex)
        {
            Debug.Assert(_solutionManager != null);

            if (_projectSafeNames != null
                && selectedIndex >= 0
                && selectedIndex < _projectSafeNames.Length)
            {
                _solutionManager.DefaultNuGetProjectName = _projectSafeNames[selectedIndex];
            }
            else
            {
                _solutionManager.DefaultNuGetProjectName = null;
            }
        }

        public string[] GetAvailableProjects()
        {
            Debug.Assert(_solutionManager != null);

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var allProjects = _solutionManager.GetNuGetProjects();
                    _projectSafeNames = allProjects.Select(_solutionManager.GetNuGetProjectSafeName).ToArray();
                    var displayNames = GetDisplayNames(allProjects).ToArray();
                    Array.Sort(displayNames, _projectSafeNames, StringComparer.CurrentCultureIgnoreCase);
                    return _projectSafeNames;
                });
        }

        private IEnumerable<string> GetDisplayNames(IEnumerable<NuGetProject> allProjects)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            List<string> projectNames = new List<string>();
            var solutionManager = (VSSolutionManager)_solutionManager;
            foreach (var nuGetProject in allProjects)
            {
                string displayName = GetDisplayName(nuGetProject, solutionManager);
                projectNames.Add(displayName);
            }
            return projectNames;
        }

        private string GetDisplayName(NuGetProject nuGetProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var solutionManager = (VSSolutionManager)_solutionManager;
            return GetDisplayName(nuGetProject, solutionManager);
        }

        private static string GetDisplayName(NuGetProject nuGetProject, VSSolutionManager solutionManager)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var safeName = solutionManager.GetNuGetProjectSafeName(nuGetProject);
            var project = solutionManager.GetDTEProject(safeName);
            return EnvDTEProjectUtility.GetDisplayName(project);
        }

        #region ITabExpansion

        public Task<string[]> GetExpansionsAsync(string line, string lastWord, CancellationToken token)
        {
            return GetExpansionsAsyncCore(line, lastWord, token);
        }

        protected abstract Task<string[]> GetExpansionsAsyncCore(string line, string lastWord, CancellationToken token);

        protected async Task<string[]> GetExpansionsAsyncCore(string line, string lastWord, bool isSync, CancellationToken token)
        {
            // Set the _token object to the CancellationToken passed in, so that the Private Data can be set with this token
            // Powershell cmdlets will pick up the CancellationToken from the private data of the Host, and use it in their calls to NuGetPackageManager
            _token = token;
            string[] expansions;
            try
            {
                SetPrivateDataOnHost(isSync);
                expansions = await Task.Run(() =>
                    {
                        var query = from s in Runspace.Invoke(
                            @"$__pc_args=@();$input|%{$__pc_args+=$_};if(Test-Path Function:\TabExpansion2){(TabExpansion2 $__pc_args[0] $__pc_args[0].length).CompletionMatches|%{$_.CompletionText}}else{TabExpansion $__pc_args[0] $__pc_args[1]};Remove-Variable __pc_args -Scope 0;",
                            new[] { line, lastWord },
                            outputResults: false)
                            select (s == null ? null : s.ToString());
                        return query.ToArray();
                    }, _token);
            }
            finally
            {
                // Set the _token object to the CancellationToken passed in, so that the Private Data can be set correctly
                _token = CancellationToken.None;
            }

            return expansions;
        }

        #endregion

        #region IPathExpansion

        public Task<SimpleExpansion> GetPathExpansionsAsync(string line, CancellationToken token)
        {
            return GetPathExpansionsAsyncCore(line, token);
        }

        protected abstract Task<SimpleExpansion> GetPathExpansionsAsyncCore(string line, CancellationToken token);

        protected async Task<SimpleExpansion> GetPathExpansionsAsyncCore(string line, bool isSync, CancellationToken token)
        {
            // Set the _token object to the CancellationToken passed in, so that the Private Data can be set with this token
            // Powershell cmdlets will pick up the CancellationToken from the private data of the Host, and use it in their calls to NuGetPackageManager
            _token = token;
            SetPropertyValueOnHost(CancellationTokenKey, _token);
            var simpleExpansion = await Task.Run(() =>
                {
                    PSObject expansion = Runspace.Invoke(
                        "$input|%{$__pc_args=$_}; _TabExpansionPath $__pc_args; Remove-Variable __pc_args -Scope 0",
                        new object[] { line },
                        outputResults: false).FirstOrDefault();
                    if (expansion != null)
                    {
                        int replaceStart = (int)expansion.Properties["ReplaceStart"].Value;
                        IList<string> paths = ((IEnumerable<object>)expansion.Properties["Paths"].Value).Select(o => o.ToString()).ToList();
                        return new SimpleExpansion(replaceStart, line.Length - replaceStart, paths);
                    }

                    return null;
                }, token);

            _token = CancellationToken.None;
            SetPropertyValueOnHost(CancellationTokenKey, _token);
            return simpleExpansion;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _initScriptsLock.Dispose();
            Runspace?.Dispose();
        }

        #endregion
    }
}
