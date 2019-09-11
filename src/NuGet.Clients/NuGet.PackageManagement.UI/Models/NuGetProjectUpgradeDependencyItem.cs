// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI
{
    public class NuGetProjectUpgradeDependencyItem : IPackageWithDependants, INotifyPropertyChanged
    {
        private bool _installAsTopLevel;
        public PackageIdentity Identity { get; }
        public IList<PackageIdentity> DependantPackages { get; }

        public IList<PackagingLogMessage> Issues { get; }

        public string Id { get; }

        public string Version { get; }

        public bool InstallAsTopLevel {
            get
            {
                return _installAsTopLevel;
            }
            set
            {
                _installAsTopLevel = value;
                OnPropertyChanged("InstallAsTopLevel");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public NuGetProjectUpgradeDependencyItem(PackageIdentity package, IList<PackageIdentity> dependingPackages = null)
        {
            _installAsTopLevel = true;
            Identity = package;
            Id = package.Id;
            Version = package.Version.ToNormalizedString();
            DependantPackages = dependingPackages ?? new List<PackageIdentity>();
            Issues = new List<PackagingLogMessage>();
        }

        public override string ToString()
        {
            return !DependantPackages.Any()
                ? Identity.ToString()
                : $"{Identity} {string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgrade_PackageDependencyOf, string.Join(", ", DependantPackages))}";
        }
    }
}
