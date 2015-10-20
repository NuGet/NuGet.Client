// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public interface IPackageRestoreManager
    {
        /// <summary>
        /// Occurs when it is detected that the packages are missing or restored for the current solution.
        /// </summary>
        event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;

        /// <summary>
        /// PackageRestoredEvent which is raised after a package is restored.
        /// </summary>
        event EventHandler<PackageRestoredEventArgs> PackageRestoredEvent;

        /// <summary>
        /// PackageRestoredEvent which is raised if a package restore failed.
        /// </summary>
        event EventHandler<PackageRestoreFailedEventArgs> PackageRestoreFailedEvent;

        /// <summary>
        /// Get the packages in the solution given the <paramref name="solutionDirectory"></paramref>.
        /// </summary>
        /// <returns>
        /// Returns a list of package references and the corresponding project names on which
        /// each package is installed, alongwith a bool which determines if the package is missing
        /// </returns>
        Task<IEnumerable<PackageRestoreData>> GetPackagesInSolutionAsync(string solutionDirectory, CancellationToken token);

        /// <summary>
        /// Checks the current solution if there is any package missing.
        /// </summary>
        Task RaisePackagesMissingEventForSolutionAsync(string solutionDirectory, CancellationToken token);

        /// <summary>
        /// Restores the missing packages for the current solution.
        /// </summary>
        /// <remarks>
        /// Best use case is the restore button that shows up in the UI or powershell when certain packages
        /// are missing
        /// </remarks>
        /// <returns>Returns true if atleast one package was restored.</returns>
        Task<PackageRestoreResult> RestoreMissingPackagesInSolutionAsync(string solutionDirectory,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token);

        /// <summary>
        /// Restores the missing packages for a project. Returns true if atleast one package was restored.
        /// </summary>
        /// <remarks>Best use case is 'nuget.exe restore packages.config'</remarks>
        Task<PackageRestoreResult> RestoreMissingPackagesAsync(string solutionDirectory,
            NuGetProject nuGetProject,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token);

        /// <summary>
        /// Restores the package references if they are missing
        /// </summary>
        /// <param name="packages">
        /// This parameter is the list of package referneces mapped to the list of
        /// project names a package is installed on. This is most likely obtained by calling
        /// GetPackagesInSolutionAsync
        /// </param>
        /// <remarks>
        /// Best use case is when GetPackagesInSolutionAsync was already called, the result can be used
        /// in this method
        /// </remarks>
        /// <returns>
        /// Returns true if at least one package is restored. Raised package restored failed event with the
        /// list of project names.
        /// </returns>
        Task<PackageRestoreResult> RestoreMissingPackagesAsync(string solutionDirectory,
            IEnumerable<PackageRestoreData> packages,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token);
    }
}
