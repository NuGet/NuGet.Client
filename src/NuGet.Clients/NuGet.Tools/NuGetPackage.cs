// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Options;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation;
using ISettings = NuGet.Configuration.ISettings;
using Resx = NuGet.PackageManagement.UI.Resources;
using Task = System.Threading.Tasks.Task;
using UI = NuGet.PackageManagement.UI;

namespace NuGetVSExtension
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", ProductVersion, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(PowerConsoleToolWindow),
        Style = VsDockStyle.Tabbed,
        Window = "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}", // this is the guid of the Output tool window, which is present in both VS and VWD
        Orientation = ToolWindowOrientation.Right)]
    [ProvideOptionPage(typeof(PackageSourceOptionsPage), "NuGet Package Manager", "Package Sources", 113, 114, true)]
    [ProvideOptionPage(typeof(GeneralOptionPage), "NuGet Package Manager", "General", 113, 115, true)]
    [ProvideSearchProvider(typeof(NuGetSearchProvider), "NuGet Search")]
    [ProvideBindingPath] // Definition dll needs to be on VS binding path
    // UI Context rule for a project that could be upgraded to PackageReference from packages.config based project.
    // Only exception is this UI context doesn't get enabled for right-click on Reference since there is no extension point on references
    // to know if there is packages.config file in this project hierarchy. So first-time right click on reference in a new VS instance
    // will not show Migrator option.
    [ProvideUIContextRule(GuidList.guidUpgradeableProjectLoadedString,
        "UpgradeableProjectLoaded",
        "SolutionExistsAndFullyLoaded & PackagesConfigBasedProjectLoaded",
        new[] { "SolutionExistsAndFullyLoaded", "PackagesConfigBasedProjectLoaded" },
        new[] { VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string,
            "HierSingleSelectionName:packages.config"})]
    [ProvideAutoLoad(GuidList.guidUpgradeableProjectLoadedString, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(GuidList.guidAutoLoadNuGetString, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ProjectRetargeting_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOrProjectUpgrading_string, PackageAutoLoadFlags.BackgroundLoad)]
    [FontAndColorsRegistration(
        "Package Manager Console",
        NuGetConsole.GuidList.GuidPackageManagerConsoleFontAndColorCategoryString,
        "{" + GuidList.guidNuGetPkgString + "}")]
    [Guid(GuidList.guidNuGetPkgString)]
    public sealed class NuGetPackage : AsyncPackage, IVsPackageExtensionProvider, IVsPersistSolutionOpts
    {
        // It is displayed in the Help - About box of Visual Studio
        public const string ProductVersion = "4.9.7";
        private const string F1KeywordValuePmUI = "VS.NuGet.PackageManager.UI";
        private static readonly object _credentialsPromptLock = new object();
        private readonly HashSet<Uri> _credentialRequested = new HashSet<Uri>();

        private AsyncLazy<IVsMonitorSelection> _vsMonitorSelection;
        private IVsMonitorSelection VsMonitorSelection => ThreadHelper.JoinableTaskFactory.Run(_vsMonitorSelection.GetValueAsync);

        private DTE _dte;
        private DTEEvents _dteEvents;
        private OleMenuCommand _managePackageDialogCommand;
        private OleMenuCommand _managePackageForSolutionDialogCommand;
        private OleMenuCommandService _mcs;

        private uint _solutionExistsAndFullyLoadedContextCookie;
        private uint _solutionNotBuildingAndNotDebuggingContextCookie;
        private uint _solutionExistsCookie;
        private bool _powerConsoleCommandExecuting;
        private bool _initialized;

        public NuGetPackage()
        {
#if VS14
            NuGet.Common.RuntimeEnvironmentHelper.IsDev14 = true;
#endif
            ServiceLocator.InitializePackageServiceProvider(this);
        }

        [Import]
        private Lazy<IConsoleStatus> ConsoleStatus { get; set; }

        [Import]
        private Lazy<IDeleteOnRestartManager> DeleteOnRestartManager { get; set; }

        [Import]
        private Lazy<INuGetUILogger> OutputConsoleLogger { get; set; }

        [Import]
        private Lazy<INuGetProjectContext> ProjectContext { get; set; }

        [Import]
        private Lazy<ISettings> Settings { get; set; }

        [Import]
        private Lazy<IVsSolutionManager> SolutionManager { get; set; }

        [Import]
        private Lazy<SolutionUserOptions> SolutionUserOptions { get; set; }

        /// <summary>
        /// This initializes the IVSSourceControlTracker, even though SourceControlTracker is unused.
        /// </summary>
        [Import]
        private Lazy<IVsSourceControlTracker> SourceControlTracker { get; set; }

        [Import]
        private Lazy<INuGetUIFactory> UIFactory { get; set; }

        private IDisposable ProjectRetargetingHandler { get; set; }

        private IDisposable ProjectUpgradeHandler { get; set; }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // Add our command handlers for menu (commands must exist in the .vsct file)
            await AddMenuCommandHandlersAsync();

            // This instantiates a decoupled ICommand instance responsible to locate and display output pane by a UI control
            UI.Commands.ShowErrorsCommand = new ShowErrorsCommand(this);

            _vsMonitorSelection = new AsyncLazy<IVsMonitorSelection>(
                async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // get the UI context cookie for the debugging mode
                    var vsMonitorSelection = await GetServiceAsync(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

                    // get the solution not building and not debugging cookie
                    var guidCmdUI = VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_guid;
                    vsMonitorSelection.GetCmdUIContextCookie(
                        ref guidCmdUI, out _solutionExistsAndFullyLoadedContextCookie);

                    guidCmdUI = VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid;
                    vsMonitorSelection.GetCmdUIContextCookie(
                        ref guidCmdUI, out _solutionNotBuildingAndNotDebuggingContextCookie);

                    guidCmdUI = VSConstants.UICONTEXT.SolutionExists_guid;
                    vsMonitorSelection.GetCmdUIContextCookie(
                        ref guidCmdUI, out _solutionExistsCookie);

                    return vsMonitorSelection;
                },
                ThreadHelper.JoinableTaskFactory);
        }

        /// <summary>
        /// Initialize all MEF imports for this package and also add required event handlers.
        /// </summary>
        private async Task InitializeMEFAsync()
        {
            _initialized = true;

            var componentModel = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            componentModel.DefaultCompositionService.SatisfyImportsOnce(this);

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            SolutionManager.Value.AfterNuGetProjectRenamed += SolutionManager_NuGetProjectRenamed;

            Brushes.LoadVsBrushes();

            _dte = (DTE)await GetServiceAsync(typeof(SDTE));

            _dteEvents = _dte.Events.DTEEvents;
            _dteEvents.OnBeginShutdown += OnBeginShutDown;

            if (SolutionManager.Value.NuGetProjectContext == null)
            {
                SolutionManager.Value.NuGetProjectContext = ProjectContext.Value;
            }

            // when NuGet loads, if the current solution has some package
            // folders marked for deletion (because a previous uninstalltion didn't succeed),
            // delete them now.
            if (SolutionManager.Value.IsSolutionOpen)
            {
                DeleteOnRestartManager.Value.DeleteMarkedPackageDirectories(ProjectContext.Value);
            }

            ProjectRetargetingHandler = new ProjectRetargetingHandler(_dte, SolutionManager.Value, this, componentModel);
            ProjectUpgradeHandler = new ProjectUpgradeHandler(this, SolutionManager.Value);

            SolutionUserOptions.Value.LoadSettings();
        }

        private void SolutionManager_NuGetProjectRenamed(object sender, NuGetProjectEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var project = await SolutionManager.Value.GetVsProjectAdapterAsync(
                    await SolutionManager.Value.GetNuGetProjectSafeNameAsync(e.NuGetProject));
                var windowFrame = FindExistingWindowFrame(project.Project);
                if (windowFrame != null)
                {
                    windowFrame.SetProperty((int)__VSFPROPID.VSFPROPID_OwnerCaption, string.Format(
                        CultureInfo.CurrentCulture,
                        Resx.Label_NuGetWindowCaption,
                        project.ProjectName));
                }
            });
        }

        private async Task AddMenuCommandHandlersAsync()
        {
            _mcs = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != _mcs)
            {
                // Switch to Main Thread before calling AddCommand which calls GetService() which should
                // always be called on UI thread.
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // menu command for upgrading packages.config files to PackageReference - References context menu
                var upgradeNuGetProjectCommandID = new CommandID(GuidList.guidNuGetDialogCmdSet, PkgCmdIDList.cmdidUpgradeNuGetProject);
                var upgradeNuGetProjectCommand = new OleMenuCommand(ExecuteUpgradeNuGetProjectCommandAsync, null,
                    BeforeQueryStatusForUpgradeNuGetProject, upgradeNuGetProjectCommandID);
                _mcs.AddCommand(upgradeNuGetProjectCommand);

                // menu command for upgrading packages.config files to PackageReference - packages.config context menu
                var upgradePackagesConfigCommandID = new CommandID(GuidList.guidNuGetDialogCmdSet, PkgCmdIDList.cmdidUpgradePackagesConfig);
                var upgradePackagesConfigCommand = new OleMenuCommand(ExecuteUpgradeNuGetProjectCommandAsync, null,
                    BeforeQueryStatusForUpgradePackagesConfig, upgradePackagesConfigCommandID);
                _mcs.AddCommand(upgradePackagesConfigCommand);

                // menu command for opening Package Manager Console
                var toolwndCommandID = new CommandID(GuidList.guidNuGetConsoleCmdSet, PkgCmdIDList.cmdidPowerConsole);
                var powerConsoleExecuteCommand = new OleMenuCommand(ExecutePowerConsoleCommand, null, BeforeQueryStatusForPowerConsole, toolwndCommandID);
                // '$' - This indicates that the input line other than the argument forms a single argument string with no autocompletion
                //       Autocompletion for filename(s) is supported for option 'p' or 'd' which is not applicable for this command
                powerConsoleExecuteCommand.ParametersDescription = "$";
                _mcs.AddCommand(powerConsoleExecuteCommand);

                // menu command for opening NuGet Debug Console
                // Remove debug console from Tools menu for 3.0.0-beta.
                //CommandID debugWndCommandID = new CommandID(GuidList.guidNuGetDebugConsoleCmdSet, PkgCmdIDList.cmdidDebugConsole);
                //OleMenuCommand debugConsoleExecuteCommand = new OleMenuCommand(ShowDebugConsole, null, null, debugWndCommandID);
                // '$' - This indicates that the input line other than the argument forms a single argument string with no autocompletion
                //       Autocompletion for filename(s) is supported for option 'p' or 'd' which is not applicable for this command
                //debugConsoleExecuteCommand.ParametersDescription = "$";
                //_mcs.AddCommand(debugConsoleExecuteCommand);

                // menu command for opening Manage NuGet packages dialog
                var managePackageDialogCommandID = new CommandID(GuidList.guidNuGetDialogCmdSet, PkgCmdIDList.cmdidAddPackageDialog);
                _managePackageDialogCommand = new OleMenuCommand(ShowManageLibraryPackageDialog, null, BeforeQueryStatusForAddPackageDialog, managePackageDialogCommandID);
                // '$' - This indicates that the input line other than the argument forms a single argument string with no autocompletion
                //       Autocompletion for filename(s) is supported for option 'p' or 'd' which is not applicable for this command
                _managePackageDialogCommand.ParametersDescription = "$";
                _mcs.AddCommand(_managePackageDialogCommand);

                // menu command for opening "Manage NuGet packages for solution" dialog
                var managePackageForSolutionDialogCommandID = new CommandID(GuidList.guidNuGetDialogCmdSet, PkgCmdIDList.cmdidAddPackageDialogForSolution);
                _managePackageForSolutionDialogCommand = new OleMenuCommand(ShowManageLibraryPackageForSolutionDialog, null, BeforeQueryStatusForAddPackageForSolutionDialog, managePackageForSolutionDialogCommandID);
                // '$' - This indicates that the input line other than the argument forms a single argument string with no autocompletion
                //       Autocompletion for filename(s) is supported for option 'p' or 'd' which is not applicable for this command
                _managePackageForSolutionDialogCommand.ParametersDescription = "$";
                _mcs.AddCommand(_managePackageForSolutionDialogCommand);

                // menu command for opening Package Source settings options page
                var settingsCommandID = new CommandID(GuidList.guidNuGetConsoleCmdSet, PkgCmdIDList.cmdidSourceSettings);
                var settingsMenuCommand = new OleMenuCommand(ShowPackageSourcesOptionPage, settingsCommandID);
                _mcs.AddCommand(settingsMenuCommand);

                // menu command for opening General options page
                var generalSettingsCommandID = new CommandID(GuidList.guidNuGetToolsGroupCmdSet, PkgCmdIDList.cmdIdGeneralSettings);
                var generalSettingsCommand = new OleMenuCommand(ShowGeneralSettingsOptionPage, generalSettingsCommandID);
                _mcs.AddCommand(generalSettingsCommand);
            }
        }

        private void ExecutePowerConsoleCommand(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ShouldMEFBeInitialized())
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(InitializeMEFAsync);
            }

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            var window = FindToolWindow(typeof(PowerConsoleToolWindow), 0, true);
            if ((null == window)
                || (null == window.Frame))
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }
            var windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());

            // Parse the arguments to determine the command and arguments to be passed to IHost
            // passed which is of type OleMenuCmdEventArgs
            string command = null;
            var eventArgs = e as OleMenuCmdEventArgs;
            if (eventArgs != null
                && eventArgs.InValue != null)
            {
                command = eventArgs.InValue as string;
            }

            // If the command string is null or empty, simply launch the console and return
            if (!string.IsNullOrEmpty(command))
            {
                var powerConsoleService = (IPowerConsoleService)window;

                if (powerConsoleService.Execute(command, null))
                {
                    _powerConsoleCommandExecuting = true;
                    powerConsoleService.ExecuteEnd += PowerConsoleService_ExecuteEnd;
                }
            }
        }

        private void PowerConsoleService_ExecuteEnd(object sender, EventArgs e)
        {
            _powerConsoleCommandExecuting = false;
        }

        private IVsWindowFrame FindExistingWindowFrame(
            Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                object docView;
                var hr = windowFrame.GetProperty(
                    (int)__VSFPROPID.VSFPROPID_DocView,
                    out docView);
                if (hr == VSConstants.S_OK
                    && docView is PackageManagerWindowPane)
                {
                    var packageManagerWindowPane = (PackageManagerWindowPane)docView;
                    if (packageManagerWindowPane.Model.IsSolution)
                    {
                        // the window is the solution package manager
                        continue;
                    }

                    var projects = packageManagerWindowPane.Model.Context.Projects;
                    if (projects.Count() != 1)
                    {
                        continue;
                    }

                    var existingProject = projects.First();
                    var projectName = existingProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                    if (string.Equals(projectName, project.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return windowFrame;
                    }
                }
            }

            return null;
        }

        private async Task<IVsWindowFrame> CreateNewWindowFrameAsync(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsProject = VsHierarchyUtility.ToVsHierarchy(project);
            var documentName = project.UniqueName;

            // Find existing hierarchy and item id of the document window if it's
            // already registered.
            var rdt = await GetServiceAsync(typeof(IVsRunningDocumentTable)) as IVsRunningDocumentTable;
            IVsHierarchy hier;
            uint itemId;
            var docData = IntPtr.Zero;
            int hr;

            try
            {
                uint cookie;
                hr = rdt.FindAndLockDocument(
                    (uint)_VSRDTFLAGS.RDT_NoLock,
                    documentName,
                    out hier,
                    out itemId,
                    out docData,
                    out cookie);
                if (hr != VSConstants.S_OK)
                {
                    // the docuemnt window is not registered yet. So use the project as the
                    // hierarchy.
                    hier = vsProject;
                    itemId = (uint)VSConstants.VSITEMID.Root;
                }
            }
            finally
            {
                if (docData != IntPtr.Zero)
                {
                    Marshal.Release(docData);
                    docData = IntPtr.Zero;
                }
            }

            // Create the doc window using the hiearchy & item id.
            return await CreateDocWindowAsync(project, documentName, hier, itemId);
        }

        private async Task<IVsWindowFrame> CreateDocWindowAsync(
            Project project,
            string documentName,
            IVsHierarchy hier,
            uint itemId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var windowFlags =
                (uint)_VSRDTFLAGS.RDT_DontAddToMRU |
                (uint)_VSRDTFLAGS.RDT_DontSaveAs;

            if (!await SolutionManager.Value.IsSolutionAvailableAsync())
            {
                throw new InvalidOperationException(Resources.SolutionIsNotSaved);
            }

            var uniqueName = EnvDTEProjectInfoUtility.GetUniqueName(project);
            var nugetProject = await SolutionManager.Value.GetNuGetProjectAsync(uniqueName);

            // If we failed to generate a cache entry in the solution manager something went wrong.
            if (nugetProject == null)
            {
                throw new InvalidOperationException(
                    string.Format(Resources.ProjectHasAnInvalidNuGetConfiguration, project.Name));
            }

            // load packages.config. This makes sure that an exception will get thrown if there
            // are problems with packages.config, such as duplicate packages. When an exception
            // is thrown, an error dialog will pop up and this doc window will not be created.
            var installedPackages = await nugetProject.GetInstalledPackagesAsync(CancellationToken.None);

            var uiController = UIFactory.Value.Create(nugetProject);

            var model = new PackageManagerModel(
                uiController,
                isSolution: false,
                editorFactoryGuid: GuidList.guidNuGetEditorType);

            var vsWindowSearchHostfactory = await GetServiceAsync(typeof(SVsWindowSearchHostFactory)) as IVsWindowSearchHostFactory;
            var vsShell = await GetServiceAsync(typeof(SVsShell)) as IVsShell4;
            var control = new PackageManagerControl(model, Settings.Value, vsWindowSearchHostfactory, vsShell, OutputConsoleLogger.Value);

            var windowPane = new PackageManagerWindowPane(control);
            var guidEditorType = GuidList.guidNuGetEditorType;
            var guidCommandUI = Guid.Empty;
            var caption = string.Format(
                CultureInfo.CurrentCulture,
                Resx.Label_NuGetWindowCaption,
                project.Name);

            IVsWindowFrame windowFrame;
            var uiShell = await GetServiceAsync(typeof(SVsUIShell)) as IVsUIShell;

            var ppunkDocView = IntPtr.Zero;
            var ppunkDocData = IntPtr.Zero;
            var hr = 0;

            try
            {
                ppunkDocView = Marshal.GetIUnknownForObject(windowPane);
                ppunkDocData = Marshal.GetIUnknownForObject(model);
                hr = uiShell.CreateDocumentWindow(
                    windowFlags,
                    documentName,
                    (IVsUIHierarchy)hier,
                    itemId,
                    ppunkDocView,
                    ppunkDocData,
                    ref guidEditorType,
                    null,
                    ref guidCommandUI,
                    null,
                    caption,
                    string.Empty,
                    null,
                    out windowFrame);
                if (windowFrame != null)
                {
                    WindowFrameHelper.AddF1HelpKeyword(windowFrame, keywordValue: F1KeywordValuePmUI);
                }
            }
            finally
            {
                if (ppunkDocView != IntPtr.Zero)
                {
                    Marshal.Release(ppunkDocData);
                }

                if (ppunkDocData != IntPtr.Zero)
                {
                    Marshal.Release(ppunkDocView);
                }
            }

            ErrorHandler.ThrowOnFailure(hr);
            return windowFrame;
        }

        private async void ExecuteUpgradeNuGetProjectCommandAsync(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ShouldMEFBeInitialized())
            {
                await InitializeMEFAsync();
            }

            var project = EnvDTEProjectInfoUtility.GetActiveProject(VsMonitorSelection);

            if (!await NuGetProjectUpgradeUtility.IsNuGetProjectUpgradeableAsync(null, project))
            {
                MessageHelper.ShowWarningMessage(Resources.ProjectMigrateErrorMessage, Resources.ErrorDialogBoxTitle);
                return;
            }

            var uniqueName = await EnvDTEProjectInfoUtility.GetCustomUniqueNameAsync(project);
            // Close NuGet Package Manager if it is open for this project
            var windowFrame = FindExistingWindowFrame(project);
            windowFrame?.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_SaveIfDirty);

            var nuGetProject = await SolutionManager.Value.GetNuGetProjectAsync(uniqueName);
            var uiController = ServiceLocator.GetInstance<INuGetUIFactory>().Create(nuGetProject);
            var settings = uiController.UIContext.UserSettingsManager.GetSettings(GetProjectSettingsKey(nuGetProject));

            await uiController.UIContext.UIActionEngine.UpgradeNuGetProjectAsync(uiController, nuGetProject);
            uiController.UIContext.UserSettingsManager.PersistSettings();
        }

        private static string GetProjectSettingsKey(NuGetProject nuGetProject)
        {
            string projectName;
            if (!nuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.Name, out projectName))
            {
                projectName = "unknown";
            }
            return "project:" + projectName;
        }

        private void ShowManageLibraryPackageDialog(object sender, EventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                if (ShouldMEFBeInitialized())
                {
                    await InitializeMEFAsync();
                }

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                string parameterString = null;
                var args = e as OleMenuCmdEventArgs;
                if (null != args)
                {
                    parameterString = args.InValue as string;
                }
                var searchText = GetSearchText(parameterString);

                // *** temp code
                var project = EnvDTEProjectInfoUtility.GetActiveProject(VsMonitorSelection);

                if (project != null
                    &&
                    !EnvDTEProjectInfoUtility.IsUnloaded(project)
                    &&
                    EnvDTEProjectUtility.IsSupported(project))
                {
                    var windowFrame = FindExistingWindowFrame(project);
                    if (windowFrame == null)
                    {
                        windowFrame = await CreateNewWindowFrameAsync(project);
                    }

                    if (windowFrame != null)
                    {
                        Search(windowFrame, searchText);
                        windowFrame.Show();
                    }
                }
                else
                {
                    // show error message when no supported project is selected.
                    var projectName = project != null ? project.Name : string.Empty;

                    var errorMessage = string.IsNullOrEmpty(projectName)
                        ? Resources.NoProjectSelected
                        : string.Format(CultureInfo.CurrentCulture, Resources.DTE_ProjectUnsupported, projectName);

                    MessageHelper.ShowWarningMessage(errorMessage, Resources.ErrorDialogBoxTitle);
                }
            });
        }

        private async Task<IVsWindowFrame> FindExistingSolutionWindowFrameAsync()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var uiShell = await GetServiceAsync(typeof(SVsUIShell)) as IVsUIShell;
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                object property;
                var hr = windowFrame.GetProperty(
                    (int)__VSFPROPID.VSFPROPID_DocData,
                    out property);
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                if (hr == VSConstants.S_OK
                    &&
                    property is IVsSolution
                    &&
                    packageManagerControl != null)
                {
                    return windowFrame;
                }
            }

            return null;
        }

        private static string GetSearchText(string parameterString)
        {
            if (parameterString == null)
            {
                return null;
            }

            // The parameterString looks like:
            //     "jquery /searchin:online"
            // or just "jquery"

            parameterString = parameterString.Trim();
            var lastIndexOfSearchInSwitch = parameterString.LastIndexOf("/searchin:", StringComparison.OrdinalIgnoreCase);

            if (lastIndexOfSearchInSwitch == -1)
            {
                return parameterString;
            }
            return parameterString.Substring(0, lastIndexOfSearchInSwitch);
        }

        private async Task<IVsWindowFrame> CreateDocWindowForSolutionAsync()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsWindowFrame windowFrame = null;
            var solution = await this.GetServiceAsync<IVsSolution>();
            var uiShell = await this.GetServiceAsync<SVsUIShell, IVsUIShell>();
            var windowFlags =
                (uint)_VSRDTFLAGS.RDT_DontAddToMRU |
                (uint)_VSRDTFLAGS.RDT_DontSaveAs;

            // when VSSolutionManager is already initialized, then use the existing APIs to check pre-conditions.
            if (!await SolutionManager.Value.IsSolutionAvailableAsync())
            {
                throw new InvalidOperationException(Resources.SolutionIsNotSaved);
            }

            var projects = (await SolutionManager.Value.GetNuGetProjectsAsync()).ToArray();
            if (projects.Length == 0)
            {
                MessageHelper.ShowWarningMessage(Resources.NoSupportedProjectsInSolution, Resources.ErrorDialogBoxTitle);
                return null;
            }

            // pass empty array of NuGetProject
            var uiController = UIFactory.Value.Create(projects);

            var solutionName = (string)_dte.Solution.Properties.Item("Name").Value;

            var model = new PackageManagerModel(
                uiController,
                isSolution: true,
                editorFactoryGuid: GuidList.guidNuGetEditorType)
            {
                SolutionName = solutionName
            };

            var vsWindowSearchHostfactory = await GetServiceAsync(typeof(SVsWindowSearchHostFactory)) as IVsWindowSearchHostFactory;
            var vsShell = await GetServiceAsync(typeof(SVsShell)) as IVsShell4;
            var control = new PackageManagerControl(model, Settings.Value, vsWindowSearchHostfactory, vsShell, OutputConsoleLogger.Value);
            var windowPane = new PackageManagerWindowPane(control);
            var guidEditorType = GuidList.guidNuGetEditorType;
            var guidCommandUI = Guid.Empty;
            var caption = Resx.Label_SolutionNuGetWindowCaption;
            var documentName = await SolutionManager.Value.GetSolutionFilePathAsync();

            var ppunkDocView = IntPtr.Zero;
            var ppunkDocData = IntPtr.Zero;
            var hr = 0;

            try
            {
                ppunkDocView = Marshal.GetIUnknownForObject(windowPane);
                ppunkDocData = Marshal.GetIUnknownForObject(model);
                hr = uiShell.CreateDocumentWindow(
                    windowFlags,
                    documentName,
                    (IVsUIHierarchy)solution,
                    (uint)VSConstants.VSITEMID.Root,
                    ppunkDocView,
                    ppunkDocData,
                    ref guidEditorType,
                    null,
                    ref guidCommandUI,
                    null,
                    caption,
                    string.Empty,
                    null,
                    out windowFrame);

                if (windowFrame != null)
                {
                    WindowFrameHelper.AddF1HelpKeyword(windowFrame, keywordValue: F1KeywordValuePmUI);
                }
            }
            finally
            {
                if (ppunkDocView != IntPtr.Zero)
                {
                    Marshal.Release(ppunkDocData);
                }

                if (ppunkDocData != IntPtr.Zero)
                {
                    Marshal.Release(ppunkDocView);
                }
            }

            ErrorHandler.ThrowOnFailure(hr);
            return windowFrame;
        }

        private void ShowManageLibraryPackageForSolutionDialog(object sender, EventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                if (ShouldMEFBeInitialized())
                {
                    await InitializeMEFAsync();
                }

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var windowFrame = await FindExistingSolutionWindowFrameAsync();
                if (windowFrame == null)
                {
                    // Create the window frame
                    windowFrame = await CreateDocWindowForSolutionAsync();
                }

                if (windowFrame != null)
                {
                    // process search string
                    string parameterString = null;
                    var args = e as OleMenuCmdEventArgs;
                    if (args != null)
                    {
                        parameterString = args.InValue as string;
                    }
                    var searchText = GetSearchText(parameterString);
                    Search(windowFrame, searchText);

                    windowFrame.Show();
                }
            });
        }

        /// <summary>
        /// Search for packages using the searchText.
        /// </summary>
        /// <param name="windowFrame">A window frame that hosts the PackageManagerControl.</param>
        /// <param name="searchText">Search text.</param>
        private void Search(IVsWindowFrame windowFrame, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return;
            }

            var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
            if (packageManagerControl != null)
            {
                packageManagerControl.Search(searchText);
            }
        }

        // For PowerShell, it's okay to query from the worker thread.
        private void BeforeQueryStatusForPowerConsole(object sender, EventArgs args)
        {
            if (ShouldMEFBeInitialized())
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(InitializeMEFAsync);
            }

            var isConsoleBusy = false;
            if (ConsoleStatus != null)
            {
                isConsoleBusy = ConsoleStatus.Value.IsBusy;
            }

            var command = (OleMenuCommand)sender;
            command.Enabled = !isConsoleBusy && !_powerConsoleCommandExecuting;
        }

        private void BeforeQueryStatusForUpgradeNuGetProject(object sender, EventArgs args)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                if (ShouldMEFBeInitialized())
                {
                  await InitializeMEFAsync();
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var command = (OleMenuCommand)sender;
                
                var isConsoleBusy = false;
                if (ConsoleStatus != null)
                {
                    isConsoleBusy = ConsoleStatus.Value.IsBusy;
                }

                command.Visible = IsSolutionOpen && await IsPackagesConfigBasedProjectAsync();
                command.Enabled = !isConsoleBusy && IsSolutionExistsAndNotDebuggingAndNotBuilding() && HasActiveLoadedSupportedProject;
            });
        }

        private void BeforeQueryStatusForUpgradePackagesConfig(object sender, EventArgs args)
        {
            // Check whether to show context menu item on packages.config
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                if (ShouldMEFBeInitialized())
                {
                    await InitializeMEFAsync();
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var command = (OleMenuCommand)sender;

                var isConsoleBusy = false;
                if (ConsoleStatus != null)
                {
                    isConsoleBusy = ConsoleStatus.Value.IsBusy;
                }

                command.Visible = IsSolutionOpen && IsPackagesConfigSelected();
                command.Enabled = !isConsoleBusy && IsSolutionExistsAndNotDebuggingAndNotBuilding() && HasActiveLoadedSupportedProject;
            });
        }

        private async Task<bool> IsPackagesConfigBasedProjectAsync()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dteProject = EnvDTEProjectInfoUtility.GetActiveProject(VsMonitorSelection);

            var uniqueName = EnvDTEProjectInfoUtility.GetUniqueName(dteProject);
            var nuGetProject = await SolutionManager.Value.GetNuGetProjectAsync(uniqueName);

            if (nuGetProject == null)
            {
                return false;
            }

            var msBuildNuGetProject = nuGetProject as MSBuildNuGetProject;

            if (msBuildNuGetProject == null || !msBuildNuGetProject.PackagesConfigNuGetProject.PackagesConfigExists())
            {
                return false;
            }

            return true;
        }


        private bool IsSolutionOpen => _dte?.Solution != null && _dte.Solution.IsOpen;

        private bool IsPackagesConfigSelected()
        {
            return NuGetProjectUpgradeUtility.IsPackagesConfigSelected(VsMonitorSelection);
        }

        private void BeforeQueryStatusForAddPackageDialog(object sender, EventArgs args)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                if (ShouldMEFBeInitialized())
                {
                    await InitializeMEFAsync();
                }

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var command = (OleMenuCommand)sender;

                // Keep the 'Manage NuGet Packages' visible, only if a solution is open. Following is why.
                // When all menu commands in the 'Project' menu are invisible, when a solution is closed, Project menu goes away.
                // This is actually true. All the menu commands under the 'Project Menu' do go away when no solution is open.
                // If 'Manage NuGet Packages' is disabled but visible, 'Project' menu shows up just because 1 menu command is visible, even though, it is disabled
                // So, make it invisible when no solution is open
                command.Visible = IsSolutionOpen;

                var isConsoleBusy = false;
                if (ConsoleStatus != null)
                {
                    isConsoleBusy = ConsoleStatus.Value.IsBusy;
                }
                // Enable the 'Manage NuGet Packages' dialog menu
                // - if the solution exists and not debugging and not building AND
                // - if the console is NOT busy executing a command, AND
                // - if the active project is loaded and supported
                command.Enabled =
                    IsSolutionExistsAndNotDebuggingAndNotBuilding() &&
                    !isConsoleBusy &&
                    HasActiveLoadedSupportedProject;
            });
        }

        private void BeforeQueryStatusForAddPackageForSolutionDialog(object sender, EventArgs args)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                if (ShouldMEFBeInitialized())
                {
                    await InitializeMEFAsync();
                }

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var command = (OleMenuCommand)sender;

                var isConsoleBusy = false;
                if (ConsoleStatus != null)
                {
                    isConsoleBusy = ConsoleStatus.Value.IsBusy;
                }
                // Enable the 'Manage NuGet Packages For Solution' dialog menu
                // - if the console is NOT busy executing a command, AND
                // - if the solution exists and not debugging and not building AND
                // - if there are NuGetProjects. This means there are loaded, supported projects.
                command.Enabled =
                    IsSolutionExistsAndNotDebuggingAndNotBuilding() &&
                    !isConsoleBusy &&
                    await SolutionManager.Value.DoesNuGetSupportsAnyProjectAsync();
            });
        }

        private bool IsSolutionExistsAndNotDebuggingAndNotBuilding()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var hr = VsMonitorSelection.IsCmdUIContextActive(
                _solutionExistsAndFullyLoadedContextCookie, out var pfActive);

            if (ErrorHandler.Succeeded(hr) && pfActive > 0)
            {
                hr = VsMonitorSelection.IsCmdUIContextActive(
                    _solutionNotBuildingAndNotDebuggingContextCookie, out pfActive);

                return ErrorHandler.Succeeded(hr) && pfActive > 0;
            }

            return false;
        }

        private void ShowPackageSourcesOptionPage(object sender, EventArgs args)
        {
            ShowOptionPageSafe(typeof(PackageSourceOptionsPage));
        }

        private void ShowGeneralSettingsOptionPage(object sender, EventArgs args)
        {
            ShowOptionPageSafe(typeof(GeneralOptionPage));
        }

        private void ShowOptionPageSafe(Type optionPageType)
        {
            try
            {
                ShowOptionPage(optionPageType);
            }
            catch (Exception exception)
            {
                MessageHelper.ShowErrorMessage(exception, Resources.ErrorDialogBoxTitle);
                ExceptionHelper.WriteErrorToActivityLog(exception);
            }
        }

        /// <summary>
        /// Gets whether the current IDE has an active, supported and non-unloaded project, which is a precondition for
        /// showing the Add Library Package Reference dialog
        /// </summary>
        private bool HasActiveLoadedSupportedProject
        {
            get
            {
                var project = EnvDTEProjectInfoUtility.GetActiveProject(VsMonitorSelection);
                return project != null && !EnvDTEProjectInfoUtility.IsUnloaded(project)
                       && EnvDTEProjectUtility.IsSupported(project);
            }
        }

        private bool ShouldMEFBeInitialized()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_initialized)
            {
                var hr = VsMonitorSelection.IsCmdUIContextActive(
                _solutionExistsCookie, out var pfActive);

                return ErrorHandler.Succeeded(hr) && pfActive > 0;
            }

            return false;
        }

        #region IVsPackageExtensionProvider implementation

        public dynamic CreateExtensionInstance(ref Guid extensionPoint, ref Guid instance)
        {
            if (instance == typeof(NuGetSearchProvider).GUID)
            {
                return new NuGetSearchProvider(_mcs, _managePackageDialogCommand, _managePackageForSolutionDialogCommand);
            }
            return null;
        }

        #endregion IVsPackageExtensionProvider implementation

        private void OnBeginShutDown()
        {
            _dteEvents.OnBeginShutdown -= OnBeginShutDown;
            _dteEvents = null;
        }

        #region IVsPersistSolutionOpts

        // Called by the shell when a solution is opened and the SUO file is read.
        public int LoadUserOptions(IVsSolutionPersistence pPersistence, uint grfLoadOpts)
        {
            if (ShouldMEFBeInitialized())
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(InitializeMEFAsync);
            }

            return SolutionUserOptions.Value.LoadUserOptions(pPersistence, grfLoadOpts);
        }

        public int ReadUserOptions(IStream _, string __)
        {
            // no package specific streams to read
            return VSConstants.S_OK;
        }

        // Called by the shell when the SUO file is saved. The provider calls the shell back to let it
        // know which options keys it will use in the suo file.
        public int SaveUserOptions(IVsSolutionPersistence pPersistence)
        {
            if (SolutionUserOptions != null && SolutionUserOptions.IsValueCreated)
            {
                return SolutionUserOptions.Value.SaveUserOptions(pPersistence);
            }

            return VSConstants.S_OK;
        }

        public int WriteUserOptions(IStream _, string __)
        {
            // no package specific streams to write
            return VSConstants.S_OK;
        }

        #endregion IVsPersistSolutionOpts
    }
}
