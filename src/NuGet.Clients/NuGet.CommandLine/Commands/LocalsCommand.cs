// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommand), "locals", "LocalsCommandDescription", MinArgs = 0, MaxArgs = 1,
        UsageSummaryResourceName = "LocalsCommandSummary",
        UsageExampleResourceName = "LocalsCommandExamples")]
    public class LocalsCommand
        : Command
    {
        private const string _httpCacheResourceName = "http-cache";
        private const string _packagesCacheResourceName = "packages-cache";
        private const string _globalPackagesResourceName = "global-packages";

        [Option(typeof(NuGetCommand), "LocalsCommandClearDescription")]
        public bool Clear { get; set; }

        [Option(typeof(NuGetCommand), "LocalsCommandListDescription")]
        public bool List { get; set; }

        public override Task ExecuteCommandAsync()
        {
            if ((!Arguments.Any() || string.IsNullOrWhiteSpace(Arguments[0]))
                || (!Clear && !List)
                || (Clear && List))
            {
                // Using both -clear and -list command options, or neither one of them, is not supported.
                // We use MinArgs = 0 even though the first argument is required,
                // to avoid throwing a command argument validation exception and
                // immediately show usage help for this command instead.
                HelpCommand.ViewHelpForCommand(CommandAttribute.CommandName);

                return Task.FromResult(0);
            }

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
                    PrintLocalResourcePath(_httpCacheResourceName, SettingsUtility.GetHttpCacheFolder(Settings));
                    break;
                case LocalResourceName.PackagesCache:
                    PrintLocalResourcePath(_packagesCacheResourceName, MachineCache.Default?.Source);
                    break;
                case LocalResourceName.GlobalPackagesFolder:
                    PrintLocalResourcePath(_globalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    break;
                case LocalResourceName.All:
                    PrintLocalResourcePath(_httpCacheResourceName, SettingsUtility.GetHttpCacheFolder(Settings));
                    PrintLocalResourcePath(_packagesCacheResourceName, MachineCache.Default?.Source);
                    PrintLocalResourcePath(_globalPackagesResourceName, SettingsUtility.GetGlobalPackagesFolder(Settings));
                    break;
                default:
                    // Invalid local resource name provided.
                    throw new CommandLineException(
                        LocalizedResourceManager.GetString(
                            nameof(NuGetResources.LocalsCommand_InvalidLocalResourceName)));
            }
        }

        private void PrintLocalResourcePath(string resourceName, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteWarning(
                    LocalizedResourceManager.GetString(
                        nameof(NuGetResources.LocalsCommand_LocalResourcePathNotSet)),
                    resourceName);
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
                case LocalResourceName.All:
                    success &= ClearNuGetHttpCache();
                    success &= ClearNuGetPackagesCache();
                    success &= ClearNuGetGlobalPackagesFolder();
                    break;
                default:
                    // Invalid local resource name provided.
                    throw new CommandLineException(
                        LocalizedResourceManager.GetString(
                            nameof(NuGetResources.LocalsCommand_InvalidLocalResourceName)));
            }

            if (!success)
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.LocalsCommand_ClearFailed)));
            }
            else
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.LocalsCommand_ClearedSuccessful)));
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

            Console.WriteLine(
                LocalizedResourceManager.GetString(nameof(NuGetResources.LocalsCommand_ClearingNuGetGlobalPackagesCache)),
                globalPackagesFolderPath);

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
                Console.WriteLine(LocalizedResourceManager.GetString(
                    nameof(NuGetResources.LocalsCommand_ClearingNuGetCache)), MachineCache.Default.Source);

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
            var httpCacheFolderPath = SettingsUtility.GetHttpCacheFolder(Settings);

            if (!string.IsNullOrEmpty(httpCacheFolderPath))
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.LocalsCommand_ClearingNuGetHttpCache)),
                    httpCacheFolderPath);

                success &= ClearCacheDirectory(httpCacheFolderPath);
            }

            return success;
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
            else
            {
                return LocalResourceName.Unknown;
            }
        }

        private bool ClearCacheDirectory(string folderPath)
        {
            // In order to get detailed error messages, we need to do recursion ourselves.
            var failedDeletes = new List<string>();
            DeleteDirectoryTree(folderPath, failedDeletes);

            if (failedDeletes.Any())
            {
                Console.WriteWarning(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.LocalsCommand_LocalsPartiallyCleared)));

                foreach (var failedDelete in failedDeletes.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteWarning(
                        LocalizedResourceManager.GetString(nameof(NuGetResources.LocalsCommand_FailedToDeletePath)),
                        failedDelete);
                }

                return false;
            }
            else
            {
                return true;
            }
        }

        private static void DeleteDirectoryTree(string folderPath, List<string> failedDeletes)
        {
            if (!Directory.Exists(folderPath))
            {
                // Non-issue.
                return;
            }

            DeleteFilesInDirectoryTree(folderPath, failedDeletes);

            try
            {
                SafeDeleteDirectoryTree(folderPath);
            }
            catch (PathTooLongException)
            {
                failedDeletes.Add(folderPath);
            }
            catch (UnauthorizedAccessException)
            {
                failedDeletes.Add(folderPath);
            }
        }

        private static void SafeDeleteDirectoryTree(string folderPath)
        {
            try
            {
                // Deletes the specified directory and any subdirectories and files in the directory.
                // When deleting a directory that contains a reparse point, such as a symbolic link or a mount point:
                // * If the reparse point is a directory, such as a mount point,
                //   it is unmounted and the mount point is deleted.
                //   This method does not recurse through the reparse point.
                // * If the reparse point is a symbolic link to a file,
                //   the reparse point is deleted and not the target of the symbolic link.
                Directory.Delete(folderPath, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // Should not happen, but it is a non-issue.
            }
            catch (IOException)
            {
                // Try once more.
                // The directory may be in use by another process and cause an IOException.
                Thread.Sleep(500);
                Directory.Delete(folderPath, recursive: true);
            }
        }

        private static void DeleteFilesInDirectoryTree(string folderPath, List<string> failedDeletes)
        {
            // Using the default SearchOption.TopDirectoryOnly, as SearchOption.AllDirectories would also
            // include reparse points such as mounted drives and symbolic links in the search.
            foreach (var subFolderPath in Directory.EnumerateDirectories(folderPath))
            {
                var directoryInfo = new DirectoryInfo(subFolderPath);
                if (!directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    DeleteFilesInDirectoryTree(subFolderPath, failedDeletes);
                }
            }

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var filePath = Path.Combine(folderPath, Path.GetFileName(file));
                try
                {
                    // When files or folders are readonly, the File.Delete method may not be able to delete it.
                    var attributes = File.GetAttributes(filePath);
                    if (attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        // Remove the readonly flag when set.
                        attributes &= ~FileAttributes.ReadOnly;
                        File.SetAttributes(filePath, attributes);
                    }

                    File.Delete(filePath);
                }
                catch (PathTooLongException)
                {
                    failedDeletes.Add(filePath);
                }
                catch (UnauthorizedAccessException)
                {
                    failedDeletes.Add(filePath);
                }
                catch (IOException)
                {
                    // The file is being used by another process.
                    failedDeletes.Add(filePath);
                }
            }
        }

        private enum LocalResourceName
        {
            Unknown,
            HttpCache,
            PackagesCache,
            GlobalPackagesFolder,
            All
        }
    }
}