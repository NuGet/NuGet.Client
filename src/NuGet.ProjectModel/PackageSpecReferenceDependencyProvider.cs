// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class PackageSpecReferenceDependencyProvider : IDependencyProvider
    {
        private readonly IPackageSpecResolver _resolver;
        private readonly ExternalProjectReference _projectReference;

        public PackageSpecReferenceDependencyProvider(
            IPackageSpecResolver projectResolver,
            ExternalProjectReference projectReference)
        {
            _resolver = projectResolver;
            _projectReference = projectReference;
        }

        public IEnumerable<string> GetAttemptedPaths(NuGetFramework targetFramework)
        {
            return _resolver.SearchPaths.Select(p => Path.Combine(p, "{name}", "project.json"));
        }

        public bool SupportsType(string libraryType)
        {
            return string.IsNullOrEmpty(libraryType) ||
                   string.Equals(libraryType, LibraryTypes.Project);
        }

        public Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var name = libraryRange.Name;

            PackageSpec packageSpec;

            // Can't find a project file with the name so bail
            if (!_resolver.TryResolvePackageSpec(name, out packageSpec))
            {
                return null;
            }

            // This never returns null
            var targetFrameworkInfo = packageSpec.GetTargetFramework(targetFramework);
            var targetFrameworkDependencies = new List<LibraryDependency>(targetFrameworkInfo.Dependencies);

            if (targetFramework.IsDesktop())
            {
                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "mscorlib",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System.Core",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                targetFrameworkDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "Microsoft.CSharp",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });
            }

            var dependencies = packageSpec.Dependencies.Concat(targetFrameworkDependencies).ToList();

            if (string.Equals(_projectReference?.UniqueName, libraryRange.Name, StringComparison.OrdinalIgnoreCase))
            {
                dependencies.AddRange(_projectReference.ExternalProjectReferences
                    .Select(reference => new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = reference,
                            VersionRange = VersionRange.All,
                            TypeConstraint = LibraryTypes.ExternalProject
                        }
                    }));
            }

            // Mark the library as unresolved if there were specified frameworks
            // and none of them resolved
            var unresolved = targetFrameworkInfo.FrameworkName == null &&
                             packageSpec.TargetFrameworks.Any();

            var library = new Library
            {
                LibraryRange = libraryRange,
                Identity = new LibraryIdentity
                {
                    Name = packageSpec.Name,
                    Version = packageSpec.Version,
                    Type = LibraryTypes.Project,
                },
                Path = packageSpec.FilePath,
                Dependencies = dependencies,
                Resolved = !unresolved,
                [KnownLibraryProperties.PackageSpec] = packageSpec
            };

            return library;
        }
    }
}
