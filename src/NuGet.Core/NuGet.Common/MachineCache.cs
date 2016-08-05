using System;
using System.IO;

namespace NuGet.Common
{
    /// <summary>
    /// The machine cache represents a location on the machine where packages are cached. It is a specific implementation of a local repository and can be used as such.
    /// NOTE: this is a shared location, and as such all IO operations need to be properly serialized
    /// </summary>
    public class MachineCache
    {
        private const string NuGetCachePathEnvironmentVariable = "NuGetCachePath";
        /// <summary>
        /// Determines the cache path to use for NuGet.exe. By default, NuGet caches files under %LocalAppData%\NuGet\Cache.
        /// This path can be overridden by specifying a value in the NuGetCachePath environment variable.
        /// </summary>
        public static string GetCachePath()
        {
            return GetCachePath(Environment.GetEnvironmentVariable, Environment.GetFolderPath);
        }

        public static string GetCachePath(Func<string, string> getEnvironmentVariable, Func<System.Environment.SpecialFolder, string> getFolderPath)
        {
            string cacheOverride = getEnvironmentVariable(NuGetCachePathEnvironmentVariable);
            if (!String.IsNullOrEmpty(cacheOverride))
            {
                return cacheOverride;
            }
            else
            {
                string localAppDataPath = getFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                if (String.IsNullOrEmpty(localAppDataPath))
                {
                    // there's a bug on Windows Azure Web Sites environment where calling through the Environment.GetFolderPath()
                    // will returns empty string, but the environment variable will return the correct value
                    localAppDataPath = getEnvironmentVariable("LocalAppData");
                }

                if (String.IsNullOrEmpty(localAppDataPath))
                {
                    return null;
                }
                return Path.Combine(localAppDataPath, "NuGet", "Cache");
            }
        }

    }
}