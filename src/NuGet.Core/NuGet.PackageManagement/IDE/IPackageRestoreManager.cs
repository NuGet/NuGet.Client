// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public interface IPackageRestoreManager
    {
        /// <summary>
        /// Gets a value indicating whether the current solution is configured for Package Restore mode.
        /// </summary>
        [Obsolete("Enabling and querying legacy package restore is not supported in VS 2015 RTM.")]
        bool IsCurrentSolutionEnabledForRestore { get; }

        /// <summary>
        /// Configures the current solution for Package Restore mode.
        /// </summary>
        /// <param name="fromActivation">
        /// if set to <c>false</c>, the method will not show any error message, and will
        /// not set package restore consent.
        /// </param>
        [Obsolete("Enabling and querying legacy package restore is not supported in VS 2015 RTM.")]
        void EnableCurrentSolutionForRestore(bool fromActivation);

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
            SourceCacheContext cacheContext,
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
            SourceCacheContext cacheContext,
            CancellationToken token);
    }

    /// <summary>
    /// If 'Restored' is false, it means that the package was already restored
    /// If 'Restored' is true, the package was restored and successfully
    /// </summary>
    public class PackageRestoredEventArgs : EventArgs
    {
        public PackageIdentity Package { get; private set; }
        public bool Restored { get; private set; }

        public PackageRestoredEventArgs(PackageIdentity packageIdentity, bool restored)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            Package = packageIdentity;
            Restored = restored;
        }
    }

    public class PackagesMissingStatusEventArgs : EventArgs
    {
        public bool PackagesMissing { get; private set; }

        public PackagesMissingStatusEventArgs(bool packagesMissing)
        {
            PackagesMissing = packagesMissing;
        }
    }
}
