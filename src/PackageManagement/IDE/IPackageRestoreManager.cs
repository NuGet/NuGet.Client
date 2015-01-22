using NuGet.Packaging;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public interface IPackageRestoreManager
    {
        /// <summary>
        /// Gets a value indicating whether the current solution is configured for Package Restore mode.
        /// </summary>
        bool IsCurrentSolutionEnabledForRestore { get; }

        /// <summary>
        /// Configures the current solution for Package Restore mode.
        /// </summary>
        /// <param name="fromActivation">if set to <c>false</c>, the method will not show any error message, and will not set package restore consent.</param>
        void EnableCurrentSolutionForRestore(bool fromActivation);

        /// <summary>
        /// Occurs when it is detected that the packages are missing or restored for the current solution.
        /// </summary>
        event EventHandler<PackagesMissingStatusEventArgs> PackagesMissingStatusChanged;

        /// <summary>
        /// Checks the current solution if there is any package missing.
        /// </summary>
        /// <returns></returns>
        void RaisePackagesMissingEventForSolution();

        /// <summary>
        /// Restores the missing packages for the current solution. Returns true if atleast one package was restored
        /// </summary>
        Task<bool> RestoreMissingPackagesInSolution();

        /// <summary>
        /// Restores the missing packages for a project. Returns true if atleast one package was restored
        /// </summary>
        Task<bool> RestoreMissingPackages(NuGetProject nuGetProject);

        /// <summary>
        /// Restores the passed in missing packages. Returns true if atleast one package was restored
        /// </summary>
        Task<bool> RestoreMissingPackages(IEnumerable<PackageReference> packageReferences);
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