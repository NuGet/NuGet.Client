using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class LicenseFileText : IText, INotifyPropertyChanged
    {
        private string _text;
        private string _licenseText;
        private Task<string> _licenseFileContent;

        public LicenseFileText(string text, Task<string> licenseFileContent)
        {
            _text = text;
            _licenseText = "Loading license file";
            _licenseFileContent = licenseFileContent;
        }

        internal void LoadLicenseFileAsync()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                var content = await _licenseFileContent;
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LicenseText = content;
            });
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged("Text");
            }
        }

        public string LicenseText
        {
            get => _licenseText;
            set
            {
                _licenseText = value;
                OnPropertyChanged("LicenseText");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
