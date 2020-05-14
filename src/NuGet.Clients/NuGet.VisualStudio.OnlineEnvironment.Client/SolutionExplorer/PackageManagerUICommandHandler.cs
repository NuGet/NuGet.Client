// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using Resx = NuGet.PackageManagement.UI.Resources;
using Task = System.Threading.Tasks.Task;
using vsShellInterop = Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.VisualStudio.OnlineEnvironment.Client
{
    internal class PackageManagerUICommandHandler
    {
        private const bool IsPackageManagerUISupportAvailable = true;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly IAsyncServiceProvider _asyncServiceProvider;

        private AsyncLazy<vsShellInterop.IVsMonitorSelection> _vsMonitorSelection;
        private vsShellInterop.IVsMonitorSelection VsMonitorSelection => ThreadHelper.JoinableTaskFactory.Run(_vsMonitorSelection.GetValueAsync);

        private DTE _dte;
        private DTEEvents _dteEvents;
        private const string F1KeywordValuePmUI = "VS.NuGet.PackageManager.UI";
        private uint _solutionExistsAndFullyLoadedContextCookie;
        private uint _solutionNotBuildingAndNotDebuggingContextCookie;
        private uint _solutionExistsCookie;

        private bool _initialized;

        public PackageManagerUICommandHandler(JoinableTaskFactory joinableTaskFactory, IAsyncServiceProvider asyncServiceProvider)
        {
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));
            _asyncServiceProvider = asyncServiceProvider ?? throw new ArgumentNullException(nameof(asyncServiceProvider));
			Initialize();
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


        //TODO: need GetService or to convert to use GetServiceAsync
        private IDisposable ProjectRetargetingHandler { get; set; }

        private IDisposable ProjectUpgradeHandler { get; set; }

		private void Initialize()
		{
            _vsMonitorSelection = new AsyncLazy<vsShellInterop.IVsMonitorSelection>(
                async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // get the UI context cookie for the debugging mode
                    var vsMonitorSelection = await _asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.IVsMonitorSelection)) as vsShellInterop.IVsMonitorSelection;
                    Assumes.Present(vsMonitorSelection);

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

        public bool IsPackageManagerUISupported(WorkspaceVisualNodeBase workspaceVisualNodeBase)
        {
            if (workspaceVisualNodeBase is null)
            {
                return false;
            }
            return IsPackageManagerUISupportAvailable;
        }

        public void OpenPackageManagerUI(WorkspaceVisualNodeBase workspaceVisualNodeBase)
        {
            _joinableTaskFactory.RunAsync(() => OpenPackageManagerUIAsync(workspaceVisualNodeBase));
        }

        private async Task OpenPackageManagerUIAsync(WorkspaceVisualNodeBase workspaceVisualNodeBase)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        	await ShowPackageManagerUI(workspaceVisualNodeBase.NodeMoniker);
        }

        /// <summary>
        /// Initialize all MEF imports for this package and also add required event handlers.
        /// </summary>
        private async Task InitializeMEFAsync()
        {
            _initialized = true;

            var componentModel = await _asyncServiceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            Assumes.Present(componentModel);
            componentModel.DefaultCompositionService.SatisfyImportsOnce(this);

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //    SolutionManager.Value.AfterNuGetProjectRenamed += SolutionManager_NuGetProjectRenamed;

            Brushes.LoadVsBrushes();

            _dte = (DTE)await _asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.SDTE));
            Assumes.Present(_dte);

            _dteEvents = _dte.Events.DTEEvents;
            //_dteEvents.OnBeginShutdown += OnBeginShutDown;

            if (SolutionManager.Value.NuGetProjectContext == null)
            {
                SolutionManager.Value.NuGetProjectContext = ProjectContext.Value;
            }

            // when NuGet loads, if the current solution has some package
            // folders marked for deletion (because a previous uninstalltion didn't succeed),
            // delete them now.
            if (await SolutionManager.Value.IsSolutionOpenAsync())
            {
                await DeleteOnRestartManager.Value.DeleteMarkedPackageDirectoriesAsync(ProjectContext.Value);
            }

            //ProjectRetargetingHandler = new ProjectRetargetingHandler(_dte, SolutionManager.Value, this, componentModel);
            //ProjectUpgradeHandler = new ProjectUpgradeHandler(this, SolutionManager.Value);

            SolutionUserOptions.Value.LoadSettings();
        }

 private async Task ShowPackageManagerUI(string projectPath)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (ShouldMEFBeInitialized())
            {
                await InitializeMEFAsync();
            }

            var window = await CreateNewWindowFrameAsync(projectPath);
            if (window != null)
            {
                Search(window, string.Empty);
                window.Show();
            }
        }


        private vsShellInterop.IVsWindowFrame FindExistingWindowFrame(
			string projectPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var uiShell = (vsShellInterop.IVsUIShell)_asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.SVsUIShell));
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                object docView;
                var hr = windowFrame.GetProperty(
                    (int)vsShellInterop.__VSFPROPID.VSFPROPID_DocView,
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
                    if (string.Equals(projectName, projectPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return windowFrame;
                    }
                }
            }

            return null;
        }

        private async Task<vsShellInterop.IVsWindowFrame> CreateNewWindowFrameAsync(string projectPath)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var documentName = projectPath;

            // Find existing hierarchy and item id of the document window if it's already registered.
            var rdt = await _asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.IVsRunningDocumentTable)) as vsShellInterop.IVsRunningDocumentTable;
            Assumes.Present(rdt);
            vsShellInterop.IVsHierarchy hier;
            uint itemId;
            var docData = IntPtr.Zero;
            int hr;

            try
            {
                uint cookie;
                hr = rdt.FindAndLockDocument(
                    (uint)vsShellInterop._VSRDTFLAGS.RDT_NoLock,
                    documentName,
                    out hier,
                    out itemId,
                    out docData,
                    out cookie);

                if (hr != VSConstants.S_OK)
                {
                    // the docuemnt window is not registered yet. So use the hierarchy from the current selection.
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
            return await CreateDocWindowAsync(projectPath, documentName, hier, itemId);
        }

        // private async Task<System.Windows.Window> CreateDocWindowAsync(
        private async Task<vsShellInterop.IVsWindowFrame> CreateDocWindowAsync(
            string projectPath,
            string documentName,
            vsShellInterop.IVsHierarchy hier,
            uint itemId)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var uiController = UIFactory.Value.Create();

            var model = new PackageManagerModel(
                uiController,
                isSolution: false,
                editorFactoryGuid: GuidList.NuGetEditorType);

            var vsWindowSearchHostfactory = await _asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.SVsWindowSearchHostFactory)) as vsShellInterop.IVsWindowSearchHostFactory;
            var vsShell = await _asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.SVsShell)) as vsShellInterop.IVsShell4;

            var control = new PackageManagerControl(model, Settings.Value, vsWindowSearchHostfactory, vsShell, OutputConsoleLogger.Value);
            var windowPane = new PackageManagerToolWindowPane(control);
            var guidEditorType = GuidList.NuGetEditorType;
            var guidCommandUI = Guid.Empty;
            var ppunkDocData = IntPtr.Zero;

            var caption = string.Format(
                CultureInfo.CurrentCulture,
                Resx.Label_NuGetWindowCaption,
                projectPath);

            vsShellInterop.IVsWindowFrame windowFrame;
            var uiShell = await _asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.SVsUIShell)) as vsShellInterop.IVsUIShell;
            Assumes.Present(uiShell);
            var hr = 0;
            int[] pfDefaultPosition = null;

            try
            {
                uint createFlag = (uint)__VSCREATETOOLWIN.CTW_fInitNew;
                hr = uiShell.CreateToolWindow(
                    createFlag,
                    0,              // dwToolWindowId - singleInstance = 0
                    windowPane,     // ToolWindowPane
                    Guid.Empty,     // rclsidTool = GUID_NULL
                    Guid.NewGuid(), // TODO: should be projectGuid...so persistance info works.
                    Guid.Empty,     // reserved - do not use - GUID_NULL
                    null,           // IServiceProvider
                    caption,
                    pfDefaultPosition,
                    out windowFrame);

                if (windowFrame != null)
                {
                    WindowFrameHelper.AddF1HelpKeyword(windowFrame, keywordValue: F1KeywordValuePmUI);
                    WindowFrameHelper.DisableWindowAutoReopen(windowFrame);
                }
            }
            finally
            {
                if (windowPane != null)
                {
                    windowPane.Dispose();
                }
            }

            ErrorHandler.ThrowOnFailure(hr);
            return windowFrame;
        }

        private async Task<vsShellInterop.IVsWindowFrame> FindExistingSolutionWindowFrameAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var uiShell = await _asyncServiceProvider.GetServiceAsync(typeof(vsShellInterop.SVsUIShell)) as vsShellInterop.IVsUIShell;
            foreach (var windowFrame in VsUtility.GetDocumentWindows(uiShell))
            {
                object property;
                var hr = windowFrame.GetProperty(
                    (int)vsShellInterop.__VSFPROPID.VSFPROPID_DocData,
                    out property);
                var packageManagerControl = VsUtility.GetPackageManagerControl(windowFrame);
                if (hr == VSConstants.S_OK
                    &&
                    property is vsShellInterop.IVsSolution
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

        /// <summary>
        /// Search for packages using the searchText.
        /// </summary>
        /// <param name="windowFrame">A window frame that hosts the PackageManagerControl.</param>
        /// <param name="searchText">Search text.</param>
        // private void Search(System.Windows.Window windowFrame, string searchText)
        private void Search(vsShellInterop.IVsWindowFrame windowFrame, string searchText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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

        private bool ShouldMEFBeInitialized()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return !_initialized;
        }

    }
}
