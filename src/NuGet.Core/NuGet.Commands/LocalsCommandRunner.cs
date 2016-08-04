using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands
{
    public class LocalsCommandRunner
    {

        private const string _httpCacheResourceName = "http-cache";
        private const string _packagesCacheResourceName = "packages-cache";
        private const string _globalPackagesResourceName = "global-packages";
        private const string _tempResourceName = "temp";

        //[Option(typeof(NuGetCommand), "LocalsCommandClearDescription")]
        public bool Clear { get; set; }

        //[Option(typeof(NuGetCommand), "LocalsCommandListDescription")]
        public bool List { get; set; }

        private IList<string> Arguments { get; set; }

        private ISettings Settings { get; set; }

        public Task ExecuteCommand()
        {
            var localResourceName = GetLocalResourceName(Arguments[0]);

            if (Clear)
            {
                ClearLocalResource(localResourceName);
            }
            else if (List)
            {
                ListLocalResource(localResourceName);
            }

            return Task.FromResult(0);
        }

        private void ListLocalResource(LocalResourceName localResourceName)
        {
            switch (localResourceName)
            {
                case LocalResourceName.HttpCache:
                    PrintLocalResourcePath(_httpCacheResourceName, SettingsUtility.GetHttpCacheFolder());
                    break;
                case LocalResourceName.PackagesCache:
                    PrintLocalResourcePath(_packagesCacheResourceName, MachineCache.Default?.Source);
                    break;
                case LocalResourceName.GlobalPackagesFolder:
                    PrintLocalResourcePath(_globalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    break;
                case LocalResourceName.Temp:
                    PrintLocalResourcePath(_tempResourceName, NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp));
                    break;
                case LocalResourceName.All:
                    PrintLocalResourcePath(_httpCacheResourceName, SettingsUtility.GetHttpCacheFolder());
                    PrintLocalResourcePath(_packagesCacheResourceName, MachineCache.Default?.Source);
                    PrintLocalResourcePath(_globalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    PrintLocalResourcePath(_tempResourceName, NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp));
                    break;
                default:
                    // Invalid local resource name provided.
                    throw new Exception(Strings.LocalsCommand_InvalidLocalResourceName);
            }
        }

        private void PrintLocalResourcePath(string resourceName, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine(Strings.LocalsCommand_LocalResourcePathNotSet);
            }
            else
            {
                Console.WriteLine($"{resourceName}: {path}");
            }
        }

        private void ClearLocalResource(LocalResourceName localResourceName)
        {
            var success = true;

            switch (localResourceName)
            {
                case LocalResourceName.HttpCache:
                    success &= ClearNuGetHttpCache();
                    break;
                case LocalResourceName.PackagesCache:
                    success &= ClearNuGetPackagesCache();
                    break;
                case LocalResourceName.GlobalPackagesFolder:
                    success &= ClearNuGetGlobalPackagesFolder();
                    break;
                case LocalResourceName.Temp:
                    success &= ClearNuGetTempFolder();
                    break;
                case LocalResourceName.All:
                    success &= ClearNuGetHttpCache();
                    success &= ClearNuGetPackagesCache();
                    success &= ClearNuGetGlobalPackagesFolder();
                    success &= ClearNuGetTempFolder();
                    break;
                default:
                    // Invalid local resource name provided.
                    throw new Exception(Strings.LocalsCommand_InvalidLocalResourceName);
            }

            if (!success)
            {
                throw new Exception(Strings.LocalsCommand_ClearFailed);
            }
            else
            {
                Console.WriteLine(Strings.LocalsCommand_ClearedSuccessful);
            }
        }

        /// <summary>
        /// Clears the global NuGet packages cache.
        /// </summary>
        /// <returns><code>True</code> if the operation was successful; otherwise <code>false</code>.</returns>
        private bool ClearNuGetGlobalPackagesFolder()
        {
            var success = true;
            var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(Settings);

            Console.WriteLine(Strings.LocalsCommand_ClearingNuGetGlobalPackagesCache, globalPackagesFolderPath);

            success &= ClearCacheDirectory(globalPackagesFolderPath);
            return success;
        }

        /// <summary>
        /// Clear the NuGet machine cache.
        /// </summary>
        /// <returns><code>True</code> if the operation was successful; otherwise <code>false</code>.</returns>
        private bool ClearNuGetPackagesCache()
        {
            var success = true;
            if (!string.IsNullOrEmpty(MachineCache.Default?.Source))
            {
                Console.WriteLine(Strings.LocalsCommand_ClearingNuGetCache, MachineCache.Default.Source);

                success = ClearCacheDirectory(MachineCache.Default.Source);
            }
            return success;
        }

        /// <summary>
        /// Clears the NuGet v3 HTTP cache.
        /// </summary>
        /// <returns><code>True</code> if the operation was successful; otherwise <code>false</code>.</returns>
        private bool ClearNuGetHttpCache()
        {
            var success = true;
            var httpCacheFolderPath = SettingsUtility.GetHttpCacheFolder();

            if (!string.IsNullOrEmpty(httpCacheFolderPath))
            {
                Console.WriteLine(Strings.LocalsCommand_ClearingNuGetHttpCache,
                    httpCacheFolderPath);

                success &= ClearCacheDirectory(httpCacheFolderPath);
            }

            return success;
        }

        private bool ClearNuGetTempFolder()
        {
            var tempFolderPath = NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp);

            return ClearCacheDirectory(tempFolderPath);
        }

        private static LocalResourceName GetLocalResourceName(string localResourceName)
        {
            if (string.Equals(localResourceName, "all", StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.All;
            }
            else if (string.Equals(localResourceName, _httpCacheResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.HttpCache;
            }
            else if (string.Equals(localResourceName, _packagesCacheResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.PackagesCache;
            }
            else if (string.Equals(localResourceName, _globalPackagesResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.GlobalPackagesFolder;
            }
            else if (string.Equals(localResourceName, _tempResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.Temp;
            }
            else
            {
                return LocalResourceName.Unknown;
            }
        }

        private bool ClearCacheDirectory(string folderPath)
        {
            // In order to get detailed error messages, we need to do recursion ourselves.
            var failedDeletes = new List<string>();
            LocalResourceUtils.DeleteDirectoryTree(folderPath, failedDeletes);

            if (failedDeletes.Any())
            {
                Console.WriteLine(Strings.LocalsCommand_LocalsPartiallyCleared);

                foreach (var failedDelete in failedDeletes.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Strings.LocalsCommand_FailedToDeletePath, failedDelete);
                }

                return false;
            }
            else
            {
                return true;
            }
        }

        private enum LocalResourceName
        {
            Unknown,
            HttpCache,
            PackagesCache,
            GlobalPackagesFolder,
            Temp,
            All
        }
    }
}
