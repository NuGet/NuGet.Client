using NuGet.Packaging.Core;
using System;
using System.Globalization;
using System.IO;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Utilities for project.json
    /// </summary>
    public static class BuildIntegratedProjectUtility
    {
        /// <summary>
        /// project.json
        /// </summary>
        public const string ProjectConfigFileName = "project.json";

        /// <summary>
        /// Lock file name
        /// </summary>
        public const string ProjectLockFileName = "project.lock.json";

        /// <summary>
        /// nupkg path from the global cache folder
        /// </summary>
        public static string GetNupkgPathFromGlobalSource(PackageIdentity identity)
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string nupkgName = String.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", identity.Id, identity.Version.ToNormalizedString());

            return Path.Combine(GetGlobalPackagesFolder(), identity.Id, identity.Version.ToNormalizedString(), nupkgName);
        }

        /// <summary>
        /// Global package folder path
        /// </summary>
        public static string GetGlobalPackagesFolder()
        {
            string path = Environment.GetEnvironmentVariable("NUGET_GLOBAL_PACKAGE_CACHE");

            if (String.IsNullOrEmpty(path))
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                path = Path.Combine(userProfile, ".dnu\\packages\\");
            }

            return path;
        }

        /// <summary>
        /// Create the lock file path from the config file path.
        /// </summary>
        public static string GetLockFilePath(string configFilePath)
        {
            return Path.Combine(Path.GetDirectoryName(configFilePath), ProjectLockFileName);
        }
    }
}
