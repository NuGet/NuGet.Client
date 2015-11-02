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

        public bool SupportsType(string libraryType)
        {
            return string.IsNullOrEmpty(libraryType) ||
                   string.Equals(libraryType, LibraryTypes.Package);
        }

        public Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var package = FindCandidate(libraryRange.Name, libraryRange.VersionRange);

            if (package != null)
            {
                NuspecReader nuspecReader = null;
                using (var stream = File.OpenRead(package.ManifestPath))
                {
                    nuspecReader = new NuspecReader(stream);
                }

                var description = new Library
                    {
                        LibraryRange = libraryRange,
                        Identity = new LibraryIdentity
                            {
                                Name = package.Id,
                                Version = package.Version,
                                Type = LibraryTypes.Package
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

            var frameworkAssemblies = NuGetFrameworkUtility.GetNearest(nuspecReader.GetFrameworkReferenceGroups(),
                targetFramework,
                item => item.TargetFramework);

            return GetDependencies(targetFramework, dependencies, frameworkAssemblies);
        }

        private static IList<LibraryDependency> GetDependencies(NuGetFramework targetFramework,
            PackageDependencyGroup dependencies,
            FrameworkSpecificGroup frameworkAssemblies)
        {
            var libraryDependencies = new List<LibraryDependency>();

            if (dependencies != null)
            {
                libraryDependencies.AddRange(
                    dependencies.Packages.Select(PackagingUtility.GetLibraryDependencyFromNuspec));
            }

            if (frameworkAssemblies == null)
            {
                return libraryDependencies;
            }

            if (!targetFramework.IsDesktop())
            {
                // REVIEW: This isn't 100% correct since none *can* mean 
                // any in theory, but in practice it means .NET full reference assembly
                // If there's no supported target frameworks and we're not targeting
                // the desktop framework then skip it.

                // To do this properly we'll need all reference assemblies supported
                // by each supported target framework which isn't always available.
                return libraryDependencies;
            }

            foreach (var name in frameworkAssemblies.Items)
            {
                libraryDependencies.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                            {
                                Name = name,
                                TypeConstraint = LibraryTypes.Reference
                            }
                    });
            }

            return libraryDependencies;
        }

        private LocalPackageInfo FindCandidate(string name, VersionRange versionRange)
        {
            var packages = _repository.FindPackagesById(name);

            return packages.FindBestMatch(versionRange, info => info?.Version);
        }

        public IEnumerable<string> GetAttemptedPaths(NuGetFramework targetFramework)
        {
            return new[]
                {
                    Path.Combine(_repository.RepositoryRoot, "{name}", "{version}", "{name}.nuspec")
                };
        }
    }
}
