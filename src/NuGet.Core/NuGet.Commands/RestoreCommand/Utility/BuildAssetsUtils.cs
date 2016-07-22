using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;

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
            // Non-Msbuild projects should skip targets and treat it as success
            if (!context.IsMsBuildBased && !ForceWriteTargets())
            {
                return new MSBuildRestoreResult(project.Name, project.BaseDirectory, success: true);
            }

            // Invalid msbuild projects should write out an msbuild error target
            if (!targetGraphs.Any())
            {
                return new MSBuildRestoreResult(project.Name, project.BaseDirectory, success: false);
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

            // Conditionals for targets and props by framework is not currently supported.
            if (NeedsMSBuildConditionals(buildAssetsByFramework))
            {
                return new MSBuildRestoreResult(project.Name, project.BaseDirectory, success: false);
            }

            // Since all targets and props are the same, any framework can be used.
            // There must be at least one framework here since there are target graphs (checked above)
            var combinedTargetsAndProps = buildAssetsByFramework.Values.First();

            // Sort the results by string case to keep the results consistent across internal nuget changes
            combinedTargetsAndProps.Props.Sort(StringComparer.Ordinal);
            combinedTargetsAndProps.Targets.Sort(StringComparer.Ordinal);

            // Targets files contain a macro for the repository root. If only the user package folder was used
            // allow a replacement. If fallback folders were used the macro cannot be applied.
            // Do not use macros for fallback folders. Use only the first repository which is the user folder.
            var repositoryRoot = repositories.First().RepositoryRoot;

            // Create a result which may be committed to disk later.
            return new MSBuildRestoreResult(
                project.Name,
                project.BaseDirectory,
                repositoryRoot,
                combinedTargetsAndProps.Props,
                combinedTargetsAndProps.Targets);
        }

        /// <summary>
        /// Verifies that all targets and props assets are the same across frameworks. If there is a mismatch
        /// this will return false.
        /// A single framework will always return true.
        /// </summary>
        private static bool NeedsMSBuildConditionals(Dictionary<NuGetFramework, TargetsAndProps> buildAssetsByFramework)
        {
            if (buildAssetsByFramework.Count > 1)
            {
                var combinedAssets = buildAssetsByFramework.Select(entry =>
                    entry.Value.Targets
                        .Concat(entry.Value.Props)
                        .OrderBy(s => s, StringComparer.Ordinal)
                        .ToArray())
                   .ToArray();

                // Compare all groups to the first group
                return combinedAssets.Skip(1).Any(group => !combinedAssets[0].SequenceEqual(group));
            }

            return false;
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
        private static Dictionary<LibraryIdentity, ContentItemGroup[]> GetMSBuildAssets(
            RemoteWalkContext context,
            RestoreTargetGraph graph,
            PackageSpec project,
            Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs)
        {
            var buildGroupSets = new Dictionary<LibraryIdentity, ContentItemGroup[]>();

            var flattenedFlags = IncludeFlagUtils.FlattenDependencyTypes(includeFlagGraphs, project, graph);

            // First find all msbuild items in the packages
            foreach (var library in graph.Flattened
                .Distinct()
                .OrderBy(g => g.Data.Match.Library))
            {
                var includeLibrary = true;

                LibraryIncludeFlags libraryFlags;
                if (flattenedFlags.TryGetValue(library.Key.Name, out libraryFlags))
                {
                    includeLibrary = libraryFlags.HasFlag(LibraryIncludeFlags.Build);
                }

                // Skip libraries that do not include build files such as transitive packages
                if (includeLibrary)
                {
                    var packageIdentity = new PackageIdentity(library.Key.Name, library.Key.Version);
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

                        buildGroupSets.Add(library.Key, buildGroupSet);
                    }
                }
            }

            return buildGroupSets;
        }

        /// <summary>
        /// Add all valid targets and props to the passed in lists.
        /// Modifies targetsAndProps
        /// </summary>
        private static void AddPropsAndTargets(
            IReadOnlyList<NuGetv3LocalRepository> repositories,
            LibraryIdentity libraryIdentity,
            ContentItemGroup buildItems,
            TargetsAndProps targetsAndProps)
        {
            // We need to additionally filter to items that are named "{packageId}.targets" and "{packageId}.props"
            // Filter by file name here and we'll filter by extension when we add things to the lists.
            var items = buildItems.Items
                .Where(item =>
                    Path.GetFileNameWithoutExtension(item.Path)
                    .Equals(libraryIdentity.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var packageInfo = NuGetv3LocalRepositoryUtility.GetPackage(repositories, libraryIdentity.Name, libraryIdentity.Version);
            var pathResolver = packageInfo.Repository.PathResolver;

            targetsAndProps.Targets.AddRange(items
                .Where(c => Path.GetExtension(c.Path).Equals(".targets", StringComparison.OrdinalIgnoreCase))
                .Select(c =>
                    Path.Combine(pathResolver.GetInstallPath(libraryIdentity.Name, libraryIdentity.Version),
                    c.Path.Replace('/', Path.DirectorySeparatorChar))));

            targetsAndProps.Props.AddRange(items
                .Where(c => Path.GetExtension(c.Path).Equals(".props", StringComparison.OrdinalIgnoreCase))
                .Select(c =>
                    Path.Combine(pathResolver.GetInstallPath(libraryIdentity.Name, libraryIdentity.Version),
                    c.Path.Replace('/', Path.DirectorySeparatorChar))));
        }

        private class TargetsAndProps
        {
            public List<string> Targets { get; } = new List<string>();

            public List<string> Props { get; } = new List<string>();
        }
    }
}
