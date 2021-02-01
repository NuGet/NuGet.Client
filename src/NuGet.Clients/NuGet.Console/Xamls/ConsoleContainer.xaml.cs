// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.UI;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Common;
using NuGet.VisualStudio.Common.Telemetry.PowerShell;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGetConsole
{
    /// <summary>
    /// Interaction logic for ConsoleContainer.xaml
    /// </summary>
    public sealed partial class ConsoleContainer : UserControl, IDisposable
    {
        private INuGetSolutionManagerService _solutionManager;

        public ConsoleContainer()
        {
            InitializeComponent();

            Loaded += ConsoleContainer_Loaded;
            Unloaded += ConsoleContainer_UnLoaded;

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

                            var productUpdateService = ServiceLocator.GetInstance<IProductUpdateService>();
                            var packageRestoreManager = ServiceLocator.GetInstance<IPackageRestoreManager>();
                            var deleteOnRestartManager = ServiceLocator.GetInstance<IDeleteOnRestartManager>();
                            var shell = ServiceLocator.GetGlobalService<SVsShell, IVsShell4>();

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
            Loaded -= ConsoleContainer_Loaded;
            Unloaded -= ConsoleContainer_UnLoaded;

            // Use more verbose null-checking syntax to avoid ISB001 misfiring.
            if (_solutionManager != null)
            {
                _solutionManager.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        void ConsoleContainer_Loaded(object sender, RoutedEventArgs e)
        {
            NuGetPowerShellUsage.RaisePmcWindowsLoadEvent(isLoad: true);
        }

        void ConsoleContainer_UnLoaded(object sender, RoutedEventArgs e)
        {
            NuGetPowerShellUsage.RaisePmcWindowsLoadEvent(isLoad: false);
        }
    }
}
