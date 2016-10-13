// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        internal static readonly string CrossTargetingCondition = "'$(TargetFramework)' == ''";
        internal static readonly string TargetFrameworkCondition = "'$(TargetFramework)' == '{0}'";
        internal static readonly string ExcludeAllCondition = "'$(ExcludeRestorePackageImports)' != 'true'";

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

            var props = new List<MSBuildRestoreImportGroup>();
            var targets = new List<MSBuildRestoreImportGroup>();

            // Conditionals for targets and props are only supported by NETCore
            if (project.RestoreMetadata?.OutputType == RestoreOutputType.NETCore)
            {
                // Add additional conditionals for cross targeting
                var isCrossTargeting = request.Project.RestoreMetadata.CrossTargeting
                    || request.Project.TargetFrameworks.Count > 1;

                Debug.Assert((!request.Project.RestoreMetadata.CrossTargeting && (request.Project.TargetFrameworks.Count < 2)
                    || (request.Project.RestoreMetadata.CrossTargeting)),
                    "Invalid crosstargeting and framework count combination");

                if (isCrossTargeting)
                {
                    // Find all global targets from buildCrossTargeting
                    var crossTargetingAssets = GetTargetsAndPropsForCrossTargeting(
                            targetGraphs,
                            repositories,
                            context,
                            request,
                            includeFlagGraphs);

                    var crossProps = new MSBuildRestoreImportGroup();
                    crossProps.Position = 0;
                    crossProps.Conditions.Add(CrossTargetingCondition);
                    crossProps.Imports.AddRange(crossTargetingAssets.Props);
                    props.Add(crossProps);

                    var crossTargets = new MSBuildRestoreImportGroup();
                    crossTargets.Position = 0;
                    crossTargets.Conditions.Add(CrossTargetingCondition);
                    crossTargets.Imports.AddRange(crossTargetingAssets.Targets);
                    targets.Add(crossTargets);
                }

                // Find TFM specific assets from the build folder
                foreach (var pair in buildAssetsByFramework)
                {
                    // There could be multiple string matches
                    foreach (var match in GetMatchingFrameworkStrings(project, pair.Key))
                    {
                        var frameworkCondition = string.Format(CultureInfo.InvariantCulture, TargetFrameworkCondition, match);

                        // Add entries regardless of if imports exist,
                        // this is needed to trigger conditionals
                        var propsGroup = new MSBuildRestoreImportGroup();

                        if (isCrossTargeting)
                        {
                            propsGroup.Conditions.Add(frameworkCondition);
                        }

                        propsGroup.Imports.AddRange(pair.Value.Props);
                        propsGroup.Position = 1;
                        props.Add(propsGroup);

                        var targetsGroup = new MSBuildRestoreImportGroup();

                        if (isCrossTargeting)
                        {
                            targetsGroup.Conditions.Add(frameworkCondition);
                        }

                        targetsGroup.Imports.AddRange(pair.Value.Targets);
                        targetsGroup.Position = 1;
                        targets.Add(targetsGroup);
                    }
                }
            }
            else
            {
                // Copy targets and props over, there can only be 1 tfm here
                // No conditionals are added
                var targetsAndProps = buildAssetsByFramework.First();

                var propsGroup = new MSBuildRestoreImportGroup();
                propsGroup.Imports.AddRange(targetsAndProps.Value.Props);
                props.Add(propsGroup);

                var targetsGroup = new MSBuildRestoreImportGroup();
                targetsGroup.Imports.AddRange(targetsAndProps.Value.Targets);
                targets.Add(targetsGroup);
            }

            // Add exclude all condition to all groups
            foreach (var group in props.Concat(targets))
            {
                group.Conditions.Add(ExcludeAllCondition);
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
            var buildGroupSets = GetMSBuildAssets(
                context,
                graph,
                request.Project,
                includeFlagGraphs,
                graph.Conventions.Patterns.MSBuildFiles);

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

        private static TargetsAndProps GetTargetsAndPropsForCrossTargeting(
            IEnumerable<RestoreTargetGraph> targetGraphs,
            IReadOnlyList<NuGetv3LocalRepository> repositories,
            RemoteWalkContext context,
            RestoreRequest request,
            Dictionary<RestoreTargetGraph,
            Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs)
        {
            var result = new TargetsAndProps();

            // Skip runtime graphs, msbuild targets may not come from RID specific packages
            // Order the graphs by framework to make this deterministic for scenarios where
            // TFMs disagree on the dependency order, there is little that can be done for
            // conflicts where A->B for TFM1 and B->A for TFM2.
            var ridlessGraphs = targetGraphs
                .Where(g => string.IsNullOrEmpty(g.RuntimeIdentifier))
                .OrderBy(g => g.Framework, new NuGetFrameworkSorter());

            // Gather props and targets to write out
            foreach (var graph in ridlessGraphs)
            {
                var globalGroupSets = GetMSBuildAssets(
                    context,
                    graph,
                    request.Project,
                    includeFlagGraphs,
                    graph.Conventions.Patterns.MSBuildCrossTargetingFiles);

                // Check if compatible build assets exist
                foreach (var globalGroupEntry in globalGroupSets)
                {
                    var libraryIdentity = globalGroupEntry.Key;
                    var buildGroupSet = globalGroupEntry.Value;

                    Debug.Assert(buildGroupSet.Length < 2, "Unexpected number of build global asset groups");

                    // There can only be one group since there are no TFMs here.
                    if (buildGroupSet.Length == 1)
                    {
                        // Add all targets and props from buildCrossTargeting
                        // Note: AddPropsAndTargets handles de-duping file paths. Since these non-TFM specific
                        // files are found for every TFM it is likely that there will be duplicates going in.
                        AddPropsAndTargets(
                                repositories,
                                libraryIdentity,
                                buildGroupSet[0],
                                result);
                    }
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
            Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs,
            PatternSet patternSet)
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
                            .FindItemGroups(patternSet)
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

            var targets = items
                .Where(c => Path.GetExtension(c.Path).Equals(".targets", StringComparison.OrdinalIgnoreCase))
                .Select(c =>
                    Path.Combine(pathResolver.GetInstallPath(libraryIdentity.Id, libraryIdentity.Version),
                    c.Path.Replace('/', Path.DirectorySeparatorChar)));

            // avoid duplicate targets
            foreach (var target in targets)
            {
                if (!targetsAndProps.Targets.Contains(target, StringComparer.Ordinal))
                {
                    targetsAndProps.Targets.Add(target);
                }
            }

            var props = items
                .Where(c => Path.GetExtension(c.Path).Equals(".props", StringComparison.OrdinalIgnoreCase))
                .Select(c =>
                    Path.Combine(pathResolver.GetInstallPath(libraryIdentity.Id, libraryIdentity.Version),
                    c.Path.Replace('/', Path.DirectorySeparatorChar)));

            foreach (var prop in props)
            {
                // avoid duplicate props
                if (!targetsAndProps.Props.Contains(prop, StringComparer.Ordinal))
                {
                    targetsAndProps.Props.Add(prop);
                }
            }
        }

        private class TargetsAndProps
        {
            public List<string> Targets { get; } = new List<string>();

            public List<string> Props { get; } = new List<string>();
        }
    }
}