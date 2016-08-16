// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGet.Commands
{
    public class LocalsCommandRunner
    {
        private enum LocalResourceName
        {
            Unknown,
            HttpCache,
            GlobalPackagesFolder,
            Temp,
            All
        }
        /// <summary>
        /// Enum used to indicate the result of the command to callers.
        /// </summary>
        public enum LocalsCommandResult
        {
            InvalidLocalResourceName,
            ClearFailure,
            ClearSuccess
        }
        private const string _httpCacheResourceName = "http-cache";
        private const string _globalPackagesResourceName = "global-packages";
        private const string _allResourceName = "all";
        private const string _tempResourceName = "temp";

        public LocalsCommandResult Result { get; private set; }

        public bool Clear { get; set; }

        public bool List { get; set; }

        private IList<string> Arguments { get; set; }

        private ISettings Settings { get; set; }

        public LocalsCommandRunner(IList<string> arguments, ISettings settings, bool clear, bool list)
        {
            this.Arguments = arguments;
            this.Settings = settings;
            this.Clear = clear;
            this.List = list;
        }
        /// <summary>
        /// Executes the logic for nuget locals command.
        /// </summary>
        /// <returns></returns>
        public void ExecuteCommand()
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

            return;
        }

        private void ListLocalResource(LocalResourceName localResourceName)
        {
            switch (localResourceName)
            {
                case LocalResourceName.HttpCache:
                    PrintLocalResourcePath(_httpCacheResourceName, SettingsUtility.GetHttpCacheFolder());
                    break;
                case LocalResourceName.GlobalPackagesFolder:
                    PrintLocalResourcePath(_globalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    break;
                case LocalResourceName.Temp:
                    PrintLocalResourcePath(_tempResourceName, NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp));
                    break;
                case LocalResourceName.All:
                    PrintLocalResourcePath(_httpCacheResourceName, SettingsUtility.GetHttpCacheFolder());
                    PrintLocalResourcePath(_globalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    PrintLocalResourcePath(_tempResourceName, NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp));
                    break;
                default:
                    // Invalid local resource name provided.
                    Result = LocalsCommandResult.InvalidLocalResourceName;
                    throw new ArgumentException(String.Format(CultureInfo.CurrentCulture,Strings.LocalsCommand_InvalidLocalResourceName));
            }
        }

        private void PrintLocalResourcePath(string resourceName, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_LocalResourcePathNotSet));
            }
            else
            {
                Console.WriteLine(String.Format(CultureInfo.CurrentCulture, $"{resourceName}: {path}"));
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
                case LocalResourceName.GlobalPackagesFolder:
                    success &= ClearNuGetGlobalPackagesFolder();
                    break;
                case LocalResourceName.Temp:
                    success &= ClearNuGetTempFolder();
                    break;
                case LocalResourceName.All:
                    success &= ClearNuGetHttpCache();
                    success &= ClearNuGetGlobalPackagesFolder();
                    success &= ClearNuGetTempFolder();
                    break;
                default:
                    // Invalid local resource name provided.
                    Result = LocalsCommandResult.InvalidLocalResourceName;
                    throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_InvalidLocalResourceName));
            }

            if (!success)
            {
                Result = LocalsCommandResult.ClearFailure;
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearFailed));
            }
            else
            {
                Result = LocalsCommandResult.ClearSuccess;
                Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearedSuccessful));
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

            Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearingNuGetGlobalPackagesCache, globalPackagesFolderPath));

            success &= ClearCacheDirectory(globalPackagesFolderPath);
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
                Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearingNuGetHttpCache,
                    httpCacheFolderPath));

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
            if (string.Equals(localResourceName, _allResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.All;
            }
            else if (string.Equals(localResourceName, _httpCacheResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.HttpCache;
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
                Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_LocalsPartiallyCleared));

                foreach (var failedDelete in failedDeletes.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine(String.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_FailedToDeletePath, failedDelete));
                }

                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
