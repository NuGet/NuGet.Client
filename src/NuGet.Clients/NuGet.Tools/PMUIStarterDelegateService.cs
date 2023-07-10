// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Common;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.ComponentModelHost;
using NuGet.VisualStudio.Internal.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using NuGet.VisualStudio.Telemetry;
using Resx = NuGet.PackageManagement.UI.Resources;

namespace NuGetVSExtension
{
    [Export(typeof(IPMUIStarter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PMUIStarterDelegateService : IPMUIStarter
    {
        private IAsyncServiceProvider _asyncServiceProvider = AsyncServiceProvider.GlobalProvider;
        private AsyncLazy<IVsMonitorSelection> _vsMonitorSelection;
        private IVsMonitorSelection VsMonitorSelection => ThreadHelper.JoinableTaskFactory.Run(_vsMonitorSelection.GetValueAsync);
        private bool _initialized;
        private uint _solutionExistsCookie;
        private readonly ReentrantSemaphore _semaphore = ReentrantSemaphore.Create(1, NuGetUIThreadHelper.JoinableTaskFactory.Context, ReentrantSemaphore.ReentrancyMode.Freeform);
        private const string F1KeywordValuePmUI = "VS.NuGet.PackageManager.UI";

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

        public void PMUIStarter()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            HereIsHowYouStartSolution();
        }

        public void HereIsHowYouStartSolution()
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
            }).PostOnFailure(nameof(PMUIStarterDelegateService), nameof(HereIsHowYouStartSolution));
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

        private async Task<IVsWindowFrame> CreateDocWindowForSolutionAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IVsWindowFrame windowFrame = null;
            var solution = await _asyncServiceProvider.GetServiceAsync<SVsSolution, IVsSolution>();
            var uiShell = await _asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>();
            var windowFlags =
                (uint)_VSRDTFLAGS.RDT_DontAddToMRU |
                (uint)_VSRDTFLAGS.RDT_DontSaveAs;

            // when VSSolutionManager is already initialized, then use the existing APIs to check pre-conditions.
            if (!await SolutionManager.Value.IsSolutionAvailableAsync())
            {
                throw new InvalidOperationException(Resources.SolutionIsNotSaved);
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
                    MessageHelper.ShowWarningMessage(Resources.NoSupportedProjectsInSolution, Resources.ErrorDialogBoxTitle);
                    return null;
                }
            }

            INuGetUI uiController = await UIFactory.Value.CreateAsync(serviceBroker, projectContexts.ToArray());
            var solutionName = (string)_dte.Solution.Properties.Item("Name").Value;

            // This model takes ownership of --- and Dispose() responsibility for --- the INuGetUI instance.
            var model = new PackageManagerModel(
                uiController,
                isSolution: true,
                editorFactoryGuid: GuidList.guidNuGetEditorType)
            {
                SolutionName = solutionName
            };

            PackageManagerControl control = await PackageManagerControl.CreateAsync(model, OutputConsoleLogger.Value);
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
                        Resx.Label_NuGetWindowCaption,
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
                    IServiceBroker serviceBroker = await ServiceBrokerProvider.Value.GetAsync();
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
    }
}
