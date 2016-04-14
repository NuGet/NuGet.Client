// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;
using NuGet.Common;
using NuGet.Configuration;
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
        private ISolutionManager _solutionManager;
        private ISettings _settings;
        private IConsole _logger;
        private PackageReferenceFile _file;
        private Project _project;
        private FrameworkName _targetFramework = null;
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

        private bool FindPackagesAndDependencies(Dictionary<string, Tuple<NuGet.IPackage, NuGet.PackageDependency>> packagesAndDependencies)
        {
            IDictionary<PackageName, PackageReference> packageReferences = _file.GetPackageReferences().ToDictionary(r => new PackageName(r.Id, r.Version));

			PackageRepository = GetPackagesRepository();
            if (PackageRepository == null)
            {
                _logger.WriteLine("Aborting: Unable to find solution's packages folder.");
                return false;
            }

            foreach (var reference in packageReferences.Values)
            {
                IPackage package = PackageRepository.FindPackage(reference.Id, reference.Version);
                if (package == null)
                {
                    // We do not want to continue if we can't find a package
                    _logger.WriteLine("Aborting: Unable find package " + reference.Id + ", Version " + reference.Version);
                    return false;
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

            return true;
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
                return _repository.ResolveDependency(dependency, allowPrereleaseVersions: false, preferListedPackages: false);
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
        }
    }
}
