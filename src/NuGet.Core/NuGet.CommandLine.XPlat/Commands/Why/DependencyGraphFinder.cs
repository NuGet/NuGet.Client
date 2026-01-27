// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Commands.Why
{
    internal static class DependencyGraphFinder
    {
        /// <summary>
        /// Finds all dependency graphs for a given project.
        /// </summary>
        /// <param name="assetsFile">Assets file for the project.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <param name="userInputFrameworks">List of target framework aliases.</param>
        /// <returns>
        /// Dictionary mapping target framework aliases to their respective dependency graphs.
        /// Returns null if the project does not have a dependency on the target package.
        /// </returns>
        public static Dictionary<string, List<DependencyNode>?>? GetAllDependencyGraphsForTarget(
            LockFile assetsFile,
            string targetPackage,
            List<string> userInputFrameworks)
        {
            var result = new Dictionary<string, List<DependencyNode>?>(StringComparer.OrdinalIgnoreCase);
            bool foundPackage = false;
            bool useTargetAlias = assetsFile.PackageSpec.TargetFrameworks.All(tf => !string.IsNullOrEmpty(tf.TargetAlias));
            if (!useTargetAlias
                && (assetsFile.PackageSpec.RestoreMetadata.OriginalTargetFrameworks.Count != 1
                    || assetsFile.PackageSpec.TargetFrameworks.Count != 1
                    || assetsFile.PackageSpec.RestoreMetadata.TargetFrameworks.Count != 1
                ))
            {
                throw new FileFormatException(Strings.WhyCommand_Error_InconsistentAssetsFile);
            }

            foreach (var target in assetsFile.Targets)
            {
                string targetAlias;
                ImmutableArray<LibraryDependency> directPackages;
                IList<ProjectRestoreReference> directProjectReferences;
                if (useTargetAlias)
                {
                    targetAlias = target.TargetAlias;
                    directPackages = assetsFile.PackageSpec.GetTargetFramework(targetAlias).Dependencies;
                    directProjectReferences = assetsFile.PackageSpec.GetRestoreMetadataFramework(targetAlias).ProjectReferences;
                }
                else
                {
                    targetAlias = assetsFile.PackageSpec.RestoreMetadata.OriginalTargetFrameworks[0];
                    directPackages = assetsFile.PackageSpec.TargetFrameworks[0].Dependencies;
                    directProjectReferences = assetsFile.PackageSpec.RestoreMetadata.TargetFrameworks[0].ProjectReferences;
                }

                if (userInputFrameworks.Count > 0
                    && !userInputFrameworks.Any(f => string.Equals(targetAlias, f, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string displayName = string.IsNullOrEmpty(target.RuntimeIdentifier)
                    ? targetAlias
                    : $"{targetAlias}/{target.RuntimeIdentifier}";

                LockFileTargetLibrary projectAsLibrary = ConvertToLibrary(directPackages, directProjectReferences, assetsFile, target);

                var graphBuilder = new TargetGraphBuilder
                {
                    Target = target,
                    FilterPackage = targetPackage
                };
                DependencyNode? projectNode = graphBuilder.CreateNode(projectAsLibrary, VersionRange.All);

                foundPackage |= projectNode != null;
                result[displayName] = projectNode?.Children.ToList();
            }

            return foundPackage ? result : null;
        }

        private static LockFileTargetLibrary ConvertToLibrary(
            ImmutableArray<LibraryDependency> directPackages,
            IList<ProjectRestoreReference> directProjectReferences,
            LockFile assetsFile,
            LockFileTarget target)
        {
            List<PackageDependency> dependencies = new List<PackageDependency>(directPackages.Length + directProjectReferences.Count);

            dependencies.AddRange(directPackages.Select(p => new PackageDependency(p.Name, p.LibraryRange.VersionRange ?? VersionRange.All)));

            string projectDirectory = Path.GetDirectoryName(assetsFile.PackageSpec.FilePath)!;
            var projectsByPath = assetsFile
                .Libraries
                .Where(l => string.Equals(l.Type, "project", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(l => Path.GetFullPath(Path.Combine(projectDirectory, l.Path)), l => l, StringComparer.OrdinalIgnoreCase);
            dependencies.AddRange(directProjectReferences
                .Select(p =>
                {
                    LockFileLibrary projectLibrary = projectsByPath[p.ProjectPath];
                    LibraryRange libraryRange = new LibraryRange(
                        projectLibrary.Name,
                        VersionRange.Parse(projectLibrary.Version.ToString()),
                        LibraryDependencyTarget.Project);
                    var dependency = new PackageDependency(
                        libraryRange.Name,
                        VersionRange.Parse(projectLibrary.Version.OriginalVersion ?? projectLibrary.Version.ToString()));
                    return dependency;
                }));

            LockFileTargetLibrary project = new LockFileTargetLibrary
            {
                Name = assetsFile.PackageSpec.Name,
                Type = "project",
                Dependencies = dependencies
            };

            return project;
        }

        private struct TargetGraphBuilder
        {
            public required LockFileTarget Target { get; init; }
            public required string FilterPackage { get; init; }

            public DependencyNode? CreateNode(LockFileTargetLibrary library, VersionRange requestedVersion)
            {
                HashSet<DependencyNode>? children = null;

                foreach (var dependency in library.Dependencies)
                {
                    LockFileTargetLibrary? dependencyLibrary = Target.Libraries.FirstOrDefault(l => l.Name!.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase));
                    if (dependencyLibrary is null)
                    {
                        // This feels like an error, but unless https://github.com/NuGet/Home/issues/14698 is fixed, we have to ignore it.
                        continue;
                    }
                    DependencyNode? childNode = CreateNode(dependencyLibrary, dependency.VersionRange);
                    if (childNode is not null)
                    {
                        if (children is null)
                        {
                            children = new HashSet<DependencyNode>();
                        }
                        children.Add(childNode);
                    }
                }

                if (!FilterPackage.Equals(library.Name, StringComparison.OrdinalIgnoreCase)
                    && children is null)
                {
                    return null;
                }

                if (library.Type!.Equals("package", StringComparison.OrdinalIgnoreCase))
                {
                    NuGetVersion resolvedVersion = library.Version!;
                    var newNode = new PackageNode(
                        library.Name!,
                        resolvedVersion,
                        requestedVersion,
                        children ?? []);
                    return newNode;
                }
                else
                {
                    var newNode = new ProjectNode(library.Name!, children ?? []);
                    return newNode;
                }
            }
        }
    }
}
