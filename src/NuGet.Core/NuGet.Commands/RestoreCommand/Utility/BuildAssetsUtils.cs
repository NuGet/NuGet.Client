// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public static class BuildAssetsUtils
    {
        internal static MSBuildRestoreResult RestoreMSBuildFiles(PackageSpec project,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            IReadOnlyList<NuGetv3LocalRepository> repositories,
            RemoteWalkContext context,
            RestoreRequest request,
            Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs)
        {
            var targetsPath = Path.Combine(request.RestoreOutputPath, $"{project.Name}.nuget.targets");
            var propsPath = Path.Combine(request.RestoreOutputPath, $"{project.Name}.nuget.props");

            if (request.RestoreOutputType == RestoreOutputType.NETCore)
            {
                var projFileName = Path.GetFileName(request.Project.RestoreMetadata.ProjectPath);

                targetsPath = Path.Combine(request.RestoreOutputPath, $"{projFileName}.nuget.g.targets");
                propsPath = Path.Combine(request.RestoreOutputPath, $"{projFileName}.nuget.g.props");
            }

            // Non-Msbuild projects should skip targets and treat it as success
            if (!context.IsMsBuildBased && !ForceWriteTargets())
            {
                return new MSBuildRestoreResult(targetsPath, propsPath, success: true);
            }

            // Invalid msbuild projects should write out an msbuild error target
            if (!targetGraphs.Any())
            {
                return new MSBuildRestoreResult(targetsPath, propsPath, success: false);
            }

            // Framework -> (targets, props)
            var buildAssetsByFramework = new Dictionary<NuGetFramework, TargetsAndProps>();

            // Get assets for each framework
            foreach (var projectFramework in project.TargetFrameworks.Select(f => f.FrameworkName))
            {
                var targetsAndProps =
                    GetTargetsAndPropsForFramework(
                        targetGraphs,
                        repositories,
                        context,
                        request,
                        includeFlagGraphs,
                        projectFramework);

                buildAssetsByFramework.Add(projectFramework, targetsAndProps);
            }

            var props = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);
            var targets = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);

            // Conditionals for targets and props are only supported by NETCore
            if (project.RestoreMetadata?.OutputType == RestoreOutputType.NETCore)
            {
                foreach (var pair in buildAssetsByFramework)
                {
                    // There could be multiple string matches
                    foreach (var match in GetMatchingFrameworkStrings(project, pair.Key))
                    {
                        // Add entries regardless of if imports exist,
                        // this is needed to trigger conditionals
                        if (!props.ContainsKey(match))
                        {
                            props.Add(match, pair.Value.Props);
                        }

                        if (!targets.ContainsKey(match))
                        {
                            targets.Add(match, pair.Value.Targets);
                        }
                    }
                }
            }
            else
            {
                // Copy targets and props over, there can only be 1 tfm here
                var targetsAndProps = buildAssetsByFramework.First();
                props.Add(string.Empty, targetsAndProps.Value.Props);
                targets.Add(string.Empty, targetsAndProps.Value.Targets);
            }

            // Targets files contain a macro for the repository root. If only the user package folder was used
            // allow a replacement. If fallback folders were used the macro cannot be applied.
            // Do not use macros for fallback folders. Use only the first repository which is the user folder.
            var repositoryRoot = repositories.First().RepositoryRoot;

            // Create a result which may be committed to disk later.
            return new MSBuildRestoreResult(
                targetsPath,
                propsPath,
                repositoryRoot,
                props,
                targets);
        }

        private static HashSet<string> GetMatchingFrameworkStrings(PackageSpec spec, NuGetFramework framework)
        {
            // Ignore case since msbuild does
            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            matches.UnionWith(spec.RestoreMetadata.OriginalTargetFrameworks
                .Where(s => framework.Equals(NuGetFramework.Parse(s))));

            // If there were no matches, use the generated name
            if (matches.Count < 1)
            {
                matches.Add(framework.GetShortFolderName());
            }

            return matches;
        }

        private static TargetsAndProps GetTargetsAndPropsForFramework(
            IEnumerable<RestoreTargetGraph> targetGraphs,
            IReadOnlyList<NuGetv3LocalRepository> repositories,
            RemoteWalkContext context,
            RestoreRequest request,
            Dictionary<RestoreTargetGraph,
            Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs,
            NuGetFramework projectFramework)
        {
            var result = new TargetsAndProps();

            // Skip runtime graphs, msbuild targets may not come from RID specific packages
            var graph = targetGraphs
                .Single(g => string.IsNullOrEmpty(g.RuntimeIdentifier) && g.Framework.Equals(projectFramework));

            // Gather props and targets to write out
            var buildGroupSets = GetMSBuildAssets(context, graph, request.Project, includeFlagGraphs);

            // Second find the nearest group for each framework
            foreach (var buildGroupSetsEntry in buildGroupSets)
            {
                var libraryIdentity = buildGroupSetsEntry.Key;
                var buildGroupSet = buildGroupSetsEntry.Value;

                // Find the nearest msbuild group, this can include the root level Any group.
                var buildItems = NuGetFrameworkUtility.GetNearest(
                        buildGroupSet,
                        graph.Framework,
                        group =>
                            group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker]
                                as NuGetFramework);

                // Check if compatible build assets exist
                if (buildItems != null)
                {
                    AddPropsAndTargets(repositories, libraryIdentity, buildItems, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Check if NUGET_XPROJ_WRITE_TARGETS is true.
        /// </summary>
        private static bool ForceWriteTargets()
        {
            var envVar = Environment.GetEnvironmentVariable("NUGET_XPROJ_WRITE_TARGETS");

            bool forceWriteTargets = false;
            if (!string.IsNullOrEmpty(envVar))
            {
                Boolean.TryParse(envVar, out forceWriteTargets);
            }

            return forceWriteTargets;
        }

        /// <summary>
        /// Find all included msbuild assets for a graph.
        /// </summary>
        private static Dictionary<PackageIdentity, ContentItemGroup[]> GetMSBuildAssets(
            RemoteWalkContext context,
            RestoreTargetGraph graph,
            PackageSpec project,
            Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs)
        {
            var buildGroupSets = new Dictionary<PackageIdentity, ContentItemGroup[]>();

            var flattenedFlags = IncludeFlagUtils.FlattenDependencyTypes(includeFlagGraphs, project, graph);

            // convert graph items to package dependency info list
            var dependencies = ConvertToPackageDependencyInfo(graph.Flattened);

            // sort graph nodes by dependencies order
            var sortedItems = TopologicalSortUtility.SortPackagesByDependencyOrder(dependencies);

            // First find all msbuild items in the packages
            foreach (var library in sortedItems)
            {
                var includeLibrary = true;

                LibraryIncludeFlags libraryFlags;
                if (flattenedFlags.TryGetValue(library.Id, out libraryFlags))
                {
                    includeLibrary = libraryFlags.HasFlag(LibraryIncludeFlags.Build);
                }

                // Skip libraries that do not include build files such as transitive packages
                if (includeLibrary)
                {
                    var packageIdentity = new PackageIdentity(library.Id, library.Version);
                    IList<string> packageFiles;
                    context.PackageFileCache.TryGetValue(packageIdentity, out packageFiles);

                    if (packageFiles != null)
                    {
                        var contentItemCollection = new ContentItemCollection();
                        contentItemCollection.Load(packageFiles);

                        // Find MSBuild groups
                        var buildGroupSet = contentItemCollection
                            .FindItemGroups(graph.Conventions.Patterns.MSBuildFiles)
                            .ToArray();

                        buildGroupSets.Add(packageIdentity, buildGroupSet);
                    }
                }
            }

            return buildGroupSets;
        }

        private static HashSet<PackageDependencyInfo> ConvertToPackageDependencyInfo(
            ISet<GraphItem<RemoteResolveResult>> items)
        {
            var result = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);
            
            foreach (var item in items)
            {
                var dependencies =
                    item.Data?.Dependencies?.Select(
                        dependency => new PackageDependency(dependency.Name, VersionRange.All));

                result.Add(new PackageDependencyInfo(item.Key.Name, item.Key.Version, dependencies));
            }

            return result;
        }

        /// <summary>
        /// Add all valid targets and props to the passed in lists.
        /// Modifies targetsAndProps
        /// </summary>
        private static void AddPropsAndTargets(
            IReadOnlyList<NuGetv3LocalRepository> repositories,
            PackageIdentity libraryIdentity,
            ContentItemGroup buildItems,
            TargetsAndProps targetsAndProps)
        {
            // We need to additionally filter to items that are named "{packageId}.targets" and "{packageId}.props"
            // Filter by file name here and we'll filter by extension when we add things to the lists.
            var items = buildItems.Items
                .Where(item =>
                    Path.GetFileNameWithoutExtension(item.Path)
                    .Equals(libraryIdentity.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var packageInfo = NuGetv3LocalRepositoryUtility.GetPackage(repositories, libraryIdentity.Id, libraryIdentity.Version);
            var pathResolver = packageInfo.Repository.PathResolver;

            targetsAndProps.Targets.AddRange(items
                .Where(c => Path.GetExtension(c.Path).Equals(".targets", StringComparison.OrdinalIgnoreCase))
                .Select(c =>
                    Path.Combine(pathResolver.GetInstallPath(libraryIdentity.Id, libraryIdentity.Version),
                    c.Path.Replace('/', Path.DirectorySeparatorChar))));

            targetsAndProps.Props.AddRange(items
                .Where(c => Path.GetExtension(c.Path).Equals(".props", StringComparison.OrdinalIgnoreCase))
                .Select(c =>
                    Path.Combine(pathResolver.GetInstallPath(libraryIdentity.Id, libraryIdentity.Version),
                    c.Path.Replace('/', Path.DirectorySeparatorChar))));
        }

        private class TargetsAndProps
        {
            public List<string> Targets { get; } = new List<string>();

            public List<string> Props { get; } = new List<string>();
        }
    }
}
