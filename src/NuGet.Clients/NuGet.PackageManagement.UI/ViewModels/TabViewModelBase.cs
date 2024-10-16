namespace NuGet.PackageManagement.UI.ViewModels
{
    public class TabViewModelBase : ViewModelBase
    {
        private string _header;
        public string Header
        {
            get => _header;
            set
            {
                SetAndRaisePropertyChanged(ref _header, value);
            }
        }

        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                SetAndRaisePropertyChanged(ref _isVisible, value);
            }
        }

        public PackageMetadataTab PackageMetadataTab { get; set; }
    }
}
