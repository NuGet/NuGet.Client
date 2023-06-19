// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;
using NuGet.VisualStudio;
using Microsoft.ServiceHub.Framework;
using Microsoft;
using NuGet.PackageManagement.UI;
using NuGet.VisualStudio.Common;
using NuGet.VisualStudio.Internal.Contracts;
using System.Runtime.InteropServices;
using System.Threading;
using System;
using NuGet.PackageManagement.VisualStudio;
using System.Collections.Generic;
using System.Linq;
using NuGet.VisualStudio.Telemetry;
using NuGet.PackageManagement;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.ProjectManagement;
using System.Globalization;
using EnvDTE;
using Microsoft.Internal.VisualStudio.PlatformUI;

namespace NuGet.SolutionRestoreManager
{
    public class InfoBarService : BaseInfoBar, IDisposable
    {
        public static InfoBarService Instance { get; private set; }

        private const string F1KeywordValuePmUI = "VS.NuGet.PackageManager.UI";
        private bool _visible;
        //private uint _cookie;

        private AsyncLazy<IVsMonitorSelection> _vsMonitorSelection;
        private IVsMonitorSelection VsMonitorSelection => ThreadHelper.JoinableTaskFactory.Run(_vsMonitorSelection.GetValueAsync);
        private bool _initialized;
        private uint _solutionExistsCookie;
        private readonly ReentrantSemaphore _semaphore = ReentrantSemaphore.Create(1, NuGetUIThreadHelper.JoinableTaskFactory.Context, ReentrantSemaphore.ReentrancyMode.Freeform);


        private DTE _dte;
        private IDisposable ProjectRetargetingHandler { get; set; }
        private IDisposable ProjectUpgradeHandler { get; set; }
        private Lazy<IDeleteOnRestartManager> DeleteOnRestartManager { get; set; }
        private Lazy<IVsSolutionManager> SolutionManager { get; set; }
        private Lazy<INuGetExperimentationService> NuGetExperimentationService { get; set; }
        private Lazy<SolutionUserOptions> SolutionUserOptions { get; set; }
        private Lazy<INuGetProjectContext> ProjectContext { get; set; }
        private Lazy<IServiceBrokerProvider> ServiceBrokerProvider { get; set; }
        private Lazy<INuGetUIFactory> UIFactory { get; set; }
        private Lazy<INuGetUILogger> OutputConsoleLogger { get; set; }

        public static void Initialize(IAsyncServiceProvider asyncServiceProvider)
        {
            Instance = new InfoBarService(asyncServiceProvider);
        }

        public InfoBarService(IAsyncServiceProvider asyncServiceProvider)
            : base(asyncServiceProvider)
        {
            _vsMonitorSelection = new AsyncLazy<IVsMonitorSelection>(
                async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // get the UI context cookie for the debugging mode
                    var vsMonitorSelection = await _asyncServiceProvider.GetServiceAsync<IVsMonitorSelection, IVsMonitorSelection>();
                    Assumes.Present(vsMonitorSelection);

                    var guidCmdUI = VSConstants.UICONTEXT.SolutionExists_guid;
                    vsMonitorSelection.GetCmdUIContextCookie(
                        ref guidCmdUI, out _solutionExistsCookie);

                    return vsMonitorSelection;
                },
                ThreadHelper.JoinableTaskFactory);

            var componentModel = NuGetUIThreadHelper.JoinableTaskFactory.Run(ServiceLocator.GetComponentModelAsync);
            SolutionManager = new Lazy<IVsSolutionManager>(() => componentModel.GetService<IVsSolutionManager>());
            ProjectContext = new Lazy<INuGetProjectContext>(() => componentModel.GetService<INuGetProjectContext>());
            SolutionUserOptions = new Lazy<SolutionUserOptions>(() => componentModel.GetService<SolutionUserOptions>());
            NuGetExperimentationService = new Lazy<INuGetExperimentationService>(() => componentModel.GetService<INuGetExperimentationService>());
            DeleteOnRestartManager = new Lazy<IDeleteOnRestartManager>(() => componentModel.GetService<IDeleteOnRestartManager>());
            ServiceBrokerProvider = new Lazy<IServiceBrokerProvider>(() => componentModel.GetService<IServiceBrokerProvider>());
            UIFactory = new Lazy<INuGetUIFactory>(() => componentModel.GetService<INuGetUIFactory>());
            OutputConsoleLogger = new Lazy<INuGetUILogger>(() => componentModel.GetService<INuGetUILogger>());
        }

        protected override void OnClosed(bool closeFromHide)
        {
            if (!closeFromHide)
            {
                _visible = false;
            }
        }

        protected override async Task<IVsInfoBarHost> GetInfoBarHostAsync(CancellationToken cancellationToken)
        {
            var uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>(throwOnFailure: false);
            if (uiShell == null)
            {
                return null;
            }

            // Ensure that we are on the UI thread before interacting with the Solution Explorer UI Element
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (ErrorHandler.Failed(uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst, VSConstants.StandardToolWindows.SolutionExplorer, out var windowFrame)))
            {
                return null;
            }

            object tempObject;
            if (ErrorHandler.Failed(windowFrame.GetProperty((int)__VSFPROPID7.VSFPROPID_InfoBarHost, out tempObject)))
            {
                return null;
            }

            return tempObject as IVsInfoBarHost;
        }

        protected override InfoBarModel GetInfoBarModel()
        {
            return new InfoBarModel(
                new IVsInfoBarTextSpan[] {
                    new InfoBarTextSpan("This solution contains packages with vulnerabilities."),
                },
                new IVsInfoBarActionItem[] {
                    new InfoBarHyperlink("Open package manager", "Open package manager"),
                },
                KnownMonikers.StatusWarning);
        }

        public async Task RefreshAsync(bool vulnerabilitiesFound, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (vulnerabilitiesFound && !_visible)
            {
                await ShowAsync(cancellationToken);
            }
            else
            {
                await HideAsync(cancellationToken);
            }
        }

        protected override void InvokeAction(string action)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (ShouldInitializeSolutionExperiences())
                {
                    await InitializeSolutionExperiencesAsync();
                }

                var windowFrame = await FindExistingSolutionWindowFrameAsync();
                if (windowFrame == null)
                {
                    // Create the window frame
                    windowFrame = await CreateDocWindowForSolutionAsync();
                }

                if (windowFrame != null)
                {
                    windowFrame.Show();
                }
            }).PostOnFailure(nameof(InfoBarService), nameof(OnActionItemClicked));
        }

        private async Task<IVsWindowFrame> FindExistingSolutionWindowFrameAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>();
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

        public async Task<IVsWindowFrame> CreateDocWindowForSolutionAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsWindowFrame windowFrame = null;
            var solution = await _asyncServiceProvider.GetServiceAsync<SVsSolution, IVsSolution>();
            var uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>();
            var windowFlags =
                (uint)_VSRDTFLAGS.RDT_DontAddToMRU |
                (uint)_VSRDTFLAGS.RDT_DontSaveAs;

            var dte = await _asyncServiceProvider.GetDTEAsync();

            // when VSSolutionManager is already initialized, then use the existing APIs to check pre-conditions.
            if (!await SolutionManager.Value.IsSolutionAvailableAsync())
            {
                throw new InvalidOperationException("Please save your solution before managing NuGet packages..");
            }

            IServiceBroker serviceBroker = await ServiceBrokerProvider.Value.GetAsync();
            IReadOnlyCollection<IProjectContextInfo> projectContexts;

            using (INuGetProjectManagerService projectManagerService = await serviceBroker.GetProxyAsync<INuGetProjectManagerService>(
                NuGetServices.ProjectManagerService))
            {
                Assumes.NotNull(projectManagerService);
                projectContexts = await projectManagerService.GetProjectsAsync(CancellationToken.None);

                if (projectContexts.Count == 0)
                {
                    MessageHelper.ShowWarningMessage("No projects supported by NuGet in the solution..", "Operation failed.");
                    return null;
                }
            }

            INuGetUI uiController = await UIFactory.Value.CreateAsync(serviceBroker, projectContexts.ToArray());
            var solutionName = (string)dte.Solution.Properties.Item("Name").Value;

            // This model takes ownership of --- and Dispose() responsibility for --- the INuGetUI instance.
#pragma warning disable CA2000 // Dispose objects before losing scope
            var model = new PackageManagerModel(
                uiController,
                isSolution: true,
                editorFactoryGuid: Guid.Parse("95501c48-a850-47c1-a785-2aaa96637f81"))
            {
                SolutionName = solutionName
            };
#pragma warning restore CA2000 // Dispose objects before losing scope

            PackageManagerControl control = await PackageManagerControl.CreateAsync(model, OutputConsoleLogger.Value);
#pragma warning disable CA2000 // Dispose objects before losing scope
            var windowPane = new PackageManagerWindowPane(control);
#pragma warning restore CA2000 // Dispose objects before losing scope
            var guidEditorType = Guid.Parse("95501c48-a850-47c1-a785-2aaa96637f81");
            var guidCommandUI = Guid.Empty;
            var caption = "NuGet - Solution"; // TODO
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
                    WindowFrameHelper.DisableWindowAutoReopen(windowFrame);
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

        private bool ShouldInitializeSolutionExperiences()
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

        private async Task InitializeSolutionExperiencesAsync()
        {
            await _semaphore.ExecuteAsync(async () =>
            {
                if (_initialized)
                {
                    return;
                }

                var componentModel = await _asyncServiceProvider.GetFreeThreadedServiceAsync<SComponentModel, IComponentModel>();
                Assumes.Present(componentModel);

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                SolutionManager.Value.AfterNuGetProjectRenamed += SolutionManager_NuGetProjectRenamed;

                Brushes.LoadVsBrushes(NuGetExperimentationService.Value);

                _dte = await _asyncServiceProvider.GetDTEAsync();
                Assumes.Present(_dte);

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

                IVsTrackProjectRetargeting vsTrackProjectRetargeting = await _asyncServiceProvider.GetServiceAsync<SVsTrackProjectRetargeting, IVsTrackProjectRetargeting>();
                IVsMonitorSelection vsMonitorSelection = await _asyncServiceProvider.GetServiceAsync<IVsMonitorSelection, IVsMonitorSelection>(throwOnFailure: false);
                var serviceProvider = await ServiceLocator.GetServiceProviderAsync();

                ProjectRetargetingHandler = new ProjectRetargetingHandler(
                    _dte,
                    SolutionManager.Value,
                    serviceProvider,
                    componentModel,
                    vsTrackProjectRetargeting,
                    vsMonitorSelection);

                IVsSolution2 vsSolution2 = await _asyncServiceProvider.GetServiceAsync<SVsSolution, IVsSolution2>();
                ProjectUpgradeHandler = new ProjectUpgradeHandler(
                    SolutionManager.Value,
                    vsSolution2);

                SolutionUserOptions.Value.LoadSettings();

                _initialized = true;
            });
        }

        // Missing caption
        private void SolutionManager_NuGetProjectRenamed(object sender, NuGetProjectEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var project = await SolutionManager.Value.GetVsProjectAdapterAsync(
                    await SolutionManager.Value.GetNuGetProjectSafeNameAsync(e.NuGetProject));
                var windowFrame = await FindExistingWindowFrameAsync(project.Project);
                if (windowFrame != null)
                {
                    windowFrame.SetProperty((int)__VSFPROPID.VSFPROPID_OwnerCaption, string.Format(
                        CultureInfo.CurrentCulture,
                        "NuGet: {0}",
                        project.ProjectName));
                }
            });
        }

        private async Task<IVsWindowFrame> FindExistingWindowFrameAsync(Project project)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>();
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

                    IProjectContextInfo existingProject = projects.First();
                    IServiceBrokerProvider serviceBrokerProvider = await ServiceLocator.GetComponentModelServiceAsync<IServiceBrokerProvider>();
                    IServiceBroker serviceBroker = await serviceBrokerProvider.GetAsync();
                    IProjectMetadataContextInfo projectMetadata = await existingProject.GetMetadataAsync(
                        serviceBroker,
                        CancellationToken.None);

                    if (string.Equals(projectMetadata.Name, project.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return windowFrame;
                    }
                }
            }

            return null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _semaphore.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
