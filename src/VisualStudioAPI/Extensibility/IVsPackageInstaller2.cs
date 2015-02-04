using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    public interface IVsPackageInstaller2 : IVsPackageInstaller
    {
        /// <summary>
        /// Installs a package using an ordered list of sources and a version spec.
        /// The highest available package within the version range.
        /// </summary>
        /// <remarks>This method is intended to allow extensions to install the latest version of a package from online, or fall back to 
        /// an existing package in the extension.</remarks>
        /// <remarks>See http://docs.nuget.org/create/versioning for versionSpec format details.</remarks>
        /// <param name="sources">An ordered list of sources from which the package may be installed.</param>
        /// <param name="project">The target project for package installation</param>
        /// <param name="packageId">Package Id</param>
        /// <param name="versionSpec">Allowed version range for the package.</param>
        /// <param name="ignoreDependencies">A boolean indicating whether the package's dependencies should be ignored</param>
        Task InstallPackageAsync(Project project, IEnumerable<string> sources, string packageId, string versionSpec, bool ignoreDependencies, CancellationToken token);

        /// <summary>
        /// Installs a single package from the specified package source.
        /// </summary>
        /// <param name="source">The package source to install the package from.</param>
        /// <param name="project">The target project for package installation.</param>
        /// <param name="packageId">The package id of the package to install.</param>
        /// <param name="version">The version of the package to install</param>
        /// <param name="ignoreDependencies">A boolean indicating whether or not to ignore the package's dependencies during installation.</param>
        Task InstallPackageAsync(IEnumerable<string> sources, Project project, string packageId, string version, bool ignoreDependencies, CancellationToken token);
    }
}
