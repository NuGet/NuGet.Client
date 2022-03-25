// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents an action performed in PM UI regarding a package, along with telemetry data
    /// </summary>
    public class UserAction
    {
        private UserAction(NuGetOperationType action, IEnumerable<PackageIdentity> packages, bool isSolutionLevel, ItemFilter activeTab, UIOperationSource uiSource, PackageLevel packageLevel, int topLevelPackagesCount, int transitivePackagesCount)
        {
            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            Action = action;
            UIOperationsource = uiSource;
            ActiveTab = activeTab;
            IsSolutionLevel = isSolutionLevel;
            PackagesInvolved = packages;
            TopLevelPackagesCount = topLevelPackagesCount;
            TransitivePackagesCount = transitivePackagesCount;
            SelectedPackageLevel = packageLevel;
        }

        public bool IsSolutionLevel { get; private set; }
        public ItemFilter ActiveTab { get; private set; }
        public UIOperationSource UIOperationsource { get; private set; }
        public NuGetOperationType Action { get; private set; }
        public IEnumerable<PackageIdentity> PackagesInvolved { get; private set; }
        public int TopLevelPackagesCount { get; private set; }
        public int TransitivePackagesCount { get; private set; }
        public string PackageId => PackagesInvolved.First().Id;
        public NuGetVersion Version => PackagesInvolved.First().Version;
        public PackageLevel SelectedPackageLevel { get; private set; }

        public static UserAction CreateInstallAction(string packageId, NuGetVersion packageVersion, bool isSolutionLevel, ItemFilter activeTab, UIOperationSource uiSource, PackageLevel selectedPackageLevel, int topLevelPackagesCount, int transitivePackagesCount)
        {
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            var packages = new[] { new PackageIdentity(packageId, packageVersion) };
            return new UserAction(NuGetOperationType.Install, packages, isSolutionLevel, activeTab, uiSource, selectedPackageLevel, topLevelPackagesCount, transitivePackagesCount);
        }

        public static UserAction CreateUnInstallAction(string packageId, bool isSolutionLevel, ItemFilter activeTab, UIOperationSource uiSource, PackageLevel selectedPackageLevel, int topLevelPackagesCount, int transitivePackagesCount)
        {
            var packages = new[] { new PackageIdentity(packageId, null) };
            return new UserAction(NuGetOperationType.Uninstall, packages, isSolutionLevel, activeTab, uiSource, selectedPackageLevel, topLevelPackagesCount, transitivePackagesCount);
        }

        public static UserAction CreateUpdateAction(IEnumerable<PackageIdentity> packagesToUpdate, bool isSolutionLevel, ItemFilter activeTab, UIOperationSource uiSource, PackageLevel selectedPackageLevel, int topLevelPackagesCount, int transitivePackagesCount)
        {
            return new UserAction(NuGetOperationType.Update, packagesToUpdate, isSolutionLevel, activeTab, uiSource, selectedPackageLevel, topLevelPackagesCount, transitivePackagesCount);
        }
    }
}
