using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        public ObservableCollection<TitledPageViewModelBase> Tabs { get; private set; }

        private TitledPageViewModelBase _selectedTab;
        public TitledPageViewModelBase SelectedTab
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
            Tabs = new ObservableCollection<TitledPageViewModelBase>();
        }

        public PackageMetadataTab GetSelectedTab()
        {
            return ConvertFromVm(SelectedTab);
        }

        public async Task InitializeAsync(DetailControlModel detailControlModel, INuGetPackageFileService nugetPackageFileService, ItemFilter currentFilter, PackageMetadataTab initialSelectedTab)
        {
            var nuGetFeatureFlagService = await ServiceLocator.GetComponentModelServiceAsync<INuGetFeatureFlagService>();
            _readmeTabEnabled = await nuGetFeatureFlagService.IsFeatureEnabledAsync(NuGetFeatureFlagConstants.RenderReadmeInPMUI);

            ReadmePreviewViewModel = new ReadmePreviewViewModel(nugetPackageFileService, currentFilter, _readmeTabEnabled);
            DetailControlModel = detailControlModel;

            Tabs.Add(ReadmePreviewViewModel);
            Tabs.Add(DetailControlModel);

            SelectedTab = Tabs.FirstOrDefault(t => t.IsVisible && ConvertFromVm(t) == initialSelectedTab) ?? Tabs.FirstOrDefault(t => t.IsVisible);

            DetailControlModel.PropertyChanged += DetailControlModel_PropertyChanged;
            ReadmePreviewViewModel.PropertyChanged += IsVisible_PropertyChanged;
        }

        private static PackageMetadataTab ConvertFromVm(TitledPageViewModelBase vm)
        {
            if (vm is DetailControlModel)
            {
                return PackageMetadataTab.PackageDetails;
            }
            return PackageMetadataTab.Readme;
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

        private void IsVisible_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TitledPageViewModelBase.IsVisible))
            {
                if (!SelectedTab.IsVisible)
                {
                    SelectedTab = Tabs.FirstOrDefault(t => t.IsVisible);
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
            IsVisible_PropertyChanged(sender, e);
        }
    }
}
