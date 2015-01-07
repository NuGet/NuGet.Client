using NuGet.Client;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public enum PackageStatus
    {
        NotInstalled,
        Installed,
        UpdateAvailable
    }

    public class UiSearchResultPackage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public string Summary { get; set; }

        private PackageStatus _status;

        public PackageStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                if (_status != value)
                {
                    _status = value;
                }
                OnPropertyChanged("Status");
            }
        }

        public Uri IconUrl { get; set; }

        public IEnumerable<NuGetVersion> Versions { get; set; }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        private SourceRepository _source;

        public UiSearchResultPackage(SourceRepository source)
        {
            _source = source;
        }
    }
}