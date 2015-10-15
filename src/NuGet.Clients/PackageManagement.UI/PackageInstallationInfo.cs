// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Globalization;
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

        public NuGetVersion InstalledVersion
        {
            get { return _version; }
            set
            {
                _version = value;
                OnPropertyChanged(nameof(InstalledVersion));
            }
        }

        public event EventHandler SelectedChanged;

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    if (SelectedChanged != null)
                    {
                        SelectedChanged(this, EventArgs.Empty);
                    }
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public NuGetProject NuGetProject { get; }        

        public PackageInstallationInfo(
            NuGetProject project)
        {
            NuGetProject = project;
            _projectName = NuGetProject.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName);
            _isSelected = false;
        }

        private string _projectName;

        public string ProjectName
        {
            get
            {
                return _projectName;
            }
            set
            {
                _projectName = value;
                OnPropertyChanged(nameof(ProjectName));
            }
        }

        private AlternativePackageManagerProviders _providers;
        public AlternativePackageManagerProviders Providers
        {
            get
            {
                return _providers;
            }

            set
            {
                _providers = value;
                OnPropertyChanged(nameof(Providers));
            }
        }

        public int CompareTo(PackageInstallationInfo other)
        {
            return string.Compare(_projectName, other._projectName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            var other = obj as PackageInstallationInfo;

            return other != null && string.Equals(_projectName, other._projectName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(_projectName);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}