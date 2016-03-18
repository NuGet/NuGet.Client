// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Repositories;
using NuGet.Versioning;

namespace NuGet.DependencyResolver
{
    public class NuGetDependencyResolver : IDependencyProvider
    {
        private readonly NuGetv3LocalRepository _repository;

        public NuGetDependencyResolver(NuGetv3LocalRepository repository)
        {
            _repository = repository;
        }

        public NuGetDependencyResolver(string packagesPath)
        {
            _repository = new NuGetv3LocalRepository(packagesPath, checkPackageIdCase: false);
        }

        public bool SupportsType(LibraryDependencyTarget libraryType)
        {
            return (libraryType & LibraryDependencyTarget.Package) == LibraryDependencyTarget.Package;
        }

        public Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var package = FindCandidate(libraryRange.Name, libraryRange.VersionRange);

            if (package != null)
            {
                var nuspecReader = package.Nuspec;

                var description = new Library
                {
                    LibraryRange = libraryRange,
                    Identity = new LibraryIdentity
                    {
                        Name = package.Id,
                        Version = package.Version,
                        Type = LibraryType.Package
                    },
                    Path = package.ManifestPath,
                    Dependencies = GetDependencies(nuspecReader, targetFramework)
                };

                description.Items["package"] = package;
                description.Items["metadata"] = nuspecReader;

                return description;
            }

            return null;
        }

        private IEnumerable<LibraryDependency> GetDependencies(NuspecReader nuspecReader, NuGetFramework targetFramework)
        {
            var dependencies = NuGetFrameworkUtility.GetNearest(nuspecReader.GetDependencyGroups(),
                targetFramework,
                item => item.TargetFramework);

            return GetDependencies(targetFramework, dependencies);
        }

        private static IList<LibraryDependency> GetDependencies(NuGetFramework targetFramework,
            PackageDependencyGroup dependencies)
        {
            var libraryDependencies = new List<LibraryDependency>();

            if (dependencies != null)
            {
                libraryDependencies.AddRange(
                    dependencies.Packages.Select(PackagingUtility.GetLibraryDependencyFromNuspec));
            }

            return libraryDependencies;
        }

        private LocalPackageInfo FindCandidate(string name, VersionRange versionRange)
        {
            var packages = _repository.FindPackagesById(name);

            return packages.FindBestMatch(versionRange, info => info?.Version);
        }
    }
}
