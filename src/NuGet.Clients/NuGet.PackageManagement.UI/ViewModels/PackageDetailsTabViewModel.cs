using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class PackageDetailsTabViewModel : ViewModelBase, IDisposable
    {
        private bool _readmeTabEnabled;
        public PackageDetailsTabViewModel()
        {
            _readmeTabEnabled = true;
            Tabs = new ObservableCollection<TabViewModelBase>();
        }

        public void Initialize(DetailControlModel detailControlModel, INuGetPackageFileService nugetPackageFileService, ItemFilter currentFilter, bool readmeTabAvailable)
        {
            ReadmePreviewViewModel = new ReadmePreviewViewModel(nugetPackageFileService, currentFilter);
            ReadmePreviewViewModel.IsVisible = readmeTabAvailable;
            DetailControlModel = detailControlModel;
            _readmeTabEnabled = readmeTabAvailable;

            Tabs.Add(ReadmePreviewViewModel);
            Tabs.Add(DetailControlModel);

            DetailControlModel.PropertyChanged += DetailControlModel_PropertyChanged;
            ReadmePreviewViewModel.PropertyChanged += ReadMePreviewViewModel_PropertyChanged;
        }

        public async Task SetCurrentFilterAsync(ItemFilter filter)
        {
            if (_readmeTabEnabled)
            {
                await ReadmePreviewViewModel.SetCurrentFilterAsync(filter);
            }
        }

        private void ReadMePreviewViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ReadmePreviewViewModel is not null
                && e.PropertyName == nameof(ReadmePreviewViewModel.ReadmeMarkdown))
            {
                Tabs[0].IsVisible = !string.IsNullOrWhiteSpace(ReadmePreviewViewModel.ReadmeMarkdown);
                if (!Tabs[0].IsVisible)
                {
                    SelectedTab = Tabs[1];
                }
            }
        }

        private void DetailControlModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                if (_readmeTabEnabled)
                {
                    await ReadmePreviewViewModel.SetPackageMetadataAsync(DetailControlModel.PackageMetadata, CancellationToken.None);
                }
            });
        }

        public async Task SetPackageMetadataAsync(DetailControlModel detailControlModel, CancellationToken cancellationToken)
        {
            if (_readmeTabEnabled)
            {
                await ReadmePreviewViewModel.SetPackageMetadataAsync(detailControlModel.PackageMetadata, cancellationToken);
                RaisePropertyChanged(nameof(ReadmePreviewViewModel));
            }

            DetailControlModel.PropertyChanged -= DetailControlModel_PropertyChanged;
            Tabs[1] = detailControlModel;
            DetailControlModel = detailControlModel;
            DetailControlModel.PropertyChanged += DetailControlModel_PropertyChanged;
            RaisePropertyChanged(nameof(DetailControlModel));
        }

        private bool _disposed = false;
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            DetailControlModel.PropertyChanged -= DetailControlModel_PropertyChanged;
        }

        internal void SelectTab(PackageMetadataTab selectedPackageMetadataTab)
        {
            switch (selectedPackageMetadataTab)
            {
                case PackageMetadataTab.Readme:
                    SelectedTab = Tabs[0];
                    break;
                case PackageMetadataTab.PackageDetails:
                    SelectedTab = Tabs[1];
                    break;
            }
        }

        public ReadmePreviewViewModel ReadmePreviewViewModel { get; private set; }

        public DetailControlModel DetailControlModel { get; private set; }

        public ObservableCollection<TabViewModelBase> Tabs { get; private set; }

        private TabViewModelBase _selectedTab;
        public TabViewModelBase SelectedTab
        {
            get => _selectedTab;
            set
            {
                SetAndRaisePropertyChanged(ref _selectedTab, value);
            }
        }
    }
}
