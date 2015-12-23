// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Handles both external references and projects discovered through directories
    /// If the type is set to external project directory discovery will be disabled.
    /// </summary>
    public class PackageSpecReferenceDependencyProvider : IDependencyProvider
    {
        private readonly IPackageSpecResolver _resolver;
        private readonly Dictionary<string, ExternalProjectReference> _externalProjects;

        public PackageSpecReferenceDependencyProvider(
            IPackageSpecResolver projectResolver,
            IEnumerable<ExternalProjectReference> externalProjects)
        {
            if (projectResolver == null)
            {
                throw new ArgumentNullException(nameof(projectResolver));
            }

            if (externalProjects == null)
            {
                throw new ArgumentNullException(nameof(externalProjects));
            }

            _resolver = projectResolver;

            _externalProjects = new Dictionary<string, ExternalProjectReference>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in externalProjects)
            {
                Debug.Assert(
                    !_externalProjects.ContainsKey(project.UniqueName),
                    $"Duplicate project {project.UniqueName}");

                if (!_externalProjects.ContainsKey(project.UniqueName))
                {
                    _externalProjects.Add(project.UniqueName, project);
                }
            }
        }

        public IEnumerable<string> GetAttemptedPaths(NuGetFramework targetFramework)
        {
            return _resolver.SearchPaths.Select(p => Path.Combine(p, "{name}", "project.json"));
        }

        public bool SupportsType(string libraryType)
        {
            return string.IsNullOrEmpty(libraryType)
                || string.Equals(libraryType, LibraryTypes.Project, StringComparison.Ordinal)
                || string.Equals(libraryType, LibraryTypes.ExternalProject, StringComparison.Ordinal);
        }

        public Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var name = libraryRange.Name;

            ExternalProjectReference externalReference = null;
            PackageSpec packageSpec = null;
            bool resolvedUsingDirectory = false;

            // Check the external references first
            if (_externalProjects.TryGetValue(name, out externalReference))
            {
                packageSpec = externalReference.PackageSpec;
            }
            else if (!string.Equals(
                libraryRange.TypeConstraint,
                LibraryTypes.ExternalProject,
                StringComparison.Ordinal))
            {
                // Allow directory look ups unless this constrained to external
                resolvedUsingDirectory = _resolver.TryResolvePackageSpec(name, out packageSpec);
            }

            if (externalReference == null && packageSpec == null)
            {
                // unable to find any projects
                return null;
            }

            // create a dictionary of dependencies to make sure that no duplicates exist
            var dependencies = new List<LibraryDependency>();
            TargetFrameworkInformation targetFrameworkInfo = null;

            if (packageSpec != null)
            {
                // Add dependencies section
                dependencies.AddRange(packageSpec.Dependencies);

                // Add framework specific dependencies
                targetFrameworkInfo = packageSpec.GetTargetFramework(targetFramework);
                dependencies.AddRange(targetFrameworkInfo.Dependencies);
            }

            if (externalReference != null)
            {
                // External references are created without pivoting on the TxM. Here we need to account for this
                // and filter out references except the nearest TxM.
                var filteredExternalDependencies = new HashSet<string>(
                    externalReference.ExternalProjectReferences,
                    StringComparer.OrdinalIgnoreCase);

                if (packageSpec != null)
                {
                    // Create an exclude list of all references from the non-selected TxM
                    // Start with all framework specific references
                    var allFrameworkDependencies = GetProjectNames(
                        packageSpec.TargetFrameworks.SelectMany(info => info.Dependencies));

                    var excludedDependencies = new HashSet<string>(
                        allFrameworkDependencies,
                        StringComparer.OrdinalIgnoreCase);

                    // Then clear out excluded dependencies that are found in the good dependency list
                    foreach (var dependency in GetProjectNames(dependencies))
                    {
                        excludedDependencies.Remove(dependency);
                    }

                    // Remove excluded dependencies from the external list
                    foreach (var excluded in excludedDependencies)
                    {
                        filteredExternalDependencies.Remove(excluded);
                    }
                }

                // Set all dependencies from project.json to external if an external match was passed in
                // This is viral and keeps p2ps from looking into directories when we are going down
                // a path already resolved by msbuild.
                foreach (var dependency in dependencies.Where(d => filteredExternalDependencies.Contains(d.Name)))
                {
                    dependency.LibraryRange.TypeConstraint = LibraryTypes.ExternalProject;
                }

                // Add dependencies passed in externally
                // These are usually msbuild references which have less metadata, they have
                // the lowest priority.
                // Note: Only add in dependencies that are in the filtered list to avoid getting the wrong TxM
                dependencies.AddRange(externalReference.ExternalProjectReferences
                    .Where(dependencyName => filteredExternalDependencies.Contains(dependencyName))
                    .Select(reference => new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = reference,
                            VersionRange = VersionRange.Parse("1.0.0"),
                            TypeConstraint = LibraryTypes.ExternalProject
                        }
                    }));
            }

            if (resolvedUsingDirectory && targetFramework.IsDesktop())
            {
                // For xproj add in the default references for Desktop
                dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "mscorlib",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "System.Core",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });

                dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = "Microsoft.CSharp",
                        TypeConstraint = LibraryTypes.Reference
                    }
                });
            }

            // Mark the library as unresolved if there were specified frameworks
            // and none of them resolved
            var resolved = true;
            if (targetFrameworkInfo != null)
            {
                resolved = !(targetFrameworkInfo.FrameworkName == null &&
                                     packageSpec.TargetFrameworks.Any());
            }

            // Remove duplicate dependencies. A reference can exist both in csproj and project.json
            // dependencies is already ordered by importance here
            var uniqueDependencies = new List<LibraryDependency>(dependencies.Count);
            var projectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in dependencies)
            {
                if (projectNames.Add(project.Name))
                {
                    uniqueDependencies.Add(project);
                }
            }

            var library = new Library
            {
                LibraryRange = libraryRange,
                Identity = new LibraryIdentity
                {
                    Name = externalReference?.UniqueName ?? packageSpec.Name,
                    Version = packageSpec?.Version ?? NuGetVersion.Parse("1.0.0"),
                    Type = LibraryTypes.Project,
                },
                Path = packageSpec?.FilePath,
                Dependencies = uniqueDependencies,
                Resolved = resolved
            };

            if (packageSpec != null)
            {
                library[KnownLibraryProperties.PackageSpec] = packageSpec;
            }

            string msbuildPath = null;

            if (externalReference == null)
            {
                // Build the path to the .xproj file
                // If it exists add it to the library properties for the lock file
                var projectDir = Path.GetDirectoryName(packageSpec.FilePath);
                var xprojPath = Path.Combine(projectDir, packageSpec.Name + ".xproj");

                if (File.Exists(xprojPath))
                {
                    msbuildPath = xprojPath;
                }
            }
            else
            {
                msbuildPath = externalReference.MSBuildProjectPath;
            }

            if (msbuildPath != null)
            {
                library[KnownLibraryProperties.MSBuildProjectPath] = msbuildPath;
            }

            if (targetFrameworkInfo != null)
            {
                library[KnownLibraryProperties.TargetFrameworkInformation] = targetFrameworkInfo;

                // Add a compile asset for msbuild to xproj projects
                if (targetFrameworkInfo.FrameworkName != null
                    && msbuildPath?.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var tfmFolder = targetFrameworkInfo.FrameworkName.GetShortFolderName();

                    // Currently the assembly name cannot be changed for xproj, we can construct the path to where
                    // the output should be.
                    var asset = $"{tfmFolder}/{packageSpec.Name}.dll";
                    library[KnownLibraryProperties.CompileAsset] = asset;
                    library[KnownLibraryProperties.RuntimeAsset] = asset;
                }
            }

            return library;
        }

        /// <summary>
        /// Filter dependencies down to only possible project references and return the names.
        /// </summary>
        private IEnumerable<string> GetProjectNames(IEnumerable<LibraryDependency> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                if (IsProject(dependency))
                {
                    yield return dependency.Name;
                }
            }

            yield break;
        }

        private bool IsProject(LibraryDependency dependency)
        {
            var type = dependency.LibraryRange.TypeConstraint;

            return SupportsType(type);
        }
    }
}
