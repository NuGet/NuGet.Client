// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

using NuGet.Commands;
using NuGet.ProjectModel;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// An up to date checker for a solution.
    /// </summary>
    public interface ISolutionRestoreChecker
    {
        /// <summary>
        /// Given the current dependency graph spec, perform a fast up to date check and return the dirty projects.
        /// The checker itself caches the DependencyGraphSpec it is provided & the last restore status, reported through <see cref="ReportStatus(IReadOnlyList{RestoreSummary})"/>.
        /// Accounts for changes in the PackageSpec and marks all the parent projects as dirty as well.
        /// Additionally also ensures that the expected output files have the same timestamps as the last time a succesful status was reported through <see cref="ReportStatus(IReadOnlyList{RestoreSummary})"/>.
        /// </summary>
        /// <param name="dependencyGraphSpec">The current dependency graph spec.</param>
        /// <returns>Unique ids of the dirty projects</returns>
        /// <remarks>Note that this call is stateful. This method may end up caching the dependency graph spec, so do not invoke multiple times. Ideally <see cref="PerformUpToDateCheck(DependencyGraphSpec)"/> call should be followed by a <see cref="ReportStatus(IReadOnlyList{RestoreSummary})"/> call.</remarks>
        IEnumerable<string> PerformUpToDateCheck(DependencyGraphSpec dependencyGraphSpec);

        /// <summary>
        /// Report the status of all the projects restored. 
        /// </summary>
        /// <param name="restoreSummaries"></param>
        /// <remarks>Note that this call is stateful. This method may end up caching the dependency graph spec, so do not invoke multiple times. Ideally <see cref="PerformUpToDateCheck(DependencyGraphSpec)"/> call should be followed by a <see cref="ReportStatus(IReadOnlyList{RestoreSummary})"/> call.</remarks>

        void ReportStatus(IReadOnlyList<RestoreSummary> restoreSummaries);

        /// <summary>
        /// Clears any cached values. This is meant to mimic restores that overwrite the incremental restore optimizations.
        /// </summary>
        /// <returns></returns>
        void CleanCache();
    }
}
