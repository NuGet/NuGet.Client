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

            return GetPackagesFolderPath(solutionManager.SolutionDirectory, settings);
        }

        public static string GetPackagesFolderPath(string solutionDirectory, ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            string path = SettingsUtility.GetRepositoryPath(settings);
            if (!String.IsNullOrEmpty(path))
            {
                return Uri.UnescapeDataString(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
            }
            return Path.Combine(solutionDirectory, String.IsNullOrEmpty(path) ? DefaultRepositoryPath : path);
        }
    }
}
