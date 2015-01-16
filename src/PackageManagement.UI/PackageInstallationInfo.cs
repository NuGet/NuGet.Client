using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    // This class is used to represent one of the following facts about a package:
    // - A version of the package is installed. In this case, property Version is not null. 
    //   Property IsSolution indicates if the package is installed in the solution or in a project.
    // - The package is not installed in a project/solution. In this case, property Version is null.
    public class PackageInstallationInfo : IComparable<PackageInstallationInfo>,
        INotifyPropertyChanged
    {
        private NuGetVersion _version;

        public NuGetVersion Version
        {
            get
            {
                return _version;
            }
            set
            {
                _version = value;
                UpdateDisplayText();
            }
        }

        public event EventHandler SelectedChanged;

        private bool _selected;

        public bool Selected
        {
            get
            {
                return _selected;
            }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    if (SelectedChanged != null)
                    {
                        SelectedChanged(this, EventArgs.Empty);
                    }
                    OnPropertyChanged("Selected");
                }
            }
        }

        private bool _enabled;

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged("Enabled");
                }
            }
        }

        public NuGetProject NuGetProject
        {
            get;
            private set;
        }

        private string _name;

        public PackageInstallationInfo(NuGetProject project, NuGetVersion version, bool enabled)
        {
            NuGetProject = project;
            _name = NuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            _selected = enabled;
            Version = version;
            Enabled = enabled;

            UpdateDisplayText();
        }

        private string _displayText;

        // the text to be displayed in UI
        public string DisplayText
        {
            get
            {
                return _displayText;
            }
            set
            {
                if (_displayText != value)
                {
                    _displayText = value;
                    OnPropertyChanged("DisplayText");
                }
            }
        }

        private void UpdateDisplayText()
        {
            if (Version == null)
            {
                DisplayText = _name;
            }
            else
            {
                DisplayText = string.Format("{0} ({1})", _name,
                    Version.ToNormalizedString());
            }
        }

        public int CompareTo(PackageInstallationInfo other)
        {
            return this._name.CompareTo(other._name);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }    
}
