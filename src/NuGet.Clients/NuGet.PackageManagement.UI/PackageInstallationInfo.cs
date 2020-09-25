// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    // This class is used to represent one of the following facts about a package:
    // - A version of the package is installed. In this case, property Version is not null.
    //   Property IsSolution indicates if the package is installed in the solution or in a project.
    // - The package is not installed in a project/solution. In this case, property Version is null.
    public class PackageInstallationInfo : IComparable<PackageInstallationInfo>, INotifyPropertyChanged
    {
        public event EventHandler SelectedChanged;

        private NuGetVersion _version;
        private bool _autoReferenced;
        private bool _isSelected;
        private string _projectName;
        private AlternativePackageManagerProviders _providers;

        private PackageInstallationInfo(IProjectContextInfo project)
        {
            NuGetProject = project;
        }

        public static async ValueTask<PackageInstallationInfo> CreateAsync(IProjectContextInfo project, CancellationToken cancellationToken)
        {
            var packageInstallationInfo = new PackageInstallationInfo(project);
            await packageInstallationInfo.InitializeAsync(cancellationToken);
            return packageInstallationInfo;
        }

        private async ValueTask InitializeAsync(CancellationToken cancellationToken)
        {
            _projectName = await NuGetProject.GetUniqueNameOrNameAsync(cancellationToken);
        }

        public NuGetVersion InstalledVersion
        {
            get => _version;
            set
            {
                _version = value;
                OnPropertyChanged(nameof(InstalledVersion));
            }
        }

        public bool AutoReferenced
        {
            get => _autoReferenced;
            set
            {
                _autoReferenced = value;
                OnPropertyChanged(nameof(AutoReferenced));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    SelectedChanged?.Invoke(this, EventArgs.Empty);
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public IProjectContextInfo NuGetProject { get; }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                _projectName = value;
                OnPropertyChanged(nameof(ProjectName));
            }
        }

        public AlternativePackageManagerProviders Providers
        {
            get => _providers;

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

        public override string ToString()
        {
            return $"{ProjectName} {InstalledVersion?.Version.ToString() ?? Resources.Text_NotInstalled}";
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
