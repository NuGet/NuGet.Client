// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget locals command
    /// </summary>
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

        public delegate void Log(string message);

        private const string HttpCacheResourceName = "http-cache";
        private const string GlobalPackagesResourceName = "global-packages";
        private const string AllResourceName = "all";
        private const string TempResourceName = "temp";

        public LocalsCommandResult Result { get; private set; }

        public bool Clear { get; set; }

        public bool List { get; set; }

        private IList<string> Arguments { get; set; }

        private ISettings Settings { get; set; }

        private ILogger Logger { get; set; }

        private Log LogError { get; set; }

        private Log LogInformation { get; set; }

        public LocalsCommandRunner(IList<string> arguments, ISettings settings, Log logInformation, Log logError, bool clear, bool list)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }
            else if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            else if (logInformation == null)
            {
                throw new ArgumentNullException(nameof(logInformation));
            }
            else if (logError == null)
            {
                throw new ArgumentNullException(nameof(logError));
            }

            Arguments = arguments;
            Settings = settings;
            Clear = clear;
            List = list;
            LogError = logError;
            LogInformation = logInformation;
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

        /// <summary>
        /// Lists out the cache location(s) path.
        /// </summary>
        /// <param name="localResourceName">Cache resource to be listed</param>
        /// <throws>Thorws <code>ArgumentException</code> if the specified resource name does not match a known cache type.</throws>
        private void ListLocalResource(LocalResourceName localResourceName)
        {
            switch (localResourceName)
            {
                case LocalResourceName.HttpCache:
                    PrintLocalResourcePath(HttpCacheResourceName, SettingsUtility.GetHttpCacheFolder());
                    break;

                case LocalResourceName.GlobalPackagesFolder:
                    PrintLocalResourcePath(GlobalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    break;

                case LocalResourceName.Temp:
                    PrintLocalResourcePath(TempResourceName, NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp));
                    break;

                case LocalResourceName.All:
                    PrintLocalResourcePath(HttpCacheResourceName, SettingsUtility.GetHttpCacheFolder());
                    PrintLocalResourcePath(GlobalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    PrintLocalResourcePath(TempResourceName, NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp));
                    break;

                default:
                    // Invalid local resource name provided.
                    Result = LocalsCommandResult.InvalidLocalResourceName;
                    LogError(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_InvalidLocalResourceName));
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_InvalidLocalResourceName));
            }
        }

        /// <summary>
        /// Prints the specified local resource path.
        /// </summary>
        /// <param name="resourceName"> Specified resource name</param>
        /// <param name="path"> Path for the specified resource</param>
        private void PrintLocalResourcePath(string resourceName, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                LogError(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_LocalResourcePathNotSet));
            }
            else
            {
                LogInformation(string.Format(CultureInfo.CurrentCulture, $"{resourceName}: {path}"));
            }
        }

        /// <summary>
        /// Clears the specified cache location(s).
        /// </summary>
        /// <param name="localResourceName"></param>
        /// <throws>Thorws <code>ArgumentException</code> if the specified resource name does not match a known cache type.</throws>
        /// <returns><code>True</code> if the operation was successful; otherwise <code>false</code>.</returns>
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
                    LogError(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_InvalidLocalResourceName));
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_InvalidLocalResourceName));
            }

            if (!success)
            {
                Result = LocalsCommandResult.ClearFailure;
                LogError(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearFailed));
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearFailed));
            }
            else
            {
                Result = LocalsCommandResult.ClearSuccess;
                LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearedSuccessful));
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

            LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearingNuGetGlobalPackagesCache, globalPackagesFolderPath));

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
                LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_ClearingNuGetHttpCache,
                    httpCacheFolderPath));

                success &= ClearCacheDirectory(httpCacheFolderPath);
            }

            return success;
        }

        /// <summary>
        /// Clears the temp folder cache.
        /// </summary>
        /// <returns><code>True</code> if the operation was successful; otherwise <code>false</code>.</returns>
        private bool ClearNuGetTempFolder()
        {
            var tempFolderPath = NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp);

            return ClearCacheDirectory(tempFolderPath);
        }

        /// <summary>
        /// Identifies the specified resource name to be cleared.
        /// </summary>
        /// <param name="localResourceName">specified resource name</param>
        /// <returns>Returns <code>LocalResourceName</code> indicating the local resource name specified.</returns>
        private static LocalResourceName GetLocalResourceName(string localResourceName)
        {
            if (string.Equals(localResourceName, AllResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.All;
            }
            else if (string.Equals(localResourceName, HttpCacheResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.HttpCache;
            }
            else if (string.Equals(localResourceName, GlobalPackagesResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.GlobalPackagesFolder;
            }
            else if (string.Equals(localResourceName, TempResourceName, StringComparison.OrdinalIgnoreCase))
            {
                return LocalResourceName.Temp;
            }
            else
            {
                return LocalResourceName.Unknown;
            }
        }

        /// <summary>
        /// Recursively deletes the specified directory tree.
        /// </summary>
        /// <param name="folderPath">Specified directory to be deleted</param>
        /// <returns><code>True</code> if the operation was successful; otherwise <code>false</code>.</returns>
        private bool ClearCacheDirectory(string folderPath)
        {
            // In order to get detailed error messages, we need to do recursion ourselves.
            var failedDeletes = new List<string>();
            LocalResourceUtils.DeleteDirectoryTree(folderPath, failedDeletes);

            if (failedDeletes.Any())
            {
                LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_LocalsPartiallyCleared));

                foreach (var failedDelete in failedDeletes.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    LogError(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_FailedToDeletePath, failedDelete));
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