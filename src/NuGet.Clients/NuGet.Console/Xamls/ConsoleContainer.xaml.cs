// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Common;
using NuGet.Common.Telemetry;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Common;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGetConsole
{
    /// <summary>
    /// Interaction logic for ConsoleContainer.xaml
    /// </summary>
    public sealed partial class ConsoleContainer : UserControl, IDisposable
    {
        private INuGetSolutionManagerService _solutionManager;
        private IVsSolutionManager _iVsSolutionManager;
        private INuGetSolutionTelemetry _nugetSolutionTelemetry;
        private int _windowLoadCount;
        private bool _isTelemetryEmitted;

        private const string PackageManagerConsoleWindowsLoad = "PackageManagerConsoleWindowsLoad";
        private const string NuGetPMCWindowLoadCount = "NuGetPMCWindowLoadCount";
        private const string ReOpenAtStart = "ReOpenAtStart";

        public ConsoleContainer()
        {
            InitializeComponent();
            Loaded += ConsoleContainer_Loaded;

            ThreadHelper.JoinableTaskFactory.StartOnIdle(
                async () =>
                {
                    await System.Threading.Tasks.Task.Run(
                        async () =>
                        {
                            IServiceBrokerProvider serviceBrokerProvider = await ServiceLocator.GetInstanceAsync<IServiceBrokerProvider>();
                            IServiceBroker serviceBroker = await serviceBrokerProvider.GetAsync();

                            _solutionManager = await serviceBroker.GetProxyAsync<INuGetSolutionManagerService>(
                                NuGetServices.SolutionManagerService,
                                cancellationToken: CancellationToken.None);

                            Assumes.NotNull(_solutionManager);

                            _iVsSolutionManager = await ServiceLocator.GetInstanceAsync<IVsSolutionManager>();
                            Assumes.NotNull(_iVsSolutionManager);

                            // Hook up solution events
                            _iVsSolutionManager.SolutionClosing += (o, e) =>
                            {
                                EmitPowershellUsageTelemetry();
                            };

                            var productUpdateService = ServiceLocator.GetInstance<IProductUpdateService>();
                            var packageRestoreManager = ServiceLocator.GetInstance<IPackageRestoreManager>();
                            var deleteOnRestartManager = ServiceLocator.GetInstance<IDeleteOnRestartManager>();
                            var shell = ServiceLocator.GetGlobalService<SVsShell, IVsShell4>();
                            _nugetSolutionTelemetry = ServiceLocator.GetInstanceSafe<INuGetSolutionTelemetry>();

                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                            RootLayout.Children.Add(new ProductUpdateBar(productUpdateService));
                            RootLayout.Children.Add(new PackageRestoreBar(_solutionManager, packageRestoreManager));
                            RootLayout.Children.Add(new RestartRequestBar(deleteOnRestartManager, shell));
                        });
                }, VsTaskRunContext.UIThreadIdlePriority);

            // Set DynamicResource binding in code
            // The reason we can't set it in XAML is that the VsBrushes class come from either
            // Microsoft.VisualStudio.Shell.10 or Microsoft.VisualStudio.Shell.11 assembly,
            // depending on whether NuGet runs inside VS10 or VS11.
            InitializeText.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.WindowTextKey);
        }

        public void AddConsoleEditor(UIElement content)
        {
            Grid.SetRow(content, 1);
            RootLayout.Children.Add(content);
        }

        public void NotifyInitializationCompleted()
        {
            RootLayout.Children.Remove(InitializeText);
        }

        public void Dispose()
        {
            if (!_isTelemetryEmitted)
            {
                // Work around to detect if PMC loaded automatically because it was last focused window.
                var reopenAtStart = IsLoaded;
                var telemetryEvent = new TelemetryEvent(PackageManagerConsoleWindowsLoad, new Dictionary<string, object>
                                {
                                    { NuGetPMCWindowLoadCount, _windowLoadCount}, // Useful if PMC used without any solution load at all then VS instance closed.
                                    { ReOpenAtStart, reopenAtStart}
                                });

                _nugetSolutionTelemetry.AddSolutionTelemetryEvent(telemetryEvent);

                _isTelemetryEmitted = true;
            }

            Loaded -= ConsoleContainer_Loaded;

            // Use more verbose null-checking syntax to avoid ISB001 misfiring.
            if (_solutionManager != null)
            {
                _solutionManager.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        void ConsoleContainer_Loaded(object sender, RoutedEventArgs e)
        {
            _windowLoadCount++;
        }

        private void EmitPowershellUsageTelemetry()
        {
            var telemetryEvent = new TelemetryEvent(PackageManagerConsoleWindowsLoad, new Dictionary<string, object>
                                {
                                    { NuGetPMCWindowLoadCount, _windowLoadCount},
                                });
            _nugetSolutionTelemetry.AddSolutionTelemetryEvent(telemetryEvent);

            _windowLoadCount = 0;
        }
    }
}
