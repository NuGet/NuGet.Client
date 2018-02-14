// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class NuGetProjectUpgradeWindowModel : INotifyPropertyChanged
    {
        private IEnumerable<string> _allPackages;
        private bool _collapseDependencies;
        private IEnumerable<string> _dependencyPackages;
        private IEnumerable<string> _includedCollapsedPackages;
        private IEnumerable<NuGetProjectUpgradeDependencyItem> _upgradeDependencyItems;
        private string _projectName;
        private IList<string> _warnings;
        private IList<string> _errors;
        private IList<PackageIdentity> _notFoundPackages;

        public NuGetProjectUpgradeWindowModel(NuGetProject project, IList<PackageDependencyInfo> packageDependencyInfos,
            bool collapseDependencies)
        {
            PackageDependencyInfos = packageDependencyInfos;
            Project = project;
            _collapseDependencies = collapseDependencies;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public NuGetProject Project { get; }

        public string Title => string.Format(CultureInfo.CurrentCulture, Resources.Text_ChangesForProject, ProjectName);

        private string ProjectName => _projectName ?? (_projectName = NuGetProject.GetUniqueNameOrName(Project));

        public IList<PackageDependencyInfo> PackageDependencyInfos { get; }

        // Changing CollapseDependencies updates the list of included packages
        public bool CollapseDependencies
        {
            get { return _collapseDependencies; }
            set
            {
                _collapseDependencies = value;
                OnPropertyChanged(nameof(DirectDependencies));
                OnPropertyChanged(nameof(TransiviteDependencies));
            }
        }

        public IEnumerable<string> Warnings
        {
            get
            {
                if (_warnings == null)
                {
                    InitPackageUpgradeIssues();
                }
                return _warnings;
            }
        }

        public IEnumerable<string> Errors
        {
            get
            {
                if (_errors == null)
                {
                    InitPackageUpgradeIssues();
                }
                return _errors;
            }
        }

        public IList<PackageIdentity> NotFoundPackages
        {
            get
            {
                if (_notFoundPackages == null)
                {
                    InitPackageUpgradeIssues();
                }
                return _notFoundPackages;
            }
        }

        public bool HasIssues => Errors.Any() || Warnings.Any();

        public IEnumerable<NuGetProjectUpgradeDependencyItem> UpgradeDependencyItems
            => _upgradeDependencyItems ?? (_upgradeDependencyItems = GetUpgradeDependencyItems());

        public IEnumerable<string> DirectDependencies => CollapseDependencies ? IncludedCollapsedPackages : AllPackages;

        public IEnumerable<string> TransiviteDependencies => CollapseDependencies ? DependencyPackages : new List<string>();

        private IEnumerable<string> DependencyPackages => _dependencyPackages ?? (_dependencyPackages = GetDependencyPackages());

        private IEnumerable<string> AllPackages => _allPackages ?? (_allPackages = UpgradeDependencyItems.Select(d => d.Package.ToString()));

        private IEnumerable<string> IncludedCollapsedPackages => _includedCollapsedPackages ?? (_includedCollapsedPackages = GetIncludedCollapsedPackages());

        private IEnumerable<string> GetDependencyPackages()
        {
            return UpgradeDependencyItems.Where(d => d.DependingPackages.Any()).Select(d => d.ToString());
        }

        private IEnumerable<string> GetIncludedCollapsedPackages()
        {
            return UpgradeDependencyItems
                .Where(upgradeDependencyItem => !upgradeDependencyItem.DependingPackages.Any())
                .Select(upgradeDependencyItem => upgradeDependencyItem.Package.ToString());
        }

        private void InitPackageUpgradeIssues()
        {
            _warnings = new List<string>();
            _errors = new List<string>();
            _notFoundPackages = new List<PackageIdentity>();

            var msBuildNuGetProject = (MSBuildNuGetProject)Project;
            var framework = msBuildNuGetProject.ProjectSystem.TargetFramework;
            var folderNuGetProject = msBuildNuGetProject.FolderNuGetProject;

            foreach (var package in PackageDependencyInfos)
            {
                // We create a new PackageIdentity here, otherwise we would be passing in a PackageDependencyInfo
                // which includes dependencies in its ToString().
                InitPackageUpgradeIssues(folderNuGetProject, new PackageIdentity(package.Id, package.Version), framework);
            }
        }

        private void InitPackageUpgradeIssues(FolderNuGetProject folderNuGetProject, PackageIdentity packageIdentity, NuGetFramework framework)
        {
            // Confirm package exists
            var packagePath = folderNuGetProject.GetInstalledPackageFilePath(packageIdentity);
            if (string.IsNullOrEmpty(packagePath))
            {
                _errors.Add(string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgradeError_CannotFindPackage, packageIdentity));
                _notFoundPackages.Add(packageIdentity);
            }
            else
            {
                var reader = new PackageArchiveReader(packagePath);

                // Check if it has content files
                var contentFilesGroup = MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(framework,
                    reader.GetContentItems());
                if (MSBuildNuGetProjectSystemUtility.IsValid(contentFilesGroup) && contentFilesGroup.Items.Any())
                {
                    _warnings.Add(string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgradeWarning_HasContentFiles, packageIdentity));
                }

                // Check if it has an install.ps1 file
                var toolItemsGroup = MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(framework,
                    reader.GetToolItems());
                toolItemsGroup = MSBuildNuGetProjectSystemUtility.Normalize(toolItemsGroup);
                var isValid = MSBuildNuGetProjectSystemUtility.IsValid(toolItemsGroup);
                var hasInstall = isValid && toolItemsGroup.Items.Any(p => p.EndsWith(Path.DirectorySeparatorChar + PowerShellScripts.Install, StringComparison.OrdinalIgnoreCase));
                if (hasInstall)
                {
                    _warnings.Add(string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgradeWarning_HasInstallScript, packageIdentity));
                }
            }
        }

        private IEnumerable<NuGetProjectUpgradeDependencyItem> GetUpgradeDependencyItems()
        {
            var upgradeDependencyItems = PackageDependencyInfos
                .Select(p => new NuGetProjectUpgradeDependencyItem(new PackageIdentity(p.Id, p.Version))).ToList();

            foreach (var packageDependencyInfo in PackageDependencyInfos)
            {
                foreach (var dependency in packageDependencyInfo.Dependencies)
                {
                    var matchingDependencyItem = upgradeDependencyItems
                        .FirstOrDefault(d => (d.Package.Id == dependency.Id) && (d.Package.Version == dependency.VersionRange.MinVersion));
                    matchingDependencyItem?.DependingPackages.Add(new PackageIdentity(packageDependencyInfo.Id, packageDependencyInfo.Version));
                }
            }

            return upgradeDependencyItems;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

#if DEBUG
        public NuGetProjectUpgradeWindowModel()
        {
            // This should only be called by the designer. Prepopulate design time sample values
            //_analysisResults = DesignTimeAnalysisResults;
            _upgradeDependencyItems = DesignTimeUpgradeDependencyItems;
            _collapseDependencies = true;
            _projectName = "TestProject";
            _errors = new List<string>
            {
                string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgradeError_CannotFindPackage, PackageTwo)
            };
            _warnings = new List<string>
            {
                string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgradeWarning_HasContentFiles, PackageOne),
                string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgradeWarning_HasInstallScript, PackageOne),
                string.Format(CultureInfo.CurrentCulture, Resources.NuGetUpgradeWarning_HasInstallScript, PackageThree)
            };
        }

        private static readonly PackageIdentity PackageOne = new PackageIdentity("Test.Package.One", new NuGetVersion("1.2.3"));
        private static readonly PackageIdentity PackageTwo = new PackageIdentity("Test.Package.Two", new NuGetVersion("4.5.6"));
        private static readonly PackageIdentity PackageThree = new PackageIdentity("Test.Package.Three", new NuGetVersion("7.8.9"));

        private static readonly IEnumerable<NuGetProjectUpgradeDependencyItem> DesignTimeUpgradeDependencyItems = new List<NuGetProjectUpgradeDependencyItem>
        {
            new NuGetProjectUpgradeDependencyItem(PackageOne),
            new NuGetProjectUpgradeDependencyItem(PackageTwo, new List<PackageIdentity> {PackageOne}),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})
        };
#endif
    }
}