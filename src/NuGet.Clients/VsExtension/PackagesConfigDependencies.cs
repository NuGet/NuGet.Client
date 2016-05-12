// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGet.Common;
using NuGet.Versioning;
using NuGetConsole;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using PackageDependency = NuGet.Packaging.Core.PackageDependency;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using ISettings = NuGet.Configuration.ISettings;

namespace NuGetVSExtension
{
    internal class PackagesConfigDependencies
    {
        private readonly ISolutionManager _solutionManager;
        private readonly ISettings _settings;
        private readonly IConsole _logger;
        private readonly PackageReferenceFile _file;
        private readonly Project _project;
        private FrameworkName _targetFramework;
        private string _projectFullPath;

        public NuGet.Configuration.IMachineWideSettings MachineWideSettings { get; set; }
        public Dictionary<string, Tuple<IPackage, NuGet.PackageDependency>> AllPackagesAndDependencies { get; }
        public Dictionary<string, PackageDependency> MinimalDependencies { get; }
		public SharedPackageRepository PackageRepository { get; private set; }
		public string PackagesDir { get; private set; }



		internal PackagesConfigDependencies(Project project, ISolutionManager solutionManager, ISettings settings, IConsole logger)
        {
            _solutionManager = solutionManager;
            _settings = settings;
            _project = project;
            var projectFileName = project.FileName;
            _logger = logger;
            _file = PackageReferenceFile.CreateFromProject(projectFileName);

            AllPackagesAndDependencies = new Dictionary<string, Tuple<IPackage, NuGet.PackageDependency>>();
            FindPackagesAndDependencies(AllPackagesAndDependencies);
            var packages = AllPackagesAndDependencies.Values.Select(t => t.Item1).ToList();

            MinimalDependencies = new Dictionary<string, PackageDependency>();
            foreach (var package in GetMinimumSet(packages))
            {
                if (MinimalDependencies.ContainsKey(package.Id))
                {
                    continue;
                }

                var dependency = AllPackagesAndDependencies[package.Id].Item2;
                MinimalDependencies[dependency.Id] = new PackageDependency(dependency.Id, VersionRange.Parse(dependency.VersionSpec.ToString()));
            }
        }

        private Dictionary<string, PackageDependency> _allDependencies = null;
        public Dictionary<string, PackageDependency> AllDependencies
        {
            get
            {
                if (_allDependencies == null)
                {
                    _allDependencies = new Dictionary<string, PackageDependency>();
                    foreach (var packageAndDependency in AllPackagesAndDependencies)
                    {
                        var dependency = packageAndDependency.Value.Item2;
                        _allDependencies[dependency.Id] = new PackageDependency(dependency.Id, VersionRange.Parse(dependency.VersionSpec.ToString()));
                    }
                }
                return _allDependencies;
            }
        }

        private void FindPackagesAndDependencies(IDictionary<string, Tuple<IPackage, NuGet.PackageDependency>> packagesAndDependencies)
        {
            IDictionary<PackageName, PackageReference> packageReferences = _file.GetPackageReferences().ToDictionary(r => new PackageName(r.Id, r.Version));

            PackageRepository = GetPackagesRepository();
            if (PackageRepository == null)
            {
                _logger.WriteLine("Aborting: Unable to find solution's packages folder.");
                return;
            }

            foreach (var reference in packageReferences.Values)
            {
                IPackage package = PackageRepository.FindPackage(reference.Id, reference.Version);
                if (package == null)
                {
                    // We do not want to continue if we can't find a package
                    _logger.WriteLine("Aborting: Unable find package " + reference.Id + ", Version " + reference.Version);
                    return;
                }

                /*var contentFiles = package.GetContentFiles();
                if (contentFiles.Any())
                {
                    _logger.WriteLine("Aborting: Package " + reference.Id + ", Version " + reference.Version + " uses content files.");
                    // If package has content files, we can't successfully convert
                    //return false;
                }*/

                if (!packagesAndDependencies.ContainsKey(package.Id))
                {
                    IVersionSpec spec = GetVersionConstraint(packageReferences, package);
                    var dependency = new NuGet.PackageDependency(package.Id, spec);
                    packagesAndDependencies.Add(package.Id, new Tuple<IPackage, NuGet.PackageDependency>(package, dependency));
                }
            }
        }

        public string ProjectFullPath
        {
            get
            {
                if (String.IsNullOrEmpty(_projectFullPath))
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _projectFullPath = EnvDTEProjectUtility.GetFullPath(_project);
                    });
                }

                return _projectFullPath;
            }
        }

        private static IVersionSpec GetVersionConstraint(IDictionary<PackageName, NuGet.PackageReference> packageReferences, IPackage package)
        {
            IVersionSpec defaultVersionConstraint = VersionUtility.ParseVersionSpec(package.Version.ToString());

            NuGet.PackageReference packageReference;
            var key = new PackageName(package.Id, package.Version);
            if (!packageReferences.TryGetValue(key, out packageReference))
            {
                return defaultVersionConstraint;
            }

            return packageReference.VersionConstraint ?? defaultVersionConstraint;
        }

        private SharedPackageRepository GetPackagesRepository()
        {
			PackagesDir = PackagesFolderPathUtility.GetPackagesFolderPath(_solutionManager.SolutionDirectory, _settings);
            if (!string.IsNullOrEmpty(PackagesDir) && Directory.Exists(PackagesDir))
            {
                return new SharedPackageRepository(PackagesDir);
            }

            return null;
        }

        private FrameworkName TargetFramework
        {
            get
            {
                if (_targetFramework == null)
                {
                    try
                    {
                        _targetFramework = _project.Properties.Item("TargetFrameworkMoniker").Value;
                    }
                    catch (Exception) { }
                }
                return _targetFramework;
            }
        }

        private IEnumerable<IPackage> GetMinimumSet(List<IPackage> packages)
        {
            return new Walker(packages, TargetFramework).GetMinimalSet();
        }

        private class Walker : PackageWalker
        {
            private readonly IPackageRepository _repository;
            private readonly List<IPackage> _packages;

            public Walker(List<IPackage> packages, FrameworkName targetFramework) :
                base(targetFramework)
            {
                _packages = packages;
                _repository = new ReadOnlyPackageRepository(packages.ToList());
            }

            protected override bool SkipDependencyResolveError
            {
                get
                {
                    // For the pack command, when don't need to throw if a dependency is missing
                    // from a nuspec file.
                    return true;
                }
            }

            protected override IPackage ResolveDependency(NuGet.PackageDependency dependency)
            {
                return ResolveDependency(_repository, dependency, allowPrereleaseVersions: false, preferListedPackages: false);
            }

            protected override bool OnAfterResolveDependency(IPackage package, IPackage dependency)
            {
                _packages.Remove(dependency);
                return base.OnAfterResolveDependency(package, dependency);
            }

            public IEnumerable<IPackage> GetMinimalSet()
            {
                foreach (var package in _repository.GetPackages())
                {
                    Walk(package);
                }
                return _packages;
            }

            public static IPackage ResolveDependency(IPackageRepository repository, NuGet.PackageDependency dependency, bool allowPrereleaseVersions, bool preferListedPackages)
            {
                return ResolveDependency(repository, dependency, constraintProvider: null, allowPrereleaseVersions: allowPrereleaseVersions, preferListedPackages: preferListedPackages, dependencyVersion: DependencyVersion.Lowest);
            }

            public static IPackage ResolveDependency(IPackageRepository repository, NuGet.PackageDependency dependency, IPackageConstraintProvider constraintProvider, bool allowPrereleaseVersions, bool preferListedPackages, DependencyVersion dependencyVersion)
            {
                IDependencyResolver dependencyResolver = repository as IDependencyResolver;
                if (dependencyResolver != null)
                {
                    return dependencyResolver.ResolveDependency(dependency, constraintProvider, allowPrereleaseVersions, preferListedPackages, dependencyVersion);
                }
                return ResolveDependencyCore(repository, dependency, constraintProvider, allowPrereleaseVersions, preferListedPackages, dependencyVersion);
            }

            internal static IPackage ResolveDependencyCore(
                IPackageRepository repository,
                NuGet.PackageDependency dependency,
                IPackageConstraintProvider constraintProvider,
                bool allowPrereleaseVersions,
                bool preferListedPackages,
                DependencyVersion dependencyVersion)
            {
                if (repository == null)
                {
                    throw new ArgumentNullException("repository");
                }

                if (dependency == null)
                {
                    throw new ArgumentNullException("dependency");
                }

                IEnumerable<IPackage> packages = repository.FindPackagesById(dependency.Id).ToList();

                // Always filter by constraints when looking for dependencies
                packages = FilterPackagesByConstraints(constraintProvider, packages, dependency.Id, allowPrereleaseVersions);

                IList<IPackage> candidates = packages.ToList();

                if (preferListedPackages)
                {
                    // pick among Listed packages first
                    IPackage listedSelectedPackage = ResolveDependencyCore(
                        candidates.Where(PackageExtensions.IsListed),
                        dependency,
                        dependencyVersion);
                    if (listedSelectedPackage != null)
                    {
                        return listedSelectedPackage;
                    }
                }

                return ResolveDependencyCore(candidates, dependency, dependencyVersion);
            }

            /// <summary>
            /// From the list of packages <paramref name="packages"/>, selects the package that best 
            /// matches the <paramref name="dependency"/>.
            /// </summary>
            /// <param name="packages">The list of packages.</param>
            /// <param name="dependency">The dependency used to select package from the list.</param>
            /// <param name="dependencyVersion">Indicates the method used to select dependency. 
            /// Applicable only when dependency.VersionSpec is not null.</param>
            /// <returns>The selected package.</returns>
            private static IPackage ResolveDependencyCore(
                IEnumerable<IPackage> packages,
                NuGet.PackageDependency dependency,
                DependencyVersion dependencyVersion)
            {
                // If version info was specified then use it
                if (dependency.VersionSpec != null)
                {
                    packages = FindByVersion(packages, dependency.VersionSpec).OrderBy(p => p.Version);
                    return SelectDependency(packages, dependencyVersion);
                }
                else
                {
                    // BUG 840: If no version info was specified then pick the latest
                    return packages.OrderByDescending(p => p.Version)
                        .FirstOrDefault();
                }
            }

            public static IEnumerable<IPackage> FindByVersion(IEnumerable<IPackage> source, IVersionSpec versionSpec)
            {
                if (versionSpec == null)
                {
                    throw new ArgumentNullException("versionSpec");
                }

                return source.Where(ToDelegate(versionSpec));
            }

            public static Func<IPackage, bool> ToDelegate(IVersionSpec versionInfo)
            {
                if (versionInfo == null)
                {
                    throw new ArgumentNullException("versionInfo");
                }
                return ToDelegate<IPackage>(versionInfo, p => p.Version);
            }

            public static Func<T, bool> ToDelegate<T>(IVersionSpec versionInfo, Func<T, NuGet.SemanticVersion> extractor)
            {
                if (versionInfo == null)
                {
                    throw new ArgumentNullException("versionInfo");
                }
                if (extractor == null)
                {
                    throw new ArgumentNullException("extractor");
                }

                return p =>
                {
                    NuGet.SemanticVersion version = extractor(p);
                    bool condition = true;
                    if (versionInfo.MinVersion != null)
                    {
                        if (versionInfo.IsMinInclusive)
                        {
                            condition = condition && version >= versionInfo.MinVersion;
                        }
                        else
                        {
                            condition = condition && version > versionInfo.MinVersion;
                        }
                    }

                    if (versionInfo.MaxVersion != null)
                    {
                        if (versionInfo.IsMaxInclusive)
                        {
                            condition = condition && version <= versionInfo.MaxVersion;
                        }
                        else
                        {
                            condition = condition && version < versionInfo.MaxVersion;
                        }
                    }

                    return condition;
                };
            }

            private static IEnumerable<IPackage> FilterPackagesByConstraints(
                IPackageConstraintProvider constraintProvider,
                IEnumerable<IPackage> packages,
                string packageId,
                bool allowPrereleaseVersions)
            {
                constraintProvider = constraintProvider ?? NullConstraintProvider.Instance;

                // Filter packages by this constraint
                IVersionSpec constraint = constraintProvider.GetConstraint(packageId);
                if (constraint != null)
                {
                    packages = packages.FindByVersion(constraint);
                }
                if (!allowPrereleaseVersions)
                {
                    packages = packages.Where(p => p.IsReleaseVersion());
                }

                return packages;
            }

            internal static IPackage SelectDependency(IEnumerable<IPackage> packages, DependencyVersion dependencyVersion)
            {
                if (packages == null || !packages.Any())
                {
                    return null;
                }

                if (dependencyVersion == DependencyVersion.Lowest)
                {
                    return packages.FirstOrDefault();
                }
                else if (dependencyVersion == DependencyVersion.Highest)
                {
                    return packages.LastOrDefault();
                }
                else if (dependencyVersion == DependencyVersion.HighestPatch)
                {
                    var groups = from p in packages
                                 group p by new { p.Version.Version.Major, p.Version.Version.Minor } into g
                                 orderby g.Key.Major, g.Key.Minor
                                 select g;
                    return (from p in groups.First()
                            orderby p.Version descending
                            select p).FirstOrDefault();
                }
                else if (dependencyVersion == DependencyVersion.HighestMinor)
                {
                    var groups = from p in packages
                                 group p by new { p.Version.Version.Major } into g
                                 orderby g.Key.Major
                                 select g;
                    return (from p in groups.First()
                            orderby p.Version descending
                            select p).FirstOrDefault();
                }

                throw new ArgumentOutOfRangeException("dependencyVersion");
            }
        }
    }
}
