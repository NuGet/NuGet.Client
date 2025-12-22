// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.LibraryModel;
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
            var dependencyGraphPerFramework = new Dictionary<string, List<DependencyNode>?>(assetsFile.Targets.Count);
            bool doesProjectHaveDependencyOnPackage = false;

            // add null to the list of runtime identifiers to account for projects that do not have a runtime identifier
            var runtimeIdentifiers = assetsFile.PackageSpec.RuntimeGraph.Runtimes.Keys
                .Append(null)
                .ToList();
            // get all top-level package and project references for the project, categorized by target framework alias
            Dictionary<string, List<LibraryRange>> topLevelReferencesByFramework = GetTopLevelPackageAndProjectReferences(assetsFile, userInputFrameworks);

            if (topLevelReferencesByFramework.Count > 0)
            {
                foreach (var (targetFrameworkAlias, topLevelReferences) in topLevelReferencesByFramework)
                {
                    foreach (var runtimeIdentifier in runtimeIdentifiers)
                    {
                        var targetFrameworkDisplayName = runtimeIdentifier == null ? targetFrameworkAlias : $"{targetFrameworkAlias}/{runtimeIdentifier}";

                        LockFileTarget target = assetsFile.GetTarget(targetFrameworkAlias, runtimeIdentifier: runtimeIdentifier);

                        // get all package libraries for the framework
                        IList<LockFileTargetLibrary>? packageLibraries = target.Libraries;

                        // if the project has a dependency on the target package, get the dependency graph
                        if (packageLibraries?.Any(l => l?.Name?.Equals(targetPackage, StringComparison.OrdinalIgnoreCase) == true) == true)
                        {
                            doesProjectHaveDependencyOnPackage = true;
                            dependencyGraphPerFramework.Add(targetFrameworkDisplayName,
                                GetDependencyGraphForTargetPerFramework(topLevelReferences, packageLibraries, targetPackage));
                        }
                        else
                        {
                            dependencyGraphPerFramework.Add(targetFrameworkDisplayName, null);
                        }
                    }
                }
            }

            return doesProjectHaveDependencyOnPackage
                ? dependencyGraphPerFramework
                : null;
        }

        /// <summary>
        /// Finds all dependency paths from the top-level packages to the target package for a given framework.
        /// </summary>
        /// <param name="topLevelReferences">All top-level package and project references for the framework.</param>
        /// <param name="packageLibraries">All package libraries for the framework.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns>
        /// List of all top-level package nodes in the dependency graph.
        /// </returns>
        private static List<DependencyNode>? GetDependencyGraphForTargetPerFramework(
            List<LibraryRange> topLevelReferences,
            IList<LockFileTargetLibrary> packageLibraries,
            string targetPackage)
        {
            List<DependencyNode>? dependencyGraph = null;

            // dictionary mapping packageIds to their resolved version and type
            (Dictionary<string, string> versions, Dictionary<string, bool> isProjectMap) = GetAllResolvedVersionsAndTypes(packageLibraries);

            // dictionary tracking all package nodes that have been added to the graph, mapped to their DependencyNode objects
            // this allows sharing of nodes when the same package is reached via multiple paths
            var dependencyNodes = new Dictionary<string, DependencyNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var topLevelReference in topLevelReferences)
            {
                // use depth-first search to find dependency paths from the top-level package to the target package
                DependencyNode? topLevelNode = FindDependencyPathForTarget(
                    topLevelReference.Name,
                    topLevelReference.VersionRange?.ToString("p", VersionRangeFormatter.Instance),
                    packageLibraries,
                    dependencyNodes,
                    versions,
                    isProjectMap,
                    targetPackage);

                if (topLevelNode != null)
                {
                    dependencyGraph ??= [];
                    dependencyGraph.Add(topLevelNode);
                }
            }

            return dependencyGraph;
        }

        /// <summary>
        /// Traverses the dependency graph for a given package, looking for paths to the target package.
        /// </summary>
        /// <param name="packageId">Package ID to traverse.</param>
        /// <param name="requestedVersion">The version range requested for this package.</param>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        /// <param name="dependencyNodes">Dictionary tracking all packageIds that were added to the graph, mapped to their DependencyNode objects.</param>
        /// <param name="versions">Dictionary mapping packageIds to their resolved versions.</param>
        /// <param name="isProjectMap">Dictionary mapping packageIds to whether they are projects.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns>
        /// The package node in the dependency graph (if a path was found), or null (if no path was found).
        /// </returns>
        private static DependencyNode? FindDependencyPathForTarget(
            string packageId,
            string? requestedVersion,
            IList<LockFileTargetLibrary> packageLibraries,
            Dictionary<string, DependencyNode> dependencyNodes,
            Dictionary<string, string> versions,
            Dictionary<string, bool> isProjectMap,
            string targetPackage)
        {
            // Create a unique key combining packageId and requestedVersion to handle cases where
            // the same package is reached via multiple paths with different version requirements
            string nodeKey = $"{packageId}|{requestedVersion}";

            // if we've already processed this node and determined its children, return it
            if (dependencyNodes.TryGetValue(nodeKey, out var existingNode))
            {
                return existingNode;
            }

            // check if this package exists in the resolved dependencies
            if (!versions.TryGetValue(packageId, out var resolvedVersion))
            {
                return null;
            }

            // create a node for this package (we'll determine if it should be added to the graph after checking its children)
            bool isProject = isProjectMap.TryGetValue(packageId, out bool isProj) && isProj;
            // For projects, use empty string as version since it won't be displayed
            string version = isProject ? string.Empty : resolvedVersion;
            var currentNode = new DependencyNode(packageId, version, requestedVersion, isProject);

            // to prevent infinite recursion in case of circular dependencies, add to dictionary before processing children
            dependencyNodes[nodeKey] = currentNode;

            bool hasPathToTarget = packageId.Equals(targetPackage, StringComparison.OrdinalIgnoreCase);

            // get all dependencies for the current package
            var dependencies = packageLibraries?.FirstOrDefault(i => i?.Name?.Equals(packageId, StringComparison.OrdinalIgnoreCase) == true)?.Dependencies;

            if (dependencies?.Count > 0)
            {
                foreach (var dependency in dependencies)
                {
                    string dependencyRequestedVersion = dependency.VersionRange.ToString("p", VersionRangeFormatter.Instance);
                    var childNode = FindDependencyPathForTarget(
                        dependency.Id,
                        dependencyRequestedVersion,
                        packageLibraries!,
                        dependencyNodes,
                        versions,
                        isProjectMap,
                        targetPackage);

                    if (childNode != null)
                    {
                        currentNode.Children.Add(childNode);
                        hasPathToTarget = true;
                    }
                }
            }

            // if this node has no path to target, remove it from the dictionary and return null
            if (!hasPathToTarget)
            {
                dependencyNodes.Remove(nodeKey);
                return null;
            }

            return currentNode;
        }

        /// <summary>
        /// Get all top-level package and project references for the given project.
        /// </summary>
        /// <param name="assetsFile">Assets file for the project.</param>
        /// <param name="userInputFrameworks">List of target framework aliases.</param>
        /// <returns>
        /// Dictionary mapping the project's target framework aliases to their respective top-level package and project references.
        /// </returns>
        private static Dictionary<string, List<LibraryRange>> GetTopLevelPackageAndProjectReferences(
            LockFile assetsFile,
            List<string> userInputFrameworks)
        {
            var topLevelReferences = new Dictionary<string, List<LibraryRange>>();

            var targetAliases = assetsFile.PackageSpec.RestoreMetadata.OriginalTargetFrameworks;

            // filter the targets to the set of targets that the user has specified
            if (userInputFrameworks?.Count > 0)
            {
                targetAliases = targetAliases.Where(f => userInputFrameworks.Contains(f)).ToList();
            }

            // we need to match top-level project references to their target library entries using their paths,
            // so we will store all project reference paths in a dictionary here
            var projectLibraries = assetsFile.Libraries.Where(l => l.Type == "project");
            var projectLibraryPathToName = new Dictionary<string, LibraryRange>(projectLibraries.Count());
            var projectDirectoryPath = Path.GetDirectoryName(assetsFile.PackageSpec.FilePath);

            if (projectDirectoryPath != null)
            {
                foreach (var library in projectLibraries)
                {
                    var projectInfo = new LibraryRange(library.Name, LibraryDependencyTarget.Project);
                    projectLibraryPathToName.Add(Path.GetFullPath(library.Path, projectDirectoryPath), projectInfo);
                }
            }

            // get all top-level references for each target alias
            foreach (string targetAlias in targetAliases)
            {
                topLevelReferences.Add(targetAlias, []);

                // top-level packages
                TargetFrameworkInformation? targetFrameworkInformation = assetsFile.PackageSpec.TargetFrameworks.FirstOrDefault(tfi => tfi.TargetAlias.Equals(targetAlias, StringComparison.OrdinalIgnoreCase));
                if (targetFrameworkInformation != default)
                {
                    topLevelReferences[targetAlias].AddRange(targetFrameworkInformation.Dependencies.Select(d => d.LibraryRange));
                }

                // top-level projects
                ProjectRestoreMetadataFrameworkInfo? restoreMetadataFrameworkInfo = assetsFile.PackageSpec.RestoreMetadata.TargetFrameworks.FirstOrDefault(tfi => tfi.TargetAlias.Equals(targetAlias, StringComparison.OrdinalIgnoreCase));
                if (restoreMetadataFrameworkInfo != default)
                {
                    var topLevelProjectPaths = restoreMetadataFrameworkInfo.ProjectReferences.Select(p => p.ProjectPath);
                    foreach (var projectPath in topLevelProjectPaths)
                    {
                        topLevelReferences[targetAlias].Add(projectLibraryPathToName[projectPath]);
                    }
                }
            }

            return topLevelReferences;
        }

        /// <summary>
        /// Adds all resolved versions of packages to a dictionary, and tracks which are projects.
        /// </summary>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        private static (Dictionary<string, string>, Dictionary<string, bool>) GetAllResolvedVersionsAndTypes(IList<LockFileTargetLibrary> packageLibraries)
        {
            var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var isProjectMap = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var package in packageLibraries)
            {
                if (package?.Name != null && package?.Version != null)
                {
                    versions.Add(package.Name, package.Version.ToNormalizedString());
                    isProjectMap.Add(package.Name, package.Type?.Equals("project", StringComparison.OrdinalIgnoreCase) == true);
                }
            }

            return (versions, isProjectMap);
        }
    }
}
