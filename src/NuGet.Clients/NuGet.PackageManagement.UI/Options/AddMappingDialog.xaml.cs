// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.PlatformUI;
using NuGet.PackageManagement.UI;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Common;
using NuGet.VisualStudio.Internal.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NuGet.Options
{
    public partial class AddMappingDialog : DialogWindow
    {
        public ICommand HideDialogCommand { get; set; }

        public ICommand AddMappingCommand { get; set; }

        public ItemsChangeObservableCollection<PackageSourceViewModel> SourcesCollection { get; private set; }

        private IReadOnlyList<PackageSourceContextInfo> _originalPackageSources;

        private PackageSourceMappingOptionsControl _parent;

        public AddMappingDialog(PackageSourceMappingOptionsControl parent)
        {
            _parent = parent;

            // The use of a `JoinableTaskFactory` is not relevant as `CanExecuteChanged` will never be raised on this command.
#pragma warning disable VSTHRD012 // Provide JoinableTaskFactory where allowed
            HideDialogCommand = new DelegateCommand(ExecuteHideDialog);
#pragma warning restore VSTHRD012 // Provide JoinableTaskFactory where allowed
            AddMappingCommand = new DelegateCommand(ExecuteAddMapping, CanExecuteAddMapping, NuGetUIThreadHelper.JoinableTaskFactory);
            SourcesCollection = new ItemsChangeObservableCollection<PackageSourceViewModel>();
            DataContext = this;
            InitializeComponent();
            CancellationToken cancellationToken = new CancellationToken(false);
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () => await InitializeOnActivatedAsync(cancellationToken));
        }

        internal async Task InitializeOnActivatedAsync(CancellationToken cancellationToken)
        {
            IServiceBrokerProvider serviceBrokerProvider = await ServiceLocator.GetComponentModelServiceAsync<IServiceBrokerProvider>();
            IServiceBroker serviceBroker = await serviceBrokerProvider.GetAsync();

            using (INuGetSourcesService _nugetSourcesService = await serviceBroker.GetProxyAsync<INuGetSourcesService>(
                    NuGetServices.SourceProviderService,
                    cancellationToken: cancellationToken))
            {
                //show package sources on open
                _originalPackageSources = await _nugetSourcesService.GetPackageSourcesAsync(cancellationToken);
            }

            SourcesCollection.Clear();
            foreach (PackageSourceContextInfo source in _originalPackageSources)
            {
                var viewModel = new PackageSourceViewModel(source, false);
                SourcesCollection.Add(viewModel);
            }
        }

        private void ExecuteHideDialog(object parameter)
        {
            Close();
        }

        private void ExecuteAddMapping(object parameter)
        {
            Close();

            // Does not add mapping if package ID is null.
            if (!string.IsNullOrWhiteSpace(_packageID.Text))
            {
                string packageId = _packageID.Text;
                List<PackageSourceContextInfo> packageSources = new List<PackageSourceContextInfo>();
                foreach (PackageSourceViewModel source in _sourcesListBox.Items)
                {
                    if (source.IsChecked)
                    {
                        packageSources.Add(source.SourceInfo);
                    }
                }
                var viewModel = new SourceMappingViewModel(packageId, packageSources);
                _parent.SourceMappingsCollection.Add(viewModel);
            }

            (_parent.ShowAddDialogCommand as DelegateCommand).RaiseCanExecuteChanged();
            (_parent.RemoveMappingCommand as DelegateCommand).RaiseCanExecuteChanged();
            (_parent.ClearMappingsCommand as DelegateCommand).RaiseCanExecuteChanged();
        }

        private bool CanExecuteAddMapping(object parameter)
        {
            if (string.IsNullOrWhiteSpace(_packageID.Text))
            {
                return false;
            }

            foreach (PackageSourceViewModel source in _sourcesListBox.Items)
            {
                if (source.IsChecked)
                {
                    return true;
                }
            }
            return false;
        }

        // Allows the user to drag the window around
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }

        private void CheckBox_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            (AddMappingCommand as DelegateCommand).RaiseCanExecuteChanged();
        }

        private void PackageID_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            (AddMappingCommand as DelegateCommand).RaiseCanExecuteChanged();
        }
    }
}
