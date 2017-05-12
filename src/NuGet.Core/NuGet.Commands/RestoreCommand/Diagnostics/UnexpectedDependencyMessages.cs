// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Commands
{
    /// <summary>
    /// Log warnings for packages that did not resolve to the minimum version of the dependency range.
    /// </summary>
    public static class UnexpectedDependencyMessages
    {
        /// <summary>
        /// Log warnings for all project issues related to unexpected dependencies.
        /// </summary>
        public static async Task LogAsync(IEnumerable<IRestoreTargetGraph> graphs, PackageSpec project, ILogger logger)
        {
            var ignoreIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var graphList = graphs.AsList();

            // 1. Detect project dependency authoring issues in the current project.
            //    The user can fix these themselves.
            var projectMissingLowerBounds = GetProjectDependenciesMissingLowerBounds(project);
            ignoreIds.UnionWith(projectMissingLowerBounds.Select(e => e.LibraryId));
            await logger.LogMessagesAsync(DiagnosticUtility.MergeOnTargetGraph(projectMissingLowerBounds));

            // 2. Detect dependency and source issues across the entire graph 
            //    where the minimum version was not matched exactly.
            //    Ignore packages already logged by #1
            var missingMinimums = GetMissingLowerBounds(graphList, ignoreIds);
            ignoreIds.UnionWith(missingMinimums.Select(e => e.LibraryId));
            await logger.LogMessagesAsync(DiagnosticUtility.MergeOnTargetGraph(missingMinimums));

            // 3. Detect top level dependencies that have a version different from the specified version.
            //    Ignore packages already logged in #1 and #2 since those errors are more specific.
            var bumpedUp = GetBumpedUpDependencies(graphList, project, ignoreIds);
            await logger.LogMessagesAsync(DiagnosticUtility.MergeOnTargetGraph(bumpedUp));
        }

        /// <summary>
        /// Get warnings for packages that have dependencies on non-existant versions of packages
        /// and also for packages with ranges that have missing minimum versions.
        /// </summary>
        public static IEnumerable<RestoreLogMessage> GetMissingLowerBounds(IEnumerable<IRestoreTargetGraph> graphs, ISet<string> ignoreIds)
        {
            var messages = new List<RestoreLogMessage>();

            foreach (var graph in graphs)
            {
                messages.AddRange(graph.ResolvedDependencies
                                            .Distinct()
                                            .Where(e => !ignoreIds.Contains(e.Child.Name, StringComparer.OrdinalIgnoreCase)
                                                        && DependencyRangeHasMissingExactMatch(e))
                                            .OrderBy(e => e.Child.Name, StringComparer.OrdinalIgnoreCase)
                                            .ThenBy(e => e.Child.Version)
                                            .ThenBy(e => e.Parent.Name, StringComparer.OrdinalIgnoreCase)
                                            .Select(e => GetMissingLowerBoundMessage(e, graph.Name)));
            }

            return messages;
        }

        /// <summary>
        /// Get warning message for missing minimum dependencies.
        /// </summary>
        public static RestoreLogMessage GetMissingLowerBoundMessage(ResolvedDependencyKey dependency, params string[] targetGraphs)
        {
            NuGetLogCode code;
            var message = string.Empty;
            var parent = DiagnosticUtility.FormatIdentity(dependency.Parent);
            var dependencyRange = DiagnosticUtility.FormatDependency(dependency.Child.Name, dependency.Range);
            var missingChild = DiagnosticUtility.FormatExpectedIdentity(dependency.Child.Name, dependency.Range);
            var resolvedChild = DiagnosticUtility.FormatIdentity(dependency.Child);

            if (HasMissingLowerBound(dependency.Range))
            {
                // Range does not have a lower bound, the best match can only be approximate.
                message = string.Format(CultureInfo.CurrentCulture, Strings.Warning_MinVersionNonInclusive,
                    parent,
                    dependencyRange,
                    resolvedChild);

                code = NuGetLogCode.NU1602;
            }
            else
            {
                // The minimum version does not exist.
                message = string.Format(CultureInfo.CurrentCulture, Strings.Warning_MinVersionDoesNotExist,
                    parent,
                    dependencyRange,
                    missingChild,
                    resolvedChild);

                code = NuGetLogCode.NU1603;
            }

            return RestoreLogMessage.CreateWarning(code, message, dependency.Child.Name, targetGraphs);
        }

        /// <summary>
        /// Warn for dependencies that have been bumped up.
        /// </summary>
        public static IEnumerable<RestoreLogMessage> GetBumpedUpDependencies(
            IEnumerable<IRestoreTargetGraph> graphs,
            PackageSpec project,
            ISet<string> ignoreIds)
        {
            var messages = new List<RestoreLogMessage>();

            // Group by framework to get project dependencies, then check each graph.
            foreach (var frameworkGroup in graphs.GroupBy(e => e.Framework))
            {
                // Get dependencies from the project
                var dependencies = project.GetPackageDependenciesForFramework(frameworkGroup.Key)
                                              .Where(e => !ignoreIds.Contains(e.Name, StringComparer.OrdinalIgnoreCase))
                                              .Where(IsNonFloatingPackageDependency);

                foreach (var dependency in dependencies)
                {
                    // Graphs may have different versions of the resolved package
                    foreach (var graph in frameworkGroup)
                    {
                        // Ignore floating or version-less (project) dependencies
                        // Avoid warnings for non-packages
                        var match = graph.Flattened.GetItemById(dependency.Name);

                        if (match != null
                            && LibraryType.Package == match.Key.Type
                            && dependency.LibraryRange.VersionRange.IsMinInclusive
                            && match.Key.Version > dependency.LibraryRange.VersionRange.MinVersion)
                        {
                            var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_DependencyBumpedUp,
                                dependency.LibraryRange.Name,
                                dependency.LibraryRange.VersionRange.PrettyPrint(),
                                match.Key.Name,
                                match.Key.Version);

                            messages.Add(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1601, message, match.Key.Name, graph.Name));
                        }
                    }
                }
            }

            return messages;
        }

        /// <summary>
        /// Warn for project dependencies that do not include a lower bound on the version range.
        /// </summary>
        public static IEnumerable<RestoreLogMessage> GetProjectDependenciesMissingLowerBounds(PackageSpec project)
        {
            return project.GetAllPackageDependencies()
                   .Where(e => HasMissingLowerBound(e.LibraryRange.VersionRange))
                   .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                   .Select(e => RestoreLogMessage.CreateWarning(
                       code: NuGetLogCode.NU1604,
                       message: string.Format(CultureInfo.CurrentCulture, Strings.Warning_ProjectDependencyMissingLowerBound,
                                              DiagnosticUtility.FormatDependency(e.Name, e.LibraryRange.VersionRange)),
                       libraryId: e.Name));
        }

        /// <summary>
        /// True if the dependency version range has a min version that matches the resolved version.
        /// </summary>
        public static bool DependencyRangeHasMissingExactMatch(ResolvedDependencyKey dependency)
        {
            // Ignore floating
            if (dependency.Range.IsFloating)
            {
                return false;
            }

            // Ignore projects
            if (dependency.Child.Type != LibraryType.Package)
            {
                return false;
            }

            return (!dependency.Range.IsMinInclusive || dependency.Range.MinVersion != dependency.Child.Version);
        }

        /// <summary>
        /// True if the range has an obtainable version for the lower bound.
        /// </summary>
        public static bool HasMissingLowerBound(VersionRange range)
        {
            if (range == null)
            {
                return true;
            }

            // Ignore floating
            if (range.IsFloating)
            {
                return false;
            }

            return !range.IsMinInclusive || !range.HasLowerBound;
        }

        private static bool IsNonFloatingPackageDependency(this LibraryDependency dependency)
        {
            return (dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)
                && dependency.LibraryRange.VersionRange != null && !dependency.LibraryRange.VersionRange.IsFloating);
        }
    }
}
