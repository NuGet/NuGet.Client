using System.Collections.Generic;
using System.Linq;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Represents all necessary configuration for installing a list of preinstalled packages.
    /// </summary>
    internal sealed class PreinstalledPackageConfiguration
    {
        /// <summary>
        /// Creates a preinstalled package configuration.
        /// </summary>
        /// <param name="repositoryPath">The absolute path to the packages repository on disk.</param>
        /// <param name="packages">The list of packages to be installed.</param>
        /// <param name="isPreunzipped">A boolean indicating whether the packages are preunzipped within the repository path.</param>
        public PreinstalledPackageConfiguration(string repositoryPath, IEnumerable<PreinstalledPackageInfo> packages, bool isPreunzipped)
        {
            Packages = packages.ToList().AsReadOnly();
            RepositoryPath = repositoryPath;
            IsPreunzipped = isPreunzipped;
        }

        public ICollection<PreinstalledPackageInfo> Packages { get; private set; }
        public string RepositoryPath { get; private set; }
        public bool IsPreunzipped { get; private set; }
    }
}