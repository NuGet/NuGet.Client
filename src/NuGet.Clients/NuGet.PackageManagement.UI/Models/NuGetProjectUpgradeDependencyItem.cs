// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;
using System.Globalization;
using System.Linq;
using NuGet.Common;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NuGet.PackageManagement.UI
{
    public class NuGetProjectUpgradeDependencyItem : INotifyPropertyChanged
    {
        private bool _promoteToTopLevel;
        public PackageIdentity Package { get; }
        public IList<PackageIdentity> DependingPackages { get; }

        public IList<PackLogMessage> Issues { get; }

        public string Id { get; }

        public string Version { get; }

        public bool PromoteToTopLevel {
            get
            {
                return _promoteToTopLevel;
            }
            set
            {
                _promoteToTopLevel = value;
                OnPropertyChanged("PromoteToTopLevel");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public NuGetProjectUpgradeDependencyItem(PackageIdentity package, IList<PackageIdentity> dependingPackages = null)
        {
            _promoteToTopLevel = true;
            Package = package;
            Id = package.Id;
            Version = package.Version.ToNormalizedString();
            DependingPackages = dependingPackages ?? new List<PackageIdentity>();
            Issues = new List<PackLogMessage>();
        }

        public override string ToString()
        {
            return !DependingPackages.Any()
                ? Package.ToString()
                : $"{Package} {string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgrade_PackageDependencyOf, string.Join(", ", DependingPackages))}";
        }
    }
}