// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Resolves dependencies retrieved from a list of <see cref="ExternalProjectReference" />.
    /// </summary>
    public class ExternalProjectReferenceDependencyProvider : IDependencyProvider
    {
        public IReadOnlyDictionary<string, ExternalProjectReference> ExternalProjects { get; }

        public ExternalProjectReferenceDependencyProvider(IEnumerable<ExternalProjectReference> externalProjects)
        {
            ExternalProjects = new ReadOnlyDictionary<string, ExternalProjectReference>(externalProjects.ToDictionary(e => e.UniqueName, StringComparer.OrdinalIgnoreCase));
        }

        public bool SupportsType(string libraryType)
        {
            return string.Equals(libraryType, LibraryTypes.ExternalProject);
        }

        public IEnumerable<string> GetAttemptedPaths(NuGetFramework targetFramework)
        {
            return Enumerable.Empty<string>();
        }

        public Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            ExternalProjectReference externalProject;
            if (!ExternalProjects.TryGetValue(libraryRange.Name, out externalProject))
            {
                // No project!
                return null;
            }

            // Fill dependencies from external project references
            var dependencies = externalProject.ExternalProjectReferences.Select(s => new LibraryDependency()
                {
                    LibraryRange = new LibraryRange()
                        {
                            Name = s,
                            VersionRange = null,
                            TypeConstraint = LibraryTypes.ExternalProject
                        },
                    Type = LibraryDependencyType.Default
                }).ToList();

            // Add dependencies from the nuget.json file
            if (!string.IsNullOrEmpty(externalProject.PackageSpecPath)
                && File.Exists(externalProject.PackageSpecPath))
            {
                PackageSpec packageSpec;
                using (var stream = new FileStream(externalProject.PackageSpecPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    packageSpec = JsonPackageSpecReader.GetPackageSpec(stream, externalProject.UniqueName, externalProject.PackageSpecPath);
                }

                // Add framework-agnostic dependencies
                dependencies.AddRange(packageSpec.Dependencies);

                // Add framework-specific dependencies
                var frameworkInfo = packageSpec.GetTargetFramework(targetFramework);
                if (frameworkInfo != null)
                {
                    dependencies.AddRange(frameworkInfo.Dependencies);
                }
            }

            // Construct the library and return it
            return new Library()
                {
                    Identity = new LibraryIdentity()
                        {
                            Name = externalProject.UniqueName,
                            Version = new NuGetVersion("1.0.0"),
                            Type = LibraryTypes.ExternalProject
                        },
                    LibraryRange = libraryRange,
                    Dependencies = dependencies,
                    Path = externalProject.PackageSpecPath,
                    Resolved = true
                };
        }
    }
}
