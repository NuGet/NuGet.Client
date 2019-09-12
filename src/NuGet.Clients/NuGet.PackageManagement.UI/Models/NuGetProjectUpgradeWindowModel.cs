// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Rules;
using NuGet.ProjectManagement;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class NuGetProjectUpgradeWindowModel : INotifyPropertyChanged
    {
        private ObservableCollection<NuGetProjectUpgradeDependencyItem> _upgradeDependencyItems;
        private HashSet<PackageIdentity> _notFoundPackages;
        private string _projectName;
        private bool _hasNotFoundPackages;

        public NuGetProjectUpgradeWindowModel(MSBuildNuGetProject project, IList<PackageDependencyInfo> packageDependencyInfos)
        {
            PackageDependencyInfos = packageDependencyInfos;
            Project = project;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MSBuildNuGetProject Project { get; }

        public bool HasIssues
        {
            get;
            private set;
        }

        public string Title => string.Format(CultureInfo.CurrentCulture, Resources.WindowTitle_NuGetMigrator, ProjectName);

        private string ProjectName
        {
            get
            {
                if (string.IsNullOrEmpty(_projectName))
                {
                    _projectName = NuGetProject.GetUniqueNameOrName(Project);
                    return _projectName;
                }
                else
                {
                    return _projectName;
                }
            }
            set
            {
                _projectName = value;
            }
        }

        public IList<PackageDependencyInfo> PackageDependencyInfos { get; }

        public IEnumerable<PackageIdentity> NotFoundPackages
        {
            get
            {
                if (_notFoundPackages == null)
                {
                    GetUpgradeDependencyItems();
                }
                return _notFoundPackages;
            }
        }

        public bool HasNotFoundPackages
        {
            get
            {
                return _hasNotFoundPackages;
            }
            set
            {
                _hasNotFoundPackages = value;
                OnPropertyChanged("HasNotFoundPackages");
            }
        }
        public ObservableCollection<NuGetProjectUpgradeDependencyItem> UpgradeDependencyItems
            => _upgradeDependencyItems ?? (_upgradeDependencyItems = GetUpgradeDependencyItems());

        public IEnumerable<NuGetProjectUpgradeDependencyItem> DirectDependencies => UpgradeDependencyItems
                .Where(e => e.IsTopLevel);

        public IEnumerable<NuGetProjectUpgradeDependencyItem> TransitiveDependencies => UpgradeDependencyItems
                .Where(e => !e.IsTopLevel);

        private void InitPackageUpgradeIssues(FolderNuGetProject folderNuGetProject, NuGetProjectUpgradeDependencyItem package)
        {
            _notFoundPackages = new HashSet<PackageIdentity>();
            var packageIdentity = new PackageIdentity(package.Id, NuGetVersion.Parse(package.Version));
            // Confirm package exists
            var packagePath = folderNuGetProject.GetInstalledPackageFilePath(packageIdentity);
            if (string.IsNullOrEmpty(packagePath))
            {
                HasIssues = true;
                HasNotFoundPackages = true;
                _notFoundPackages.Add(packageIdentity);
                package.Issues.Add(PackagingLogMessage.CreateWarning(
                    string.Format(CultureInfo.CurrentCulture, Resources.Upgrader_PackageNotFound, packageIdentity.Id),
                    NuGetLogCode.NU5500));
            }
            else
            {
                using (var reader = new PackageArchiveReader(packagePath))
                {
                    var packageRules = RuleSet.PackagesConfigToPackageReferenceMigrationRuleSet;
                    var issues = package.Issues;

                    foreach (var rule in packageRules)
                    {
                        var foundIssues = rule.Validate(reader).OrderBy(p => p.Code.ToString(), StringComparer.CurrentCulture);
                        if (foundIssues != null && foundIssues.Any())
                        {
                            HasIssues = true;
                        }
                        issues.AddRange(foundIssues);
                    }

                    if(!package.InstallAsTopLevel)
                    {
                        PromoteToTopLevelIfNeeded(reader, package);
                    }
                }
            }
        }

        /* The package will be installed as top level if :
         * 1) The package contains build, buildCrossTargeting, contentFiles or analyzers folder.
         * 2) The package has developmentDependency set to true.
         */         
        private void PromoteToTopLevelIfNeeded(PackageArchiveReader reader, NuGetProjectUpgradeDependencyItem item)
        {
            if(reader.GetDevelopmentDependency() ||
                reader.GetFiles(PackagingConstants.Folders.Build).Any() ||
                reader.GetFiles(PackagingConstants.Folders.BuildCrossTargeting).Any() ||
                reader.GetFiles(PackagingConstants.Folders.ContentFiles).Any() ||
                reader.GetFiles(PackagingConstants.Folders.Analyzers).Any())
            {
                item.InstallAsTopLevel = true;
            }

        }

        private ObservableCollection<NuGetProjectUpgradeDependencyItem> GetUpgradeDependencyItems()
        {
            var upgradeDependencyItems = PackageGraphAnalysisUtilities.GetPackagesWithDependants(PackageDependencyInfos).Select(e => new NuGetProjectUpgradeDependencyItem(e.Identity, e));
            var folderNuGetProject = Project.FolderNuGetProject;
            foreach (var package in upgradeDependencyItems)
            {
                InitPackageUpgradeIssues(folderNuGetProject, package);
            }

            return new ObservableCollection<NuGetProjectUpgradeDependencyItem>(upgradeDependencyItems);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

#if DEBUG
        public NuGetProjectUpgradeWindowModel()
        {
            _upgradeDependencyItems = DesignTimeUpgradeDependencyItems;
            _projectName = "TestProject";
        }

        private static readonly PackageIdentity PackageOne = new PackageIdentity("Test.Package.One", new NuGetVersion("1.2.3"));
        private static readonly PackageIdentity PackageTwo = new PackageIdentity("Test.Package.Two", new NuGetVersion("4.5.6"));
        private static readonly PackageIdentity PackageThree = new PackageIdentity("Test.Package.Three", new NuGetVersion("7.8.9"));

        public static readonly ObservableCollection<NuGetProjectUpgradeDependencyItem> DesignTimeUpgradeDependencyItems = new ObservableCollection<NuGetProjectUpgradeDependencyItem>( new List<NuGetProjectUpgradeDependencyItem>
        {
            new NuGetProjectUpgradeDependencyItem(PackageOne, new PackageWithDependants(PackageOne, Enumerable.Empty<PackageIdentity>().ToList())),
            new NuGetProjectUpgradeDependencyItem(PackageTwo, new PackageWithDependants(PackageTwo, new List<PackageIdentity> {PackageOne})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo})),
            new NuGetProjectUpgradeDependencyItem(PackageThree, new PackageWithDependants(PackageThree, new List<PackageIdentity> {PackageOne, PackageTwo}))

        });
#endif
    }
}
