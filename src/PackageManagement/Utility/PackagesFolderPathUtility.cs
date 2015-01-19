using NuGet.Configuration;
using System;
using System.IO;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Static class to help get PackagesFolderPath
    /// </summary>
    public static class PackagesFolderPathUtility
    {
        private const string ConfigSection = "config";
        private const string RepositoryPathKey = "repositoryPath";
        private const string DefaultRepositoryPath = "packages";
        public static string GetPackagesFolderPath(ISolutionManager solutionManager, ISettings settings)
        {
            if(solutionManager == null)
            {
                throw new ArgumentNullException("solutionManager");
            }

            // If the solution directory is unavailable then throw an exception
            if(solutionManager.SolutionDirectory == null)
            {
                throw new InvalidOperationException(Strings.SolutionDirectoryNotAvailable);
            }

            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            string path = settings.GetValue(ConfigSection, RepositoryPathKey, isPath: true);
            if (!String.IsNullOrEmpty(path))
            {
                return Uri.UnescapeDataString(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            }
            return Path.Combine(solutionManager.SolutionDirectory, String.IsNullOrEmpty(path) ? DefaultRepositoryPath : path);
        }
    }
}
