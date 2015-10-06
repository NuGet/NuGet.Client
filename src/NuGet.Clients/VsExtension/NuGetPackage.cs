// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet;
using NuGet.Options;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGetConsole;
using NuGetConsole.Implementation;
using IMachineWideSettings = NuGet.Configuration.IMachineWideSettings;
using ISettings = NuGet.Configuration.ISettings;
using Resx = NuGet.PackageManagement.UI.Resources;
using Strings = NuGet.PackageManagement.VisualStudio.Strings;
using NuGet.Credentials;

namespace NuGetVSExtension
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", ProductVersion, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(PowerConsoleToolWindow),
        Style = VsDockStyle.Tabbed,
        Window = "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}", // this is the guid of the Output tool window, which is present in both VS and VWD
        Orientation = ToolWindowOrientation.Right)]
    //[ProvideToolWindow(typeof(DebugConsoleToolWindow),
    //    Style = VsDockStyle.Tabbed,
    //    Window = "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}",      // this is the guid of the debug tool window, which is present in both VS and VWD
    //    Orientation = ToolWindowOrientation.Right)]
    [ProvideOptionPage(typeof(PackageSourceOptionsPage), "NuGet Package Manager", "Package Sources", 113, 114, true)]
    [ProvideOptionPage(typeof(GeneralOptionPage), "NuGet Package Manager", "General", 113, 115, true)]
    [ProvideSearchProvider(typeof(NuGetSearchProvider), "NuGet Search")]
    [ProvideBindingPath] // Definition dll needs to be on VS binding path
    [ProvideAutoLoad(GuidList.guidAutoLoadNuGetString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionBuilding_string)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ProjectRetargeting_string)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOrProjectUpgrading_string)]
    [FontAndColorsRegistration(
        "Package Manager Console",
        NuGetConsole.Implementation.GuidList.GuidPackageManagerConsoleFontAndColorCategoryString,
        "{" + GuidList.guidNuGetPkgString + "}")]
    [Guid(GuidList.guidNuGetPkgString)]
    public sealed class NuGetPackage : Package, IVsPackageExtensionProvider, IVsPersistSolutionOpts
    {
        // It is displayed in the Help - About box of Visual Studio
        public const string ProductVersion = "3.3.0";
        private static readonly object _credentialsPromptLock = new object();

        private static readonly string[] _visualizerSupportedSKUs = { "Premium", "Ultimate" };

        private uint _solutionNotBuildingAndNotDebuggingContextCookie;
        private DTE _dte;
        private DTEEvents _dteEvents;
        private IConsoleStatus _consoleStatus;
        private IVsMonitorSelection _vsMonitorSelection;
        private bool? _isVisualizerSupported;
        private IPackageRestoreManager _packageRestoreManager;

        private ISettings _settings;
        private ISourceControlManagerProvider _sourceControlManagerProvider;
        private IVsSourceControlTracker _vsSourceControlTracker;
        private ICommonOperations _commonOperations;
        private ISolutionManager _solutionManager;
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private IDeleteOnRestartManager _deleteOnRestart;

        private OleMenuCommand _managePackageDialogCommand;

        private OleMenuCommand _managePackageForSolutionDialogCommand;
        private OleMenuCommandService _mcs;
        private bool _powerConsoleCommandExecuting;
        private IMachineWideSettings _machineWideSettings;

        private NuGetUIProjectContext _uiProjectContext;

        private NuGetSettings _nugetSettings;

        private OutputConsoleLogger _outputConsoleLogger;
        private readonly HashSet<Uri> _credentialRequested;

        public NuGetPackage()
        {
            ServiceLocator.InitializePackageServiceProvider(this);
            StandaloneSwitch.IsRunningStandalone = false;
            _nugetSettings = new NuGetSettings();
            _credentialRequested = new HashSet<Uri>();
        }

        private IVsMonitorSelection VsMonitorSelection
        {
            get
            {
                if (_vsMonitorSelection == null)
                {
                    // get the UI context cookie for the debugging mode
                    _vsMonitorSelection = (IVsMonitorSelection)GetService(typeof(IVsMonitorSelection));

                    // get the solution not building and not debugging cookie
                    Guid guid = VSConstants.UICONTEXT.SolutionExistsAndNotBuildingAndNotDebugging_guid;
                    _vsMonitorSelection.GetCmdUIContextCookie(ref guid, out _solutionNotBuildingAndNotDebuggingContextCookie);
                }
                return _vsMonitorSelection;
            }
        }

        private IConsoleStatus ConsoleStatus
        {
            get
            {
                if (_consoleStatus == null)
                {
                    _consoleStatus = ServiceLocator.GetInstanceSafe<IConsoleStatus>();
                    Debug.Assert(_consoleStatus != null);
                }

                return _consoleStatus;
            }
        }

        private IPackageRestoreManager PackageRestoreManager
        {
            get
            {
                if (_packageRestoreManager == null)
                {
                    _packageRestoreManager = ServiceLocator.GetInstance<IPackageRestoreManager>();
                    Debug.Assert(_packageRestoreManager != null);
                }
                return _packageRestoreManager;
            }
        }

        private ISettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = ServiceLocator.GetInstance<ISettings>();
                    Debug.Assert(_settings != null);
                }
                return _settings;
            }
        }

        private ISourceControlManagerProvider SourceControlManagerProvider
        {
            get
            {
                if (_sourceControlManagerProvider == null)
                {
                    _sourceControlManagerProvider = ServiceLocator.GetInstanceSafe<ISourceControlManagerProvider>();
                }
                return _sourceControlManagerProvider;
            }
        }

        private ICommonOperations CommonOperations
        {
            get
            {
                if (_commonOperations == null)
                {
                    _commonOperations = ServiceLocator.GetInstanceSafe<ICommonOperations>();
                }
                return _commonOperations;
            }
        }

        private ISolutionManager SolutionManager
        {
            get
            {
                if (_solutionManager == null)
                {
                    _solutionManager = ServiceLocator.GetInstance<ISolutionManager>();
                    Debug.Assert(_solutionManager != null);
                }
                return _solutionManager;
            }
        }

        private ISourceRepositoryProvider SourceRepositoryProvider
        {
            get
            {
                if (_sourceRepositoryProvider == null)
                {
                    _sourceRepositoryProvider = ServiceLocator.GetInstance<ISourceRepositoryProvider>();
                    Debug.Assert(_sourceRepositoryProvider != null);
                }
                return _sourceRepositoryProvider;
            }
        }

        private IDeleteOnRestartManager DeleteOnRestart
        {
            get
            {
                if (_deleteOnRestart == null)
                {
                    _deleteOnRestart = ServiceLocator.GetInstance<IDeleteOnRestartManager>();
                    Debug.Assert(_deleteOnRestart != null);
                }

                return _deleteOnRestart;
            }
        }

        private IMachineWideSettings MachineWideSettings
        {
            get
            {
                if (_machineWideSettings == null)
                {
                    _machineWideSettings = ServiceLocator.GetInstance<IMachineWideSettings>();
                    Debug.Assert(_machineWideSettings != null);
                }

                return _machineWideSettings;
            }
        }

        private OnBuildPackageRestorer OnBuildPackageRestorer { get; set; }

        private ProjectRetargetingHandler ProjectRetargetingHandler { get; set; }

        private ProjectUpgradeHandler ProjectUpgradeHandler { get; set; }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            Styles.Initialize();
            Brushes.Initialize();

            // ***
            // VsNuGetDiagnostics.Initialize(
            //    ServiceLocator.GetInstance<IDebugConsoleController>());

            // Add our command handlers for menu (commands must exist in the .vsct file)
            AddMenuCommandHandlers();

            // IMPORTANT: Do NOT do anything that can lead to a call to ServiceLocator.GetGlobalService().
            // Doing so is illegal and may cause VS to hang.

            _dte = (DTE)GetService(typeof(SDTE));
            Debug.Assert(_dte != null);

            _dteEvents = _dte.Events.DTEEvents;
            _dteEvents.OnBeginShutdown += OnBeginShutDown;

            SetDefaultCredentialProvider();

            if (SolutionManager != null)
            {
                SolutionManager.SolutionOpened += (obj, ev) =>
                    {
                        _nugetSettings = new NuGetSettings();
                        LoadNuGetSettings();
                    };
            }

            _outputConsoleLogger = new OutputConsoleLogger(this);
            _uiProjectContext = new NuGetUIProjectContext(
                _outputConsoleLogger,
                SourceControlManagerProvider,
                CommonOperations);

            if (SolutionManager.NuGetProjectContext == null)
            {
                SolutionManager.NuGetProjectContext = _uiProjectContext;
            }

            // when NuGet loads, if the current solution has some package
            // folders marked for deletion (because a previous uninstalltion didn't succeed),
            // delete them now.
            if (SolutionManager.IsSolutionOpen)
            {
                DeleteOnRestart.DeleteMarkedPackageDirectories(_uiProjectContext);
            }

            // NOTE: Don't use the exported IPackageRestoreManager for OnBuildPackageRestorer. Exported IPackageRestoreManager also uses 'PackageRestoreManager'
            //       but, overrides RestoreMissingPackages to catch the exceptions. OnBuildPackageRestorer needs to catch the exception by itself to populate error list window
            //       Exported IPackageRestoreManager is used by UI manual restore, Powershell manual restore and by VS extensibility package restore
            OnBuildPackageRestorer = new OnBuildPackageRestorer(SolutionManager,
                PackageRestoreManager,
                this,
                SourceRepositoryProvider,
                Settings,
                new EmptyNuGetProjectContext());

            ProjectRetargetingHandler = new ProjectRetargetingHandler(_dte, SolutionManager, this);
            ProjectUpgradeHandler = new ProjectUpgradeHandler(this, SolutionManager);

            LoadNuGetSettings();

            // This initializes the IVSSourceControlTracker, even though _vsSourceControlTracker is unused.
            _vsSourceControlTracker = ServiceLocator.GetInstanceSafe<IVsSourceControlTracker>();
        }

        /// <summary>
        /// Set default credential provider for the HttpClient, which is used by V2 sources.
        /// Also set up authenticated proxy handling for V3 sources.
        /// </summary>
        private void SetDefaultCredentialProvider()
        {
            var webProxy = (IVsWebProxy)GetService(typeof(SVsWebProxy));
            Debug.Assert(webProxy != null);

            PackageSourceProvider packageSourceProvider = new PackageSourceProvider(
                new SettingsToLegacySettings(Settings));

            var credentialProviders = new List<NuGet.Credentials.ICredentialProvider>
            {
                new CredentialProviderAdapter(new SettingsCredentialProvider(
                    NuGet.NullCredentialProvider.Instance, packageSourceProvider)),
                new VisualStudioAccountProvider(),
                new VisualStudioCredentialProvider(webProxy)
            };

            var credentialService = new CredentialService(
                credentialProviders,
                (s) => this._outputConsoleLogger.OutputConsole.WriteLine(s),
                nonInteractive: false);

            HttpClient.DefaultCredentialProvider = new CredentialServiceAdapter(credentialService); ;

            // Set up proxy handling for v3 sources.
            // We need to sync the v2 proxy cache and v3 proxy cache so that the user will not
            // get prompted twice for the same authenticated proxy.
            var v2ProxyCache = ProxyCache.Instance;
            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.PromptForProxyCredentials =
                async (uri, proxy, cancellationToken) =>
                {
                    var v2Credentials = v2ProxyCache?.GetProxy(uri)?.Credentials;
                    if (v2Credentials != null && proxy.Credentials != v2Credentials)
                    {
                        // if cached v2 credentials have not been used, try using it first.
                        return v2Credentials;
                    }

                    return await credentialService
                        .GetCredentials(uri, proxy, isProxy: true, cancellationToken: cancellationToken);
                };

            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.ProxyPassed = proxy =>
            {
                // add the proxy to v2 proxy cache.
                v2ProxyCache?.Add(proxy);
            };

            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.PromptForCredentials =
                async (uri, cancellationToken) =>
                {
                    // Get the proxy for this URI so we can pass it to the credentialService methods
                    // this lets them use the proxy if they have to hit the network.
                    var proxyCache = ProxyCache.Instance;
                    var proxy = proxyCache?.GetProxy(uri);

                    return await credentialService
                        .GetCredentials(uri, proxy: proxy, isProxy: false, cancellationToken: cancellationToken);
                };

            NuGet.Protocol.Core.v3.HttpHandlerResourceV3.CredentialsSuccessfullyUsed = (uri, credentials) =>
            {
                NuGet.CredentialStore.Instance.Add(uri, credentials);
                NuGet.Configuration.CredentialStore.Instance.Add(uri, credentials);
            };
        }

        private void AddMenuCommandHandlers()
        {
            _mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != _mcs)
            {
                // menu command for opening Package Manager Console
                CommandID toolwndCommandID = new CommandID(GuidList.guidNuGetConsoleCmdSet, PkgCmdIDList.cmdidPowerConsole);
                OleMenuCommand powerConsoleExecuteCommand = new OleMenuCommand(ExecutePowerConsoleCommand, null, BeforeQueryStatusForPowerConsole, toolwndCommandID);
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
                CommandID managePackageDialogCommandID = new CommandID(GuidList.guidNuGetDialogCmdSet, PkgCmdIDList.cmdidAddPackageDialog);
                _managePackageDialogCommand = new OleMenuCommand(ShowManageLibraryPackageDialog, null, BeforeQueryStatusForAddPackageDialog, managePackageDialogCommandID);
                // '$' - This indicates that the input line other than the argument forms a single argument string with no autocompletion
                //       Autocompletion for filename(s) is supported for option 'p' or 'd' which is not applicable for this command
                _managePackageDialogCommand.ParametersDescription = "$";
                _mcs.AddCommand(_managePackageDialogCommand);

                // menu command for opening "Manage NuGet packages for solution" dialog
                CommandID managePackageForSolutionDialogCommandID = new CommandID(GuidList.guidNuGetDialogCmdSet, PkgCmdIDList.cmdidAddPackageDialogForSolution);
                _managePackageForSolutionDialogCommand = new OleMenuCommand(ShowManageLibraryPackageForSolutionDialog, null, BeforeQueryStatusForAddPackageForSolutionDialog, managePackageForSolutionDialogCommandID);
                // '$' - This indicates that the input line other than the argument forms a single argument string with no autocompletion
                //       Autocompletion for filename(s) is supported for option 'p' or 'd' which is not applicable for this command
                _managePackageForSolutionDialogCommand.ParametersDescription = "$";
                _mcs.AddCommand(_managePackageForSolutionDialogCommand);

                // menu command for opening Package Source settings options page
                CommandID settingsCommandID = new CommandID(GuidList.guidNuGetConsoleCmdSet, PkgCmdIDList.cmdidSourceSettings);
                OleMenuCommand settingsMenuCommand = new OleMenuCommand(ShowPackageSourcesOptionPage, settingsCommandID);
                _mcs.AddCommand(settingsMenuCommand);

                // menu command for opening General options page
                CommandID generalSettingsCommandID = new CommandID(GuidList.guidNuGetToolsGroupCmdSet, PkgCmdIDList.cmdIdGeneralSettings);
                OleMenuCommand generalSettingsCommand = new OleMenuCommand(ShowGeneralSettingsOptionPage, generalSettingsCommandID);
                _mcs.AddCommand(generalSettingsCommand);

                // menu command for Package Restore command
                CommandID restorePackagesCommandID = new CommandID(GuidList.guidNuGetDialogCmdSet, PkgCmdIDList.cmdidRestorePackages);
                var restorePackagesCommand = new OleMenuCommand(RestorePackages, null, BeforeQueryStatusForPackageRestore, restorePackagesCommandID);
                _mcs.AddCommand(restorePackagesCommand);
            }
        }

        private void ExecutePowerConsoleCommand(object sender, EventArgs e)
        {
            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = FindToolWindow(typeof(PowerConsoleToolWindow), 0, true);
            if ((null == window)
                || (null == window.Frame))
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }
            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());

            // Parse the arguments to determine the command and arguments to be passed to IHost
            // passed which is of type OleMenuCmdEventArgs
            string command = null;
            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;
            if (eventArgs != null
                && eventArgs.InValue != null)
            {
                command = eventArgs.InValue as string;
            }

            // If the command string is null or empty, simply launch the console and return
            if (!String.IsNullOrEmpty(command))
            {
                IPowerConsoleService powerConsoleService = (IPowerConsoleService)window;

                if (powerConsoleService.Execute(command, null))
                {
                    _powerConsoleCommandExecuting = true;
                    powerConsoleService.ExecuteEnd += PowerConsoleService_ExecuteEnd;
                }
            }
        }

        private void ShowDebugConsole(object sender, EventArgs e)
        {
            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            ToolWindowPane window = FindToolWindow(typeof(DebugConsoleToolWindow), 0, true);
            if ((null == window)
                || (null == window.Frame))
            {
                throw new NotSupportedException(Resources.CanNotCreateWindow);
            }

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }

        private void PowerConsoleService_ExecuteEnd(object sender, EventArgs e)
        {
            _powerConsoleCommandExecuting = false;
        }

        /// <summary>
        /// Executes the NuGet Visualizer.
        /// </summary>
        private void ExecuteVisualizer(object sender, EventArgs e)
        {
            /* ***
            var visualizer = new NuGet.Dialog.Visualizer(
                ServiceLocator.GetInstance<IVsPackageManagerFactory>(),
                ServiceLocator.GetInstance<ISolutionManager>());
            string outputFile = visualizer.CreateGraph();
            _dte.ItemOperations.OpenFile(outputFile); */
        }

        private IVsWindowFrame FindExistingWindowFrame(
            Project project)
        {
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                object docView;
                int hr = windowFrame.GetProperty(
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
                    if (String.Equals(projectName, project.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return windowFrame;
                    }
                }
            }

            return null;
        }

        private static T GetProperty<T>(IVsHierarchy hier, __VSHPROPID propertyId)
        {
            object propertyValue;
            hier.GetProperty(
                (uint)VSConstants.VSITEMID.Root,
                (int)propertyId,
                out propertyValue);
            return (T)propertyValue;
        }

        private async Task<IVsWindowFrame> CreateNewWindowFrameAsync(Project project)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            var vsProject = VsHierarchyUtility.ToVsHierarchy(project);
            var documentName = project.UniqueName;

            // Find existing hierarchy and item id of the document window if it's
            // already registered.
            var rdt = (IVsRunningDocumentTable)GetService(typeof(IVsRunningDocumentTable));
            IVsHierarchy hier;
            uint itemId;
            IntPtr docData = IntPtr.Zero;
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
            uint windowFlags =
                (uint)_VSRDTFLAGS.RDT_DontAddToMRU |
                (uint)_VSRDTFLAGS.RDT_DontSaveAs;

            var solutionManager = ServiceLocator.GetInstance<ISolutionManager>();

            if (!solutionManager.IsSolutionAvailable)
            {
                throw new InvalidOperationException(Strings.SolutionIsNotSaved);
            }

            var nugetProject = solutionManager.GetNuGetProject(EnvDTEProjectUtility.GetCustomUniqueName(project));

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

            var uiContextFactory = ServiceLocator.GetInstance<INuGetUIContextFactory>();
            var uiContext = uiContextFactory.Create(this, new[] { nugetProject });

            var uiFactory = ServiceLocator.GetInstance<INuGetUIFactory>();
            var uiController = uiFactory.Create(uiContext, _uiProjectContext);

            var model = new PackageManagerModel(uiController, uiContext, isSolution: false);
            var vsWindowSearchHostfactory = ServiceLocator.GetGlobalService<SVsWindowSearchHostFactory, IVsWindowSearchHostFactory>();
            var vsShell = ServiceLocator.GetGlobalService<SVsShell, IVsShell4>();
            var control = new PackageManagerControl(model, Settings, vsWindowSearchHostfactory, vsShell);
            var windowPane = new PackageManagerWindowPane(control);
            var guidEditorType = Guid.Empty;
            var guidCommandUI = Guid.Empty;
            var caption = String.Format(
                CultureInfo.CurrentCulture,
                Resx.Label_NuGetWindowCaption,
                project.Name);

            IVsWindowFrame windowFrame;
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));

            IntPtr ppunkDocView = IntPtr.Zero;
            IntPtr ppunkDocData = IntPtr.Zero;
            int hr = 0;

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

        private void ShowManageLibraryPackageDialog(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                string parameterString = null;
                OleMenuCmdEventArgs args = e as OleMenuCmdEventArgs;
                if (null != args)
                {
                    parameterString = args.InValue as string;
                }
                var searchText = GetSearchText(parameterString);

                // *** temp code
                Project project = EnvDTEProjectUtility.GetActiveProject(VsMonitorSelection);

                if (project != null
                    &&
                    !EnvDTEProjectUtility.IsUnloaded(project)
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
                    string projectName = project != null ? project.Name : String.Empty;

                    string errorMessage = String.IsNullOrEmpty(projectName)
                        ? Resources.NoProjectSelected
                        : String.Format(CultureInfo.CurrentCulture, Strings.DTE_ProjectUnsupported, projectName);

                    MessageHelper.ShowWarningMessage(errorMessage, Resources.ErrorDialogBoxTitle);
                }
            });
        }

        private IVsWindowFrame FindExistingSolutionWindowFrame()
        {
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                object property;
                int hr = windowFrame.GetProperty(
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
            int lastIndexOfSearchInSwitch = parameterString.LastIndexOf("/searchin:", StringComparison.OrdinalIgnoreCase);

            if (lastIndexOfSearchInSwitch == -1)
            {
                return parameterString;
            }
            return parameterString.Substring(0, lastIndexOfSearchInSwitch);
        }

        private void LoadNuGetSettings()
        {
            IVsSolutionPersistence solutionPersistence = GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            solutionPersistence.LoadPackageUserOpts(this, "nuget");
        }

        public void SaveNuGetSettings()
        {
            IVsSolutionPersistence solutionPersistence = GetGlobalService(typeof(SVsSolutionPersistence)) as IVsSolutionPersistence;
            solutionPersistence.SavePackageUserOpts(this, "nuget");
        }

        public UserSettings GetWindowSetting(string key)
        {
            UserSettings settings;
            if (_nugetSettings.WindowSettings.TryGetValue(key, out settings))
            {
                return settings ?? new UserSettings();
            }

            return new UserSettings();
        }

        public void AddWindowSettings(string key, UserSettings obj)
        {
            _nugetSettings.WindowSettings[key] = obj;
        }

        private async Task<IVsWindowFrame> CreateDocWindowForSolutionAsync()
        {
            IVsWindowFrame windowFrame = null;
            IVsSolution solution = ServiceLocator.GetInstance<IVsSolution>();
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            uint windowFlags =
                (uint)_VSRDTFLAGS.RDT_DontAddToMRU |
                (uint)_VSRDTFLAGS.RDT_DontSaveAs;

            var solutionManager = ServiceLocator.GetInstance<ISolutionManager>();

            if (!solutionManager.IsSolutionAvailable)
            {
                throw new InvalidOperationException(Strings.SolutionIsNotSaved);
            }

            var projects = solutionManager.GetNuGetProjects();
            if (!projects.Any())
            {
                // NOTE: The menu 'Manage NuGet Packages For Solution' will be disabled in this case.
                // But, it is possible, that, before NuGetPackage is loaded in VS, the menu is enabled and used.
                // For once, this message will be shown. Once the package is loaded, the menu will get disabled as appropriate
                MessageHelper.ShowWarningMessage(Resources.NoSupportedProjectsInSolution, Resources.ErrorDialogBoxTitle);
                return null;
            }

            // load packages.config. This makes sure that an exception will get thrown if there
            // are problems with packages.config, such as duplicate packages. When an exception
            // is thrown, an error dialog will pop up and this doc window will not be created.
            foreach (var project in projects)
            {
                await project.GetInstalledPackagesAsync(CancellationToken.None);
            }

            var uiContextFactory = ServiceLocator.GetInstance<INuGetUIContextFactory>();
            var uiContext = uiContextFactory.Create(this, projects);

            var uiFactory = ServiceLocator.GetInstance<INuGetUIFactory>();
            var uiController = uiFactory.Create(uiContext, _uiProjectContext);

            var solutionName = (string)_dte.Solution.Properties.Item("Name").Value;
            var model = new PackageManagerModel(uiController, uiContext, isSolution: true);
            model.SolutionName = solutionName;
            var vsWindowSearchHostfactory = ServiceLocator.GetGlobalService<SVsWindowSearchHostFactory, IVsWindowSearchHostFactory>();
            var vsShell = ServiceLocator.GetGlobalService<SVsShell, IVsShell4>();
            var control = new PackageManagerControl(model, Settings, vsWindowSearchHostfactory, vsShell);
            var windowPane = new PackageManagerWindowPane(control);
            var guidEditorType = Guid.Empty;
            var guidCommandUI = Guid.Empty;
            var caption = Resx.Label_SolutionNuGetWindowCaption;
            var documentName = _dte.Solution.FullName;

            IntPtr ppunkDocView = IntPtr.Zero;
            IntPtr ppunkDocData = IntPtr.Zero;
            int hr = 0;

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
            ThreadHelper.ThrowIfNotOnUIThread();

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var windowFrame = FindExistingSolutionWindowFrame();
                if (windowFrame == null)
                {
                    // Create the window frame
                    windowFrame = await CreateDocWindowForSolutionAsync();
                }

                if (windowFrame != null)
                {
                    // process search string
                    string parameterString = null;
                    OleMenuCmdEventArgs args = e as OleMenuCmdEventArgs;
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

        private void RestorePackages(object sender, EventArgs args)
        {
            OnBuildPackageRestorer.RestorePackages();
        }

        // For PowerShell, it's okay to query from the worker thread.
        private void BeforeQueryStatusForPowerConsole(object sender, EventArgs args)
        {
            OleMenuCommand command = (OleMenuCommand)sender;
            command.Enabled = !ConsoleStatus.IsBusy && !_powerConsoleCommandExecuting;
        }

        private void BeforeQueryStatusForAddPackageDialog(object sender, EventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OleMenuCommand command = (OleMenuCommand)sender;

                // Keep the 'Manage NuGet Packages' visible, only if a solution is open. Following is why.
                // When all menu commands in the 'Project' menu are invisible, when a solution is closed, Project menu goes away.
                // This is actually true. All the menu commands under the 'Project Menu' do go away when no solution is open.
                // If 'Manage NuGet Packages' is disabled but visible, 'Project' menu shows up just because 1 menu command is visible, even though, it is disabled
                // So, make it invisible when no solution is open
                command.Visible = (_dte != null && _dte.Solution != null && _dte.Solution.IsOpen);

                // Enable the 'Manage NuGet Packages' dialog menu
                // a) if the console is NOT busy executing a command, AND
                // b) if the solution exists and not debugging and not building AND
                // c) if the active project is loaded and supported
                command.Enabled = !ConsoleStatus.IsBusy && IsSolutionExistsAndNotDebuggingAndNotBuilding() && HasActiveLoadedSupportedProject;
            });
        }

        private void BeforeQueryStatusForAddPackageForSolutionDialog(object sender, EventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OleMenuCommand command = (OleMenuCommand)sender;

                // Enable the 'Manage NuGet Packages For Solution' dialog menu
                // a) if the console is NOT busy executing a command, AND
                // b) if the solution exists and not debugging and not building AND
                // c) if there are no NuGetProjects. This means that there no loaded, supported projects
                command.Enabled = !ConsoleStatus.IsBusy && IsSolutionExistsAndNotDebuggingAndNotBuilding() && SolutionManager.GetNuGetProjects().Any();
            });
        }

        private void BeforeQueryStatusForPackageRestore(object sender, EventArgs args)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OleMenuCommand command = (OleMenuCommand)sender;

                // Enable the 'Restore NuGet Packages' dialog menu
                // a) if the console is NOT busy executing a command, AND
                // b) if the solution exists and not debugging and not building AND
                // c) if there are no NuGetProjects. This means that there no loaded, supported projects
                command.Enabled = !ConsoleStatus.IsBusy && IsSolutionExistsAndNotDebuggingAndNotBuilding() && SolutionManager.GetNuGetProjects().Any();
            });
        }

        private bool IsSolutionExistsAndNotDebuggingAndNotBuilding()
        {
            int pfActive;
            int result = VsMonitorSelection.IsCmdUIContextActive(_solutionNotBuildingAndNotDebuggingContextCookie, out pfActive);
            return (result == VSConstants.S_OK && pfActive > 0);
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
                ExceptionHelper.WriteToActivityLog(exception);
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
                Project project = EnvDTEProjectUtility.GetActiveProject(VsMonitorSelection);
                return project != null && !EnvDTEProjectUtility.IsUnloaded(project)
                       && EnvDTEProjectUtility.IsSupported(project);
            }
        }

        private bool IsVisualizerSupported
        {
            get
            {
                if (_isVisualizerSupported == null)
                {
                    _isVisualizerSupported = _visualizerSupportedSKUs.Contains(_dte.Edition, StringComparer.OrdinalIgnoreCase);
                }
                return _isVisualizerSupported.Value;
            }
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
        #endregion // IVsPackageExtensionProvider implementation

        private void OnBeginShutDown()
        {
            _dteEvents.OnBeginShutdown -= OnBeginShutDown;
            _dteEvents = null;

            // Clean up optimized zips used by NuGet.Core as part of the V2 Protocol
            OptimizedZipPackage.PurgeCache();
        }

        #region IVsPersistSolutionOpts implementation
        public int LoadUserOptions(IVsSolutionPersistence pPersistence, uint grfLoadOpts)
        {
            return VSConstants.S_OK;
        }

        public int ReadUserOptions(IStream pOptionsStream, string pszKey)
        {
            try
            {
                using (var stream = new DataStreamFromComStream(pOptionsStream))
                {
                    BinaryFormatter serializer = new BinaryFormatter();
                    var obj = serializer.Deserialize(stream) as NuGetSettings;
                    if (obj != null)
                    {
                        _nugetSettings = obj;
                    }
                }
            }
            catch
            {
            }

            return VSConstants.S_OK;
        }

        public int SaveUserOptions(IVsSolutionPersistence pPersistence)
        {
            pPersistence.SavePackageUserOpts(this, "nuget");
            return VSConstants.S_OK;
        }

        public int WriteUserOptions(IStream pOptionsStream, string pszKey)
        {
            try
            {
                using (var stream = new DataStreamFromComStream(pOptionsStream))
                {
                    BinaryFormatter serializer = new BinaryFormatter();
                    serializer.Serialize(stream, _nugetSettings);
                }
            }
            catch
            {
            }

            return VSConstants.S_OK;
        }
        #endregion // IVsPersistSolutionOpts implementation
    }
}
