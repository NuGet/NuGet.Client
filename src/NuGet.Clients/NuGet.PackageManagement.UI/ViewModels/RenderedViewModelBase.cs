namespace NuGet.PackageManagement.UI.ViewModels
{
    public class RenderedViewModelBase : ViewModelBase
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
    }
}
