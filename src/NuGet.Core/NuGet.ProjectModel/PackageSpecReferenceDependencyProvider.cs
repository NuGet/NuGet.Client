// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Common;
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
    public class PackageSpecReferenceDependencyProvider : IProjectDependencyProvider
    {
        private readonly IPackageSpecResolver _defaultResolver;
        private readonly Dictionary<string, ExternalProjectReference> _externalProjectsByPath
            = new Dictionary<string, ExternalProjectReference>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, ExternalProjectReference> _externalProjectsByName
            = new Dictionary<string, ExternalProjectReference>(StringComparer.OrdinalIgnoreCase);

        // RootPath -> Resolver
        private readonly ConcurrentDictionary<string, IPackageSpecResolver> _resolverCache
            = new ConcurrentDictionary<string, IPackageSpecResolver>(StringComparer.Ordinal);

        private readonly ILogger _logger;

        public PackageSpecReferenceDependencyProvider(
            IPackageSpecResolver projectResolver,
            IEnumerable<ExternalProjectReference> externalProjects,
            ILogger logger)
        {
            if (projectResolver == null)
            {
                throw new ArgumentNullException(nameof(projectResolver));
            }

            if (externalProjects == null)
            {
                throw new ArgumentNullException(nameof(externalProjects));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _defaultResolver = projectResolver;
            _logger = logger;

            // The constructor is only executed by a single thread, so TryAdd is fine.
            _resolverCache.TryAdd(projectResolver.RootPath, projectResolver);

            foreach (var project in externalProjects)
            {
                Debug.Assert(
                    !_externalProjectsByPath.ContainsKey(project.UniqueName),
                    $"Duplicate project {project.UniqueName}");

                if (!_externalProjectsByPath.ContainsKey(project.UniqueName))
                {
                    _externalProjectsByPath.Add(project.UniqueName, project);
                }

                Debug.Assert(
                    !_externalProjectsByName.ContainsKey(project.ProjectName),
                    $"Duplicate project {project.ProjectName}");

                if (!_externalProjectsByName.ContainsKey(project.ProjectName))
                {
                    _externalProjectsByName.Add(project.ProjectName, project);
                }
            }
        }

        public bool SupportsType(LibraryDependencyTarget libraryType)
        {
            return (libraryType & (LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject)) != LibraryDependencyTarget.None;
        }

        public Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            return GetLibrary(libraryRange, targetFramework, rootPath: _defaultResolver.RootPath);
        }

        public Library GetLibrary(LibraryRange libraryRange, NuGetFramework targetFramework, string rootPath)
        {
            Library library = null;
            var name = libraryRange.Name;

            ExternalProjectReference externalReference = null;
            PackageSpec packageSpec = null;
            bool resolvedUsingDirectory = false;

            // Check the external references first
            if (_externalProjectsByName.TryGetValue(name, out externalReference))
            {
                packageSpec = externalReference.PackageSpec;
            }
            else if (libraryRange.TypeConstraintAllows(LibraryDependencyTarget.Project))
            {
                // Find the package spec resolver for this root path.
                var specResolver = GetPackageSpecResolver(rootPath);

                // Allow directory look ups unless this constrained to external
                resolvedUsingDirectory = specResolver.TryResolvePackageSpec(name, out packageSpec);
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

                // Remove all framework assemblies
                dependencies.RemoveAll(d => d.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference);

                // Disallow projects (resolved by directory) for non-xproj msbuild projects.
                // If there is no msbuild path then resolving by directory is allowed.
                // CSProj does not allow directory to directory look up.
                if (XProjUtility.IsMSBuildBasedProject(externalReference?.MSBuildProjectPath))
                {
                    foreach (var dependency in dependencies)
                    {
                        // Remove "project" from the allowed types for this dependency
                        // This will require that projects referenced by an msbuild project
                        // must be external projects.
                        dependency.LibraryRange.TypeConstraint &= ~LibraryDependencyTarget.Project;
                    }
                }
            }

            if (externalReference != null)
            {
                var childReferences = GetChildReferences(externalReference);
                var childReferenceNames = childReferences.Select(reference => reference.ProjectName).ToList();

                // External references are created without pivoting on the TxM. Here we need to account for this
                // and filter out references except the nearest TxM.
                var filteredExternalDependencies = new HashSet<string>(
                    childReferenceNames,
                    StringComparer.OrdinalIgnoreCase);

                // Non-Xproj projects may only have one TxM, all external references should be 
                // included if this is an msbuild based project.
                if (packageSpec != null
                    && !XProjUtility.IsMSBuildBasedProject(externalReference.MSBuildProjectPath))
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
                foreach (var dependency in dependencies.Where(d => IsProject(d)
                    && filteredExternalDependencies.Contains(d.Name)))
                {
                    dependency.LibraryRange.TypeConstraint = LibraryDependencyTarget.ExternalProject;
                }

                // Add dependencies passed in externally
                // These are usually msbuild references which have less metadata, they have
                // the lowest priority.
                // Note: Only add in dependencies that are in the filtered list to avoid getting the wrong TxM
                dependencies.AddRange(childReferences
                    .Where(reference => filteredExternalDependencies.Contains(reference.ProjectName))
                    .Select(reference => new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = reference.ProjectName,
                            VersionRange = VersionRange.Parse("1.0.0"),
                            TypeConstraint = LibraryDependencyTarget.ExternalProject
                        }
                    }));
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

            library = new Library
            {
                LibraryRange = libraryRange,
                Identity = new LibraryIdentity
                {
                    Name = externalReference?.ProjectName ?? packageSpec.Name,
                    Version = packageSpec?.Version ?? NuGetVersion.Parse("1.0.0"),
                    Type = LibraryType.Project,
                },
                Path = packageSpec?.FilePath,
                Dependencies = uniqueDependencies,
                Resolved = true
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

            if (packageSpec != null)
            {
                // Record all frameworks in the project
                library[KnownLibraryProperties.ProjectFrameworks] = new List<NuGetFramework>(
                    packageSpec.TargetFrameworks.Select(fw => fw.FrameworkName));
            }

            if (targetFrameworkInfo != null)
            {
                library[KnownLibraryProperties.TargetFrameworkInformation] = targetFrameworkInfo;

                // Add framework references
                var frameworkReferences = targetFrameworkInfo.Dependencies
                    .Where(d => d.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference)
                    .Select(d => d.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                library[KnownLibraryProperties.FrameworkAssemblies] = frameworkReferences;

                // Add a compile asset for msbuild to xproj projects
                if (targetFrameworkInfo.FrameworkName != null
                    && msbuildPath?.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var tfmFolder = targetFrameworkInfo.FrameworkName.GetShortFolderName();

                    // Projects under solution folders will have names such as src\\MyProject
                    // For the purpose of finding the output assembly just take the last part of the name
                    var projectName = packageSpec.Name.Split(
                        new char[] { '/', '\\' },
                        StringSplitOptions.RemoveEmptyEntries)
                        .Last();

                    // Currently the assembly name cannot be changed for xproj, we can construct the path to where
                    // the output should be.
                    var asset = $"{tfmFolder}/{projectName}.dll";
                    library[KnownLibraryProperties.CompileAsset] = asset;
                    library[KnownLibraryProperties.RuntimeAsset] = asset;
                }
            }

            return library;
        }

        /// <summary>
        /// Get and cache the package spec resolver.
        /// </summary>
        private IPackageSpecResolver GetPackageSpecResolver(string rootPath)
        {
            var specResolver = _defaultResolver;

            if (!string.IsNullOrEmpty(rootPath))
            {
                specResolver = _resolverCache.GetOrAdd(
                    rootPath,
                    _createPackageSpecResolver);
            }

            return specResolver;
        }

        private static readonly Func<string, PackageSpecResolver> _createPackageSpecResolver = CreatePackageSpecResolver;

        private static PackageSpecResolver CreatePackageSpecResolver(string rootPath)
        {
            return new PackageSpecResolver(rootPath);
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

        private List<ExternalProjectReference> GetChildReferences(ExternalProjectReference parent)
        {
            var children = new List<ExternalProjectReference>(parent.ExternalProjectReferences.Count);

            foreach (var reference in parent.ExternalProjectReferences)
            {
                ExternalProjectReference childReference;
                if (!_externalProjectsByPath.TryGetValue(reference, out childReference))
                {
                    // Create a reference to mark that this project is unresolved here
                    childReference = new ExternalProjectReference(
                        uniqueName: reference,
                        packageSpec: null,
                        msbuildProjectPath: null,
                        projectReferences: Enumerable.Empty<string>());
                }

                children.Add(childReference);
            }

            return children;
        }
    }
}
