// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.Options
{
    /// <summary>
    /// Interaction logic for StubGeneralOptionControl.xaml
    /// </summary>
    public partial class StubGeneralOptionControl : UserControl
    {
        public StubGeneralOptionControl()
        {
            DataContext = this;
            InitializeComponent();
        }

        private void SettingsButtonClicked(object sender, RoutedEventArgs e)
        {
            var optionsPageActivator = ServiceLocator.GetComponentModelService<IOptionsPageActivator>();
            optionsPageActivator.ActivatePage(OptionsPage.General, closeCallback: null);


            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var asyncServiceProvider = AsyncServiceProvider.GlobalProvider;
                IVsUIShell2 uiShell = await asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell2>();
                var options = await asyncServiceProvider.GetServiceAsync<SVsToolsOptions, IVsToolsOptionsPrivate2>();

                options.CloseToolsOptions(applyChanges: true);
            });
        }
    }
}
