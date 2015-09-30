// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = NuGet.ProjectManagement.ExecutionContext;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using System.Xml.Linq;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "Disposing _blockingCollection is problematic since it continues to be used after the cmdlet has been disposed.")]
    /// <summary>
    /// This command process the specified package against the specified project.
    /// </summary>
    public abstract class NuGetPowerShellBaseCommand : PSCmdlet, IPSNuGetProjectContext, IErrorHandler
    {
        #region Members

        private readonly BlockingCollection<Message> _blockingCollection = new BlockingCollection<Message>();
        private readonly Semaphore _scriptEndSemaphore = new Semaphore(0, Int32.MaxValue);
        private readonly ISourceRepositoryProvider _resourceRepositoryProvider;
        private readonly ICommonOperations _commonOperations;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;

        // TODO: Hook up DownloadResource.Progress event
        private readonly IHttpClientEvents _httpClientEvents;

        private ProgressRecordCollection _progressRecordCache;
        private Exception _scriptException;
        private bool _overwriteAll;
        private bool _ignoreAll;
        internal const string PowerConsoleHostName = "Package Manager Host";
        internal const string ActivePackageSourceKey = "activePackageSource";
        internal const string SyncModeKey = "IsSyncMode";
        private const string CancellationTokenKey = "CancellationTokenKey";

        #endregion Members

        protected NuGetPowerShellBaseCommand()
        {
            _resourceRepositoryProvider = ServiceLocator.GetInstance<ISourceRepositoryProvider>();
            ConfigSettings = ServiceLocator.GetInstance<Configuration.ISettings>();
            VsSolutionManager = ServiceLocator.GetInstance<ISolutionManager>();
            DTE = ServiceLocator.GetInstance<DTE>();
            SourceControlManagerProvider = ServiceLocator.GetInstance<ISourceControlManagerProvider>();
            _commonOperations = ServiceLocator.GetInstance<ICommonOperations>();
            PackageRestoreManager = ServiceLocator.GetInstance<IPackageRestoreManager>();
            _deleteOnRestartManager = ServiceLocator.GetInstance<IDeleteOnRestartManager>();

            if (_commonOperations != null)
            {
                ExecutionContext = new IDEExecutionContext(_commonOperations);
            }
        }

        #region Properties

        public XDocument OriginalPackagesConfig { get; set; }

        /// <summary>
        /// NuGet Package Manager for PowerShell Cmdlets
        /// </summary>
        protected NuGetPackageManager PackageManager
        {
            get { return new NuGetPackageManager(
                _resourceRepositoryProvider,
                ConfigSettings,
                VsSolutionManager,
                _deleteOnRestartManager); }
        }

        /// <summary>
        /// Vs Solution Manager for PowerShell Cmdlets
        /// </summary>
        protected ISolutionManager VsSolutionManager { get; }

        protected IPackageRestoreManager PackageRestoreManager { get; private set; }

        /// <summary>
        /// Package Source Provider
        /// </summary>
        protected Configuration.PackageSourceProvider PackageSourceProvider
        {
            get { return new Configuration.PackageSourceProvider(ConfigSettings); }
        }

        /// <summary>
        /// Active Source Repository for PowerShell Cmdlets
        /// </summary>
        protected SourceRepository ActiveSourceRepository { get; set; }

        /// <summary>
        /// List of all the enabled source repositories
        /// </summary>
        protected List<SourceRepository> EnabledSourceRepositories { get; private set; }

        /// <summary>
        /// Settings read from the config files
        /// </summary>
        protected Configuration.ISettings ConfigSettings { get; }

        /// <summary>
        /// DTE instance for PowerShell Cmdlets
        /// </summary>
        protected DTE DTE { get; }

        /// <summary>
        /// NuGet Project
        /// </summary>
        protected NuGetProject Project { get; set; }

        /// <summary>
        /// File conflict action property
        /// </summary>
        protected FileConflictAction? ConflictAction { get; set; }

        protected CancellationToken Token
        {
            get
            {
                if (Host == null
                    || Host.PrivateData == null)
                {
                    return CancellationToken.None;
                }

                var tokenProp = GetPropertyValueFromHost(CancellationTokenKey);
                if (tokenProp == null)
                {
                    return CancellationToken.None;
                }

                return (CancellationToken)tokenProp;
            }
        }

        /// <summary>
        /// Determine if current PowerShell host is sync or async
        /// </summary>
        internal bool IsSyncMode
        {
            get
            {
                if (Host == null
                    || Host.PrivateData == null)
                {
                    return false;
                }

                var syncModeProp = GetPropertyValueFromHost(SyncModeKey);
                return syncModeProp != null && (bool)syncModeProp;
            }
        }

        /// <summary>
        /// Error handler
        /// </summary>
        protected IErrorHandler ErrorHandler
        {
            get { return this; }
        }

        #endregion Properties

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to display friendly message to the console.")]
        protected override sealed void ProcessRecord()
        {
            try
            {
                ProcessRecordCore();
            }
            catch (Exception ex)
            {
                ExceptionHelper.WriteToActivityLog(ex);

                // unhandled exceptions should be terminating
                ErrorHandler.HandleException(ex, terminating: true);
            }
            finally
            {
                UnsubscribeEvents();
            }
        }

        /// <summary>
        /// Derived classess must implement this method instead of ProcessRecord(), which is sealed by
        /// NuGetPowerShellBaseCommand.
        /// </summary>
        protected abstract void ProcessRecordCore();

        protected async Task CheckMissingPackagesAsync()
        {
            var solutionDirectory = VsSolutionManager.SolutionDirectory;

            var packages = await PackageRestoreManager.GetPackagesInSolutionAsync(solutionDirectory, CancellationToken.None);
            if (packages.Any(p => p.IsMissing))
            {
                var packageRestoreConsent = new VisualStudio.PackageRestoreConsent(ConfigSettings);
                if (packageRestoreConsent.IsGranted)
                {
                    await TaskScheduler.Default;

                    var result = await PackageRestoreManager.RestoreMissingPackagesAsync(solutionDirectory,
                        packages,
                        this,
                        Token);

                    if (result.Restored)
                    {
                        await PackageRestoreManager.RaisePackagesMissingEventForSolutionAsync(solutionDirectory, CancellationToken.None);
                        return;
                    }
                }

                ErrorHandler.HandleException(
                    new InvalidOperationException(Resources.Cmdlet_MissingPackages),
                    terminating: true,
                    errorId: NuGetErrorId.MissingPackages,
                    category: ErrorCategory.InvalidOperation);
            }
        }

        #region Cmdlets base APIs

        /// <summary>
        /// Get the active source repository for PowerShell cmdlets, based on the source string.
        /// </summary>
        /// <param name="source">The source string specified by -Source switch.</param>
        protected void UpdateActiveSourceRepository(string source)
        {
            // If source string is not specified, get the current active package source from the host
            source = string.IsNullOrEmpty(source) ? (string)GetPropertyValueFromHost(ActivePackageSourceKey) : source;

            if (!string.IsNullOrEmpty(source))
            {
                var packageSources = _resourceRepositoryProvider?.PackageSourceProvider?.LoadPackageSources();

                // Look through all available sources (including those disabled) by matching source name and url
                var matchingSource = packageSources
                    ?.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, source) ||
                                 StringComparer.OrdinalIgnoreCase.Equals(p.Source, source))
                    .FirstOrDefault();

                if (matchingSource != null)
                {
                    ActiveSourceRepository = _resourceRepositoryProvider?.CreateRepository(matchingSource);
                }
                else
                {
                    // source should be the format of url here; otherwise it cannot resolve from name anyways.
                    ActiveSourceRepository = CreateRepositoryFromSource(source);
                }

                EnabledSourceRepositories = _resourceRepositoryProvider?.GetRepositories()
                    .Where(r => r.PackageSource.IsEnabled)
                    .ToList();
            }
        }

        /// <summary>
        /// Create a package repository from the source by trying to resolve relative paths.
        /// </summary>
        protected SourceRepository CreateRepositoryFromSource(string source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var packageSource = new Configuration.PackageSource(source);
            SourceRepository repository = _resourceRepositoryProvider.CreateRepository(packageSource);
            PSSearchResource resource = repository.GetResource<PSSearchResource>();

            // resource can be null here for relative path package source.
            if (resource == null)
            {
                Uri uri;
                // if it's not an absolute path, treat it as relative path
                if (Uri.TryCreate(source, UriKind.Relative, out uri))
                {
                    string outputPath;
                    bool? exists;
                    string errorMessage;
                    // translate relative path to absolute path
                    if (TryTranslatePSPath(source, out outputPath, out exists, out errorMessage)
                        && exists == true)
                    {
                        source = outputPath;
                        packageSource = new Configuration.PackageSource(outputPath);
                    }
                }
            }

            var sourceRepo = _resourceRepositoryProvider.CreateRepository(packageSource);
            // Right now if packageSource is invalid, CreateRepository will not throw. Instead, resource returned is null.
            PSSearchResource newResource = repository.GetResource<PSSearchResource>();
            if (newResource == null)
            {
                // Try to create Uri again to throw UriFormat exception for invalid source input.
                new Uri(source);
            }
            return sourceRepo;
        }

        /// <summary>
        /// Translate a PSPath into a System.IO.* friendly Win32 path.
        /// Does not resolve/glob wildcards.
        /// </summary>
        /// <param name="psPath">
        /// The PowerShell PSPath to translate which may reference PSDrives or have
        /// provider-qualified paths which are syntactically invalid for .NET APIs.
        /// </param>
        /// <param name="path">The translated PSPath in a format understandable to .NET APIs.</param>
        /// <param name="exists">Returns null if not tested, or a bool representing path existence.</param>
        /// <param name="errorMessage">If translation failed, contains the reason.</param>
        /// <returns>True if successfully translated, false if not.</returns>
        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "1#", Justification = "Following TryParse pattern in BCL", Target = "path")]
        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "2#", Justification = "Following TryParse pattern in BCL", Target = "exists")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "ps", Justification = "ps is a common powershell prefix")]
        protected bool TryTranslatePSPath(string psPath, out string path, out bool? exists, out string errorMessage)
        {
            return PSPathUtility.TryTranslatePSPath(SessionState, psPath, out path, out exists, out errorMessage);
        }

        /// <summary>
        /// Check if solution is open. If not, throw terminating error
        /// </summary>
        protected void CheckSolutionState()
        {
            if (!VsSolutionManager.IsSolutionOpen)
            {
                ErrorHandler.ThrowSolutionNotOpenTerminatingError();
            }

            if (!VsSolutionManager.IsSolutionAvailable)
            {
                ErrorHandler.HandleException(
                    new InvalidOperationException(VisualStudio.Strings.SolutionIsNotSaved),
                    terminating: true,
                    errorId: NuGetErrorId.UnsavedSolution,
                    category: ErrorCategory.InvalidOperation);
            }
        }

        /// <summary>
        /// Get the default NuGet Project
        /// </summary>
        /// <param name="projectName"></param>
        protected void GetNuGetProject(string projectName = null)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                Project = VsSolutionManager.DefaultNuGetProject;
                if (VsSolutionManager.IsSolutionAvailable
                    && Project == null)
                {
                    ErrorHandler.WriteProjectNotFoundError("Default", terminating: true);
                }
            }
            else
            {
                Project = VsSolutionManager.GetNuGetProject(projectName);
                if (VsSolutionManager.IsSolutionAvailable
                    && Project == null)
                {
                    ErrorHandler.WriteProjectNotFoundError(projectName, terminating: true);
                }
            }
        }

        protected IEnumerable<NuGetProject> GetNuGetProjectsByName(string[] projectNames)
        {
            List<NuGetProject> nuGetProjects = new List<NuGetProject>();
            foreach (Project project in GetProjectsByName(projectNames))
            {
                NuGetProject nuGetProject = VsSolutionManager.GetNuGetProject(project.Name);
                if (nuGetProject != null)
                {
                    nuGetProjects.Add(nuGetProject);
                }
            }
            return nuGetProjects;
        }

        /// <summary>
        /// Get default project in the type of EnvDTE.Project, to keep PowerShell scripts backward-compatbility.
        /// </summary>
        /// <returns></returns>
        protected Project GetDefaultProject()
        {
            string customUniqueName = string.Empty;
            Project defaultDTEProject = null;

            NuGetProject defaultNuGetProject = VsSolutionManager.DefaultNuGetProject;
            // Solution may be open without a project in it. Then defaultNuGetProject is null.
            if (defaultNuGetProject != null)
            {
                customUniqueName = defaultNuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);
            }

            // Get all DTE projects in the solution and compare by CustomUnique names, especially for projects under solution folders.
            IEnumerable<Project> allDTEProjects = EnvDTESolutionUtility.GetAllEnvDTEProjects(DTE);
            if (allDTEProjects != null)
            {
                defaultDTEProject = allDTEProjects.Where(p => StringComparer.OrdinalIgnoreCase.Equals(EnvDTEProjectUtility.GetCustomUniqueName(p), customUniqueName)).FirstOrDefault();
            }

            return defaultDTEProject;
        }

        /// <summary>
        /// Return all projects in the solution matching the provided names. Wildcards are supported.
        /// This method will automatically generate error records for non-wildcarded project names that
        /// are not found.
        /// </summary>
        /// <param name="projectNames">An array of project names that may or may not include wildcards.</param>
        /// <returns>Projects matching the project name(s) provided.</returns>
        protected IEnumerable<Project> GetProjectsByName(string[] projectNames)
        {
            var allValidProjectNames = GetAllValidProjectNames().ToList();
            var allDteProjects = EnvDTESolutionUtility.GetAllEnvDTEProjects(DTE);

            foreach (string projectName in projectNames)
            {
                // if ctrl+c hit, leave immediately
                if (Stopping)
                {
                    break;
                }

                // Treat every name as a wildcard; results in simpler code
                var pattern = new WildcardPattern(projectName, WildcardOptions.IgnoreCase);

                var matches = from s in allValidProjectNames
                              where pattern.IsMatch(s)
                              select VsSolutionManager.GetNuGetProject(s);

                int count = 0;
                foreach (var project in matches)
                {
                    if (project != null)
                    {
                        count++;
                        string name = project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);
                        Project dteProject = allDteProjects
                            .Where(p => StringComparer.OrdinalIgnoreCase.Equals(EnvDTEProjectUtility.GetCustomUniqueName(p), name))
                            .FirstOrDefault();
                        yield return dteProject;
                    }
                }

                // We only emit non-terminating error record if a non-wildcarded name was not found.
                // This is consistent with built-in cmdlets that support wildcarded search.
                // A search with a wildcard that returns nothing should not be considered an error.
                if ((count == 0)
                    && !WildcardPattern.ContainsWildcardCharacters(projectName))
                {
                    ErrorHandler.WriteProjectNotFoundError(projectName, terminating: false);
                }
            }
        }

        /// <summary>
        /// Return all possibly valid project names in the current solution. This includes all
        /// unique names and safe names.
        /// </summary>
        /// <returns></returns>
        protected IEnumerable<string> GetAllValidProjectNames()
        {
            var nugetProjects = VsSolutionManager.GetNuGetProjects();
            var safeNames = nugetProjects?.Select(p => VsSolutionManager.GetNuGetProjectSafeName(p));
            var uniqueNames = nugetProjects?.Select(p => NuGetProject.GetUniqueNameOrName(p));
            return uniqueNames.Concat(safeNames).Distinct();
        }

        /// <summary>
        /// Get the list of installed packages based on Filter, Skip and First parameters. Used for Get-Package.
        /// </summary>
        /// <returns></returns>
        protected static async Task<Dictionary<NuGetProject, IEnumerable<Packaging.PackageReference>>> GetInstalledPackages(IEnumerable<NuGetProject> projects,
            string filter,
            int skip,
            int take,
            CancellationToken token)
        {
            var installedPackages = new Dictionary<NuGetProject, IEnumerable<Packaging.PackageReference>>();

            foreach (var project in projects)
            {
                var packageRefs = await project.GetInstalledPackagesAsync(token);
                // Filter the results by string
                if (!string.IsNullOrEmpty(filter))
                {
                    packageRefs = packageRefs.Where(p => p.PackageIdentity.Id.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
                }

                // Skip and then take
                if (skip != 0)
                {
                    packageRefs = packageRefs.Skip(skip);
                }
                if (take != 0)
                {
                    packageRefs = packageRefs.Take(take);
                }

                installedPackages.Add(project, packageRefs);
            }

            return installedPackages;
        }

        /// <summary>
        /// Get list of packages from the remote package source. Used for Get-Package -ListAvailable.
        /// </summary>
        protected async Task<IEnumerable<PSSearchMetadata>> GetPackagesFromRemoteSourceAsync(string packageId,
            IEnumerable<string> targetFrameworks,
            bool includePrerelease,
            int skip,
            int take)
        {
            var searchfilter = new SearchFilter();
            searchfilter.IncludePrerelease = includePrerelease;
            searchfilter.SupportedFrameworks = targetFrameworks;
            searchfilter.IncludeDelisted = false;

            var packages = Enumerable.Empty<PSSearchMetadata>();

            var resource = await ActiveSourceRepository.GetResourceAsync<PSSearchResource>();

            if (resource != null)
            {
                packages = await resource.Search(packageId, searchfilter, skip, take, Token);
            }

            return packages;
        }

        /// <summary>
        /// Log preview nuget project actions on PowerShell console.
        /// </summary>
        /// <param name="actions"></param>
        [SuppressMessage("Microsoft.Globalization", "CA1303")]
        protected void PreviewNuGetPackageActions(IEnumerable<NuGetProjectAction> actions)
        {
            if (actions == null
                || !actions.Any())
            {
                Log(ProjectManagement.MessageLevel.Info, Resources.Cmdlet_NoPackageActions);
            }
            else
            {
                foreach (NuGetProjectAction action in actions)
                {
                    Log(ProjectManagement.MessageLevel.Info, action.NuGetProjectActionType + " " + action.PackageIdentity);
                }
            }
        }

        #endregion Cmdlets base APIs

        #region Processing

        protected override void BeginProcessing()
        {
            IsExecuting = true;
            if (_httpClientEvents != null)
            {
                _httpClientEvents.SendingRequest += OnSendingRequest;
            }
        }

        protected override void EndProcessing()
        {
            IsExecuting = false;
            UnsubscribeEvents();
            base.EndProcessing();
        }

        protected void UnsubscribeEvents()
        {
            if (_httpClientEvents != null)
            {
                _httpClientEvents.SendingRequest -= OnSendingRequest;
            }
        }

        protected virtual void OnSendingRequest(object sender, WebRequestEventArgs e)
        {
            //HttpUtility.SetUserAgent(e.Request, _psCommandsUserAgent.Value);
        }

        private void OnProgressAvailable(object sender, ProgressEventArgs e)
        {
            WriteProgress(ProgressActivityIds.DownloadPackageId, e.Operation, e.PercentComplete);
        }

        protected void SubscribeToProgressEvents()
        {
            if (!IsSyncMode
                && _httpClientEvents != null)
            {
                _httpClientEvents.ProgressAvailable += OnProgressAvailable;
            }
        }

        protected void UnsubscribeFromProgressEvents()
        {
            if (_httpClientEvents != null)
            {
                _httpClientEvents.ProgressAvailable -= OnProgressAvailable;
            }
        }

        private ProgressRecordCollection ProgressRecordCache
        {
            get
            {
                if (_progressRecordCache == null)
                {
                    _progressRecordCache = new ProgressRecordCollection();
                }

                return _progressRecordCache;
            }
        }

        protected object GetPropertyValueFromHost(string propertyName)
        {
            PSObject privateData = Host.PrivateData;
            var propertyInfo = privateData.Properties[propertyName];
            if (propertyInfo != null)
            {
                return propertyInfo.Value;
            }
            return null;
        }

        #endregion Processing

        #region Implementing IErrorHandler

        public void HandleError(ErrorRecord errorRecord, bool terminating)
        {
            if (terminating)
            {
                ThrowTerminatingError(errorRecord);
            }
            else
            {
                WriteError(errorRecord);
            }
        }

        public void HandleException(Exception exception, bool terminating,
            string errorId, ErrorCategory category, object target)
        {
            exception = ExceptionUtility.Unwrap(exception);

            var error = new ErrorRecord(exception, errorId, category, target);

            ErrorHandler.HandleError(error, terminating: terminating);
        }

        public void WriteProjectNotFoundError(string projectName, bool terminating)
        {
            var notFoundException =
                new ItemNotFoundException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        Resources.Cmdlet_ProjectNotFound, projectName));

            ErrorHandler.HandleError(
                new ErrorRecord(
                    notFoundException,
                    NuGetErrorId.ProjectNotFound, // This is your locale-agnostic error id.
                    ErrorCategory.ObjectNotFound,
                    projectName),
                terminating: terminating);
        }

        public void ThrowSolutionNotOpenTerminatingError()
        {
            ErrorHandler.HandleException(
                new InvalidOperationException(Resources.Cmdlet_NoSolution),
                terminating: true,
                errorId: NuGetErrorId.NoActiveSolution,
                category: ErrorCategory.InvalidOperation);
        }

        public void ThrowNoCompatibleProjectsTerminatingError()
        {
            ErrorHandler.HandleException(
                new InvalidOperationException(Resources.Cmdlet_NoCompatibleProjects),
                terminating: true,
                errorId: NuGetErrorId.NoCompatibleProjects,
                category: ErrorCategory.InvalidOperation);
        }

        #endregion Implementing IErrorHandler

        #region Logging

        [SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "This exception is passed to PowerShell. We really don't care about the type of exception here.")]
        protected void WriteError(string message)
        {
            if (!String.IsNullOrEmpty(message))
            {
                WriteError(new Exception(message));
            }
        }

        protected void WriteError(Exception exception)
        {
            ErrorHandler.HandleException(exception, terminating: false);
        }

        protected void WriteLine(string message = null)
        {
            if (Host == null)
            {
                // Host is null when running unit tests. Simply return in this case
                return;
            }

            if (message == null)
            {
                Host.UI.WriteLine();
            }
            else
            {
                Host.UI.WriteLine(message);
            }
        }

        protected void WriteProgress(int activityId, string operation, int percentComplete)
        {
            if (IsSyncMode)
            {
                // don't bother to show progress if we are in synchronous mode
                return;
            }

            ProgressRecord progressRecord;

            // retrieve the ProgressRecord object for this particular activity id from the cache.
            if (ProgressRecordCache.Contains(activityId))
            {
                progressRecord = ProgressRecordCache[activityId];
            }
            else
            {
                progressRecord = new ProgressRecord(activityId, operation, operation);
                ProgressRecordCache.Add(progressRecord);
            }

            progressRecord.CurrentOperation = operation;
            progressRecord.PercentComplete = percentComplete;

            WriteProgress(progressRecord);
        }

        /// <summary>
        /// Implement INuGetProjectContext.ResolveFileConflict()
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public FileConflictAction ResolveFileConflict(string message)
        {
            if (_overwriteAll)
            {
                return FileConflictAction.OverwriteAll;
            }

            if (_ignoreAll)
            {
                return FileConflictAction.IgnoreAll;
            }

            if (ConflictAction != null
                && ConflictAction != FileConflictAction.PromptUser)
            {
                return (FileConflictAction)ConflictAction;
            }

            var choices = new Collection<ChoiceDescription>
                {
                    new ChoiceDescription(Resources.Cmdlet_Yes, Resources.Cmdlet_FileConflictYesHelp),
                    new ChoiceDescription(Resources.Cmdlet_YesAll, Resources.Cmdlet_FileConflictYesAllHelp),
                    new ChoiceDescription(Resources.Cmdlet_No, Resources.Cmdlet_FileConflictNoHelp),
                    new ChoiceDescription(Resources.Cmdlet_NoAll, Resources.Cmdlet_FileConflictNoAllHelp)
                };

            int choice = Host.UI.PromptForChoice(Resources.Cmdlet_FileConflictTitle, message, choices, defaultChoice: 2);

            Debug.Assert(choice >= 0 && choice < 4);
            switch (choice)
            {
                case 0:
                    return FileConflictAction.Overwrite;

                case 1:
                    _overwriteAll = true;
                    return FileConflictAction.OverwriteAll;

                case 2:
                    return FileConflictAction.Ignore;

                case 3:
                    _ignoreAll = true;
                    return FileConflictAction.IgnoreAll;
            }

            return FileConflictAction.Ignore;
        }

        /// <summary>
        /// Implement INuGetProjectContext.Log(). Called by worker thread.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            string formattedMessage = String.Format(CultureInfo.CurrentCulture, message, args);
            BlockingCollection.Add(new LogMessage(level, formattedMessage));
        }

        /// <summary>
        /// LogCore that write messages to the PowerShell console via PowerShellExecution thread.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="formattedMessage"></param>
        protected virtual void LogCore(ProjectManagement.MessageLevel level, string formattedMessage)
        {
            switch (level)
            {
                case ProjectManagement.MessageLevel.Debug:
                    WriteVerbose(formattedMessage);
                    break;

                case ProjectManagement.MessageLevel.Warning:
                    WriteWarning(formattedMessage);
                    break;

                case ProjectManagement.MessageLevel.Info:
                    WriteLine(formattedMessage);
                    break;

                case ProjectManagement.MessageLevel.Error:
                    WriteError(formattedMessage);
                    break;
            }
        }

        /// <summary>
        /// Wait for package actions and log messages.
        /// </summary>
        protected void WaitAndLogPackageActions()
        {
            try
            {
                while (true)
                {
                    var message = BlockingCollection.Take();
                    if (message is ExecutionCompleteMessage)
                    {
                        break;
                    }

                    var scriptMessage = message as ScriptMessage;
                    if (scriptMessage != null)
                    {
                        ExecutePSScriptInternal(scriptMessage.ScriptPath);
                        continue;
                    }

                    var logMessage = message as LogMessage;
                    if (logMessage != null)
                    {
                        LogCore(logMessage.Level, logMessage.Content);
                        continue;
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                LogCore(ProjectManagement.MessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Execute PowerShell script internally by PowerShell execution thread.
        /// </summary>
        /// <param name="path"></param>
        [SuppressMessage("Microsoft.Design", "CA1031")]
        public void ExecutePSScriptInternal(string path)
        {
            try
            {
                if (path != null)
                {
                    string command = "& " + ProjectManagement.PathUtility.EscapePSPath(path) + " $__rootPath $__toolsPath $__package $__project";
                    LogCore(ProjectManagement.MessageLevel.Info, String.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_ExecutingScript, path));

                    InvokeCommand.InvokeScript(command, false, PipelineResultTypes.Error, null, null);
                }

                // clear temp variables
                SessionState.PSVariable.Remove("__rootPath");
                SessionState.PSVariable.Remove("__toolsPath");
                SessionState.PSVariable.Remove("__package");
                SessionState.PSVariable.Remove("__project");
            }
            catch (Exception ex)
            {
                _scriptException = ex;
            }
            finally
            {
                ScriptEndSemaphore.Release();
            }
        }

        protected BlockingCollection<Message> BlockingCollection => _blockingCollection;

        protected Semaphore ScriptEndSemaphore => _scriptEndSemaphore;

        #endregion Logging

        public bool IsExecuting { get; private set; }

        public PSCmdlet CurrentPSCmdlet
        {
            get { return this; }
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public void ExecutePSScript(string scriptPath, bool throwOnFailure)
        {
            BlockingCollection.Add(new ScriptMessage(scriptPath));

            WaitHandle.WaitAny(new WaitHandle[] { ScriptEndSemaphore });

            if (_scriptException != null)
            {
                // Re-throw the exception so that Package Manager rolls back the action
                if (throwOnFailure)
                {
                    throw _scriptException;
                }
                Log(ProjectManagement.MessageLevel.Warning, _scriptException.Message);
            }
        }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ExecutionContext ExecutionContext { get; protected set; }

        public void ReportError(string message)
        {
            // no-op
        }
    }

    public class ProgressRecordCollection : KeyedCollection<int, ProgressRecord>
    {
        protected override int GetKeyForItem(ProgressRecord item)
        {
            return item.ActivityId;
        }
    }

    public interface IPSNuGetProjectContext : INuGetProjectContext
    {
        bool IsExecuting { get; }

        PSCmdlet CurrentPSCmdlet { get; }

        void ExecutePSScript(string scriptPath, bool throwOnFailure);
    }
}
