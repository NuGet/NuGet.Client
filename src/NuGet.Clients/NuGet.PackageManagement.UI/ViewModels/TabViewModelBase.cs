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

        private bool _visible;
        public bool Visible
        {
            get => _visible;
            set
            {
                SetAndRaisePropertyChanged(ref _visible, value);
            }
        }

        public PackageMetadataTab PackageMetadataTab { get; set; }
    }
}
