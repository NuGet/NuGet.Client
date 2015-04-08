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
using EnvDTE;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Resolver;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This command process the specified package against the specified project.
    /// </summary>
    public abstract class NuGetPowerShellBaseCommand : PSCmdlet, IPSNuGetProjectContext, IErrorHandler
    {
        #region Members
        private PackageManagementContext _packageManagementContext;
        private ISourceRepositoryProvider _resourceRepositoryProvider;
        private ISolutionManager _solutionManager;
        private ISettings _settings;
        private ICommonOperations _commonOperations;
        private DTE _dte;
        // TODO: Hook up DownloadResource.Progress event
        private readonly IHttpClientEvents _httpClientEvents;
        private ProgressRecordCollection _progressRecordCache;
        private Exception _scriptException;
        private bool _overwriteAll, _ignoreAll;
        internal const string PowerConsoleHostName = "Package Manager Host";
        internal const string ActivePackageSourceKey = "activePackageSource";
        internal const string SyncModeKey = "IsSyncMode";
        internal const string PackageManagementContextKey = "PackageManagementContext";
        internal const string DTEKey = "DTE";
        #endregion

        public NuGetPowerShellBaseCommand()
        {
        }

        #region Properties
        /// <summary>
        /// NuGet Package Manager for PowerShell Cmdlets
        /// </summary>
        protected NuGetPackageManager PackageManager
        {
            get
            {
                return new NuGetPackageManager(_resourceRepositoryProvider, _settings, _solutionManager);
            }
        }

        /// <summary>
        /// Vs Solution Manager for PowerShell Cmdlets
        /// </summary>
        protected ISolutionManager VsSolutionManager
        {
            get
            {
                return _solutionManager;
            }
        }

        /// <summary>
        /// Package Source Provider
        /// </summary>
        protected PackageSourceProvider PackageSourceProvider
        {
            get
            {
                return new PackageSourceProvider(_settings);
            }
        }

        /// <summary>
        /// Active Source Repository for PowerShell Cmdlets
        /// </summary>
        protected SourceRepository ActiveSourceRepository { get; set; }

        /// <summary>
        /// Settings read from the config files
        /// </summary>
        protected ISettings ConfigSettings
        {
            get
            {
                return _settings;
            }
        }

        /// <summary>
        /// DTE instance for PowerShell Cmdlets
        /// </summary>
        protected DTE DTE
        {
            get
            {
                return _dte;
            }
        }

        /// <summary>
        /// NuGet Project
        /// </summary>
        protected NuGetProject Project { get; set; }

        /// <summary>
        /// File conflict action property
        /// </summary>
        protected FileConflictAction? ConflictAction { get; set; }

        /// <summary>
        /// Determine if current PowerShell host is sync or async
        /// </summary>
        internal bool IsSyncMode
        {
            get
            {
                if (Host == null || Host.PrivateData == null)
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
            get
            {
                return this;
            }
        }
        #endregion

        internal void Execute()
        {
            BeginProcessing();
            ProcessRecord();
            EndProcessing();
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to display friendly message to the console.")]
        protected sealed override void ProcessRecord()
        {
            try
            {
                ProcessRecordCore();
            }
            catch (Exception ex)
            {
                // unhandled exceptions should be terminating
                ErrorHandler.HandleException(ex, terminating: true);
            }
            finally
            {
                UnsubscribeEvents();
            }
        }

        /// <summary>
        /// Derived classess must implement this method instead of ProcessRecord(), which is sealed by NuGetPowerShellBaseCommand.
        /// </summary>
        protected abstract void ProcessRecordCore();

        /// <summary>
        /// Preprocess to get resourceRepositoryProvider and solutionManager from packageManagementContext.
        /// </summary>
        protected virtual void Preprocess()
        {
            _packageManagementContext = (PackageManagementContext)GetPropertyValueFromHost(PackageManagementContextKey);
            if (_packageManagementContext != null)
            {
                _resourceRepositoryProvider = _packageManagementContext.SourceRepositoryProvider;
                _solutionManager = _packageManagementContext.VsSolutionManager;
                _settings = _packageManagementContext.Settings;
                _commonOperations = _packageManagementContext.CommonOperations;
                if (_commonOperations != null)
                {
                    ExecutionContext = new IDEExecutionContext(_commonOperations);
                }
            }
            _dte = (DTE)GetPropertyValueFromHost(DTEKey);
        }

        #region Cmdlets base APIs
        /// <summary>
        /// Get the active source repository for PowerShell cmdlets, which is passed in by the host.
        /// </summary>
        /// <param name="source"></param>
        protected void UpdateActiveSourceRepository(string source = null)
        {
            if (string.IsNullOrEmpty(source))
            {
                source = (string)GetPropertyValueFromHost(ActivePackageSourceKey);
            }

            IEnumerable<SourceRepository> repoes = Enumerable.Empty<SourceRepository>();
            if (_resourceRepositoryProvider != null)
            {
                repoes = _resourceRepositoryProvider.GetRepositories();
            }
               
            if (!string.IsNullOrEmpty(source))
            {
                // Look through all available sources (including those disabled) by matching source name and url
                ActiveSourceRepository = repoes
                    .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageSource.Name, source) ||
                    StringComparer.OrdinalIgnoreCase.Equals(p.PackageSource.Source, source))
                    .FirstOrDefault();

                if(ActiveSourceRepository == null)
                {
                    try
                    {
                        // source should be the format of url here; otherwise it cannot resolve from name anyways.
                        ActiveSourceRepository = CreateRepositoryFromSource(source);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
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

            UriFormatException uriException = null;
            PackageSource packageSource = new PackageSource(source);
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
                    if (TryTranslatePSPath(source, out outputPath, out exists, out errorMessage) && exists == true)
                    {
                        source = outputPath;
                        packageSource = new PackageSource(outputPath);
                    }
                }
            }

            try
            {
                var sourceRepo = _resourceRepositoryProvider.CreateRepository(packageSource);
                // Right now if packageSource is invalid, CreateRepository will not throw. Instead, resource returned is null. 
                PSSearchResource newResource = repository.GetResource<PSSearchResource>();
                if (newResource == null)
                {
                    // Try to create Uri again to throw UriFormat exception for invalid source input.
                    Uri sourceUri = new Uri(source);
                }
                return sourceRepo;
            }
            catch (Exception ex)
            {
                // if this is not a valid relative path either, 
                // we rethrow the UriFormatException that we caught earlier.
                if (uriException != null)
                {
                    throw uriException;
                }
                else
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Translate a PSPath into a System.IO.* friendly Win32 path.
        /// Does not resolve/glob wildcards.
        /// </summary>                
        /// <param name="psPath">The PowerShell PSPath to translate which may reference PSDrives or have provider-qualified paths which are syntactically invalid for .NET APIs.</param>
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
        protected void CheckForSolutionOpen()
        {
            if (!_solutionManager.IsSolutionOpen)
            {
                ErrorHandler.ThrowSolutionNotOpenTerminatingError();
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
                Project = _solutionManager.DefaultNuGetProject;
                if (_solutionManager.IsSolutionOpen && Project == null)
                {
                    ErrorHandler.WriteProjectNotFoundError("Default", terminating: true);
                }
            }
            else
            {
                Project = _solutionManager.GetNuGetProject(projectName);
                if (_solutionManager.IsSolutionOpen && Project == null)
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
                NuGetProject nuGetProject = _solutionManager.GetNuGetProject(project.Name);
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

            NuGetProject defaultNuGetProject = _solutionManager.DefaultNuGetProject;
            // Solution may be open without a project in it. Then defaultNuGetProject is null.
            if (defaultNuGetProject != null)
            {
                customUniqueName = defaultNuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);
            }

            // Get all DTE projects in the solution and compare by CustomUnique names, especially for projects under solution folders.
            IEnumerable<Project> allDTEProjects = DTE.Solution.GetAllProjects();
            if (allDTEProjects != null)
            {
                defaultDTEProject = allDTEProjects.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.GetCustomUniqueName(), customUniqueName)).FirstOrDefault();
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
            var allDteProjects = _dte.Solution.GetAllProjects();

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
                              select _solutionManager.GetNuGetProject(s);

                int count = 0;
                foreach (var project in matches)
                {
                    if (project != null)
                    {
                        count++;
                        string name = project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);
                        Project dteProject = allDteProjects
                                .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.GetCustomUniqueName(), name))
                                .FirstOrDefault();
                        yield return dteProject;
                    }
                }

                // We only emit non-terminating error record if a non-wildcarded name was not found.
                // This is consistent with built-in cmdlets that support wildcarded search.
                // A search with a wildcard that returns nothing should not be considered an error.
                if ((count == 0) && !WildcardPattern.ContainsWildcardCharacters(projectName))
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
            var safeNames = _dte.Solution.GetAllProjects().Select(p => p.GetProjectSafeName(_dte));
            var uniqueNames = _dte.Solution.GetAllProjects().Select(p => p.GetCustomUniqueName());
            return uniqueNames.Concat(safeNames).Distinct();
        }

        /// <summary>
        /// Get the list of installed packages based on Filter, Skip and First parameters. Used for Get-Package.
        /// </summary>
        /// <returns></returns>
        protected async Task<Dictionary<NuGetProject, IEnumerable<PackageReference>>> GetInstalledPackages(IEnumerable<NuGetProject> projects, 
            string filter, int skip, int take)
        {
            Dictionary<NuGetProject, IEnumerable<PackageReference>> installedPackages = new Dictionary<NuGetProject, IEnumerable<PackageReference>>();

            foreach (NuGetProject project in projects)
            {
                IEnumerable<PackageReference> packageRefs = await project.GetInstalledPackagesAsync(CancellationToken.None);
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
        /// <param name="packageId"></param>
        /// <param name="targetFrameworks"></param>
        /// <param name="includePrerelease"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        protected IEnumerable<PSSearchMetadata> GetPackagesFromRemoteSource(string packageId, IEnumerable<string> targetFrameworks, 
            bool includePrerelease, int skip, int take)
        {
            SearchFilter searchfilter = new SearchFilter();
            searchfilter.IncludePrerelease = includePrerelease;
            searchfilter.SupportedFrameworks = targetFrameworks;
            searchfilter.IncludeDelisted = false;

            IEnumerable<PSSearchMetadata> packages = Enumerable.Empty<PSSearchMetadata>();
            PSSearchResource resource = ActiveSourceRepository.GetResource<PSSearchResource>();
            if (resource != null)
            {
                Task<IEnumerable<PSSearchMetadata>> task = resource.Search(packageId, searchfilter, skip, take, CancellationToken.None);
                packages = task.Result;
            }
            return packages;
        }

        /// <summary>
        /// Get list of package updates that are installed to a project. Used for Get-Package -Updates.
        /// </summary>
        /// <param name="installedPackages"></param>
        /// <param name="targetFrameworks"></param>
        /// <param name="includePrerelease"></param>
        /// <param name="skip"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        protected Dictionary<PSSearchMetadata, NuGetVersion> GetPackageUpdatesFromRemoteSource(IEnumerable<PackageReference> installedPackages,
            IEnumerable<string> targetFrameworks, bool includePrerelease, int skip = 0, int take = 30)
        {
            Dictionary<PSSearchMetadata, NuGetVersion> updates = new Dictionary<PSSearchMetadata, NuGetVersion>();

            foreach (PackageReference package in installedPackages)
            {
                PSSearchMetadata metadata = GetPackagesFromRemoteSource(package.PackageIdentity.Id, targetFrameworks, includePrerelease, skip, take)
                    .Where(p => string.Equals(p.Identity.Id, package.PackageIdentity.Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (metadata != null)
                {
                    updates.Add(metadata, package.PackageIdentity.Version);
                }
            }

            return updates;
        }

        /// <summary>
        /// Get update identity for a package that is installed to a project. Used for Update-Package Id -Version.
        /// </summary>
        /// <param name="installedPackage"></param>
        /// <param name="project"></param>
        /// <param name="includePrerelease"></param>
        /// <param name="isSafe"></param>
        /// <param name="version"></param>
        /// <param name="isEnum"></param>
        /// <param name="dependencyEnum"></param>
        /// <returns></returns>
        protected PackageIdentity GetPackageUpdate(PackageReference installedPackage, NuGetProject project,
            bool includePrerelease, bool isSafe, string version = null, bool isEnum = false, DependencyBehavior dependencyEnum = DependencyBehavior.Lowest)
        {
            PackageIdentity identity = null;
            if (isSafe)
            {
                identity = PowerShellCmdletsUtility.GetSafeUpdateForPackageIdentity(ActiveSourceRepository, installedPackage.PackageIdentity, project, includePrerelease, installedPackage.PackageIdentity.Version);
            }
            else if (isEnum)
            {
                identity = PowerShellCmdletsUtility.GetUpdateForPackageByDependencyEnum(ActiveSourceRepository, installedPackage.PackageIdentity, project, dependencyEnum, includePrerelease);
            }
            else
            {
                NuGetVersion nVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(version);
                identity = new PackageIdentity(installedPackage.PackageIdentity.Id, nVersion);
            }

            // Since package downgrade is allowed, we will not check nVersion > installedPackage.PackageIdentity.Version here.
            return identity;
        }

        /// <summary>
        /// Log preview nuget project actions on PowerShell console.
        /// </summary>
        /// <param name="actions"></param>
        protected void PreviewNuGetPackageActions(IEnumerable<NuGetProjectAction> actions)
        {
            if (actions == null || !actions.Any())
            {
                Log(MessageLevel.Info, Resources.Cmdlet_NoPackageActions);
            }
            else
            {
                foreach (NuGetProjectAction action in actions)
                {
                    Log(MessageLevel.Info, string.Format("{0} {1}", action.NuGetProjectActionType, action.PackageIdentity));
                }
            }
        }
        #endregion

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
            if (!IsSyncMode && _httpClientEvents != null)
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
        #endregion

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
        #endregion

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

            if (ConflictAction != null && ConflictAction != FileConflictAction.PromptUser)
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

            int choice = Host.UI.PromptForChoice(Resources.FileConflictTitle, message, choices, defaultChoice: 2);

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
        public void Log(MessageLevel level, string message, params object[] args)
        {
            string formattedMessage = String.Format(CultureInfo.CurrentCulture, message, args);
            blockingCollection.Add(new LogMessage(level, formattedMessage));
        }

        /// <summary>
        /// LogCore that write messages to the PowerShell console via PowerShellExecution thread.
        /// </summary>
        /// <param name="level"></param>
        /// <param name="formattedMessage"></param>
        protected virtual void LogCore(MessageLevel level, string formattedMessage)
        {
            switch (level)
            {
                case MessageLevel.Debug:
                    WriteVerbose(formattedMessage);
                    break;

                case MessageLevel.Warning:
                    WriteWarning(formattedMessage);
                    break;

                case MessageLevel.Info:
                    WriteLine(formattedMessage);
                    break;

                case MessageLevel.Error:
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
                    var message = blockingCollection.Take();
                    if (message is ExecutionCompleteMessage)
                    {
                        break;
                    }
                    else if (message is ScriptMessage)
                    {
                        ScriptMessage scriptMessage = message as ScriptMessage;
                        ExecutePSScriptInternal(scriptMessage.ScriptPath);
                    }
                    else if (message is LogMessage)
                    {
                        LogMessage logMessage = message as LogMessage;
                        LogCore(logMessage.Level, logMessage.Content); 
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                LogCore(MessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Execute PowerShell script internally by PowerShell execution thread.
        /// </summary>
        /// <param name="path"></param>
        public void ExecutePSScriptInternal(string path)
        {
            try
            {
                if (path != null)
                {
                    string command = "& " + PathUtility.EscapePSPath(path) + " $__rootPath $__toolsPath $__package $__project";
                    LogCore(MessageLevel.Info, String.Format(CultureInfo.CurrentCulture, Resources.ExecutingScript, path));

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
                scriptEndSemaphore.Release();
            }
        }

        protected BlockingCollection<Message> blockingCollection = new BlockingCollection<Message>();

        protected Semaphore scriptEndSemaphore = new Semaphore(0, Int32.MaxValue);
        #endregion

        public bool IsExecuting
        {
            get;
            private set;
        }

        public PSCmdlet CurrentPSCmdlet
        {
            get { return this; }
        }

        public PackageExtractionContext PackageExtractionContext
        {
            get;
            set;
        }

        public void ExecutePSScript(string scriptPath)
        {
            blockingCollection.Add(new ScriptMessage(scriptPath));

            WaitHandle.WaitAny(new WaitHandle[] { scriptEndSemaphore });

            if (_scriptException != null)
            {
                throw _scriptException;
            }
        }


        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return _packageManagementContext.SourceControlManagerProvider; }
        }


        public ProjectManagement.ExecutionContext ExecutionContext
        {
            get;
            protected set;
        }

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

        void ExecutePSScript(string scriptPath);
    }
}
