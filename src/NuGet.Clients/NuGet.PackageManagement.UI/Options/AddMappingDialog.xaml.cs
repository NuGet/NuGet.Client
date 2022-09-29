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
        public ICommand HideButtonCommand { get; set; }

        public ICommand AddButtonCommand { get; set; }

        public ItemsChangeObservableCollection<PackageSourceViewModel> SourcesCollection { get; private set; }

        private IReadOnlyList<PackageSourceContextInfo> _originalPackageSources;

        private PackageSourceMappingOptionsControl _parent;

        public AddMappingDialog(PackageSourceMappingOptionsControl parent)
        {
            _parent = parent;
            HideButtonCommand = new ButtonCommand(ExecuteHideButtonCommand, CanExecuteHideButtonCommand);
            AddButtonCommand = new ButtonCommand(ExecuteAddButtonCommand, CanExecuteAddButtonCommand);
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
                var tempSource = new PackageSourceViewModel(source, false);
                SourcesCollection.Add(tempSource);
            }
        }

        private void ExecuteHideButtonCommand(object parameter)
        {
            Close();
            (_parent.ShowButtonCommand as ButtonCommand).InvokeCanExecuteChanged();
        }

        private bool CanExecuteHideButtonCommand(object parameter)
        {
            return true;
        }

        private void ExecuteAddButtonCommand(object parameter)
        {
            Close();
            //does not add mapping if package ID is null
            if (!string.IsNullOrEmpty(packageID.Text))
            {
                string tempPkgID = packageID.Text;
                List<PackageSourceContextInfo> tempSources = new List<PackageSourceContextInfo>();
                foreach (PackageSourceViewModel source in sourcesListBox.Items)
                {
                    if (source.IsChecked)
                    {
                        tempSources.Add(source.SourceInfo);
                    }
                }
                var tempPkg = new SourceMappingViewModel(tempPkgID, tempSources);
                _parent.SourceMappingsCollection.Add(tempPkg);
            }
            (_parent.ShowButtonCommand as ButtonCommand).InvokeCanExecuteChanged();
            (_parent.RemoveButtonCommand as ButtonCommand).InvokeCanExecuteChanged();
            (_parent.ClearButtonCommand as ButtonCommand).InvokeCanExecuteChanged();
        }

        private bool CanExecuteAddButtonCommand(object parameter)
        {
            foreach (PackageSourceViewModel source in sourcesListBox.Items)
            {
                if (source.IsChecked)
                {
                    if (!string.IsNullOrEmpty(packageID.Text))
                    {
                        return true;
                    }
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
            (AddButtonCommand as ButtonCommand).InvokeCanExecuteChanged();
        }

        private void PackageID_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            (AddButtonCommand as ButtonCommand).InvokeCanExecuteChanged();
        }
    }
}
