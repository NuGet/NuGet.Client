using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class PackageDetailsTabViewModel : ViewModelBase, IDisposable
    {
        private bool _disposed = false;

        private bool _readmeTabEnabled;

        public ReadmePreviewViewModel ReadmePreviewViewModel { get; private set; }

        public DetailControlModel DetailControlModel { get; private set; }

        public ObservableCollection<RenderedViewModelBase> Tabs { get; private set; }

        private RenderedViewModelBase _selectedTab;
        public RenderedViewModelBase SelectedTab
        {
            get => _selectedTab;
            set
            {
                SetAndRaisePropertyChanged(ref _selectedTab, value);
            }
        }

        public PackageDetailsTabViewModel()
        {
            _readmeTabEnabled = true;
            Tabs = new ObservableCollection<RenderedViewModelBase>();
        }

        public PackageMetadataTab GetSelectedTab()
        {
            return SelectedTab is DetailControlModel ? PackageMetadataTab.PackageDetails : PackageMetadataTab.Readme;
        }

        public async Task InitializeAsync(DetailControlModel detailControlModel, INuGetPackageFileService nugetPackageFileService, ItemFilter currentFilter, PackageMetadataTab initialSelectedTab)
        {
            var nuGetFeatureFlagService = await ServiceLocator.GetComponentModelServiceAsync<INuGetFeatureFlagService>();
            _readmeTabEnabled = await nuGetFeatureFlagService.IsFeatureEnabledAsync(NuGetFeatureFlagConstants.RenderReadmeInPMUI);

            ReadmePreviewViewModel = new ReadmePreviewViewModel(nugetPackageFileService, currentFilter);
            ReadmePreviewViewModel.IsVisible = _readmeTabEnabled;
            DetailControlModel = detailControlModel;

            Tabs.Add(ReadmePreviewViewModel);
            Tabs.Add(DetailControlModel);

            SelectedTab = _readmeTabEnabled && initialSelectedTab == PackageMetadataTab.Readme
                ? ReadmePreviewViewModel
                : DetailControlModel;

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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            DetailControlModel.PropertyChanged -= DetailControlModel_PropertyChanged;
        }

        private void ReadMePreviewViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ReadmePreviewViewModel is not null
                && e.PropertyName == nameof(ReadmePreviewViewModel.IsVisible))
            {
                if (!ReadmePreviewViewModel.IsVisible)
                {
                    SelectedTab = DetailControlModel;
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
    }
}
