// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommand), "clearcache", "ClearCacheCommandDescription", MaxArgs = 0,
        UsageSummaryResourceName = "ClearCacheCommandSummary",
        UsageExampleResourceName = "ClearCacheCommandExamples")]
    public class ClearCacheCommand
        : Command
    {
        [Option(typeof(NuGetCommand), "ClearCacheCommandClearGlobalPackagesDescription")]
        public bool ClearGlobalPackages { get; set; }

        public override Task ExecuteCommandAsync()
        {
            var withoutErrors = true;

            // Clear the NuGet machine cache
            if (!string.IsNullOrEmpty(MachineCache.Default?.Source))
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetCache)),
                    MachineCache.Default.Source);

                withoutErrors = ClearCacheDirectory(MachineCache.Default.Source);
            }

            // Clear NuGet v3 HTTP cache
            var httpCacheFolderPath = SettingsUtility.GetHttpCacheFolder(Settings);
            if (!string.IsNullOrEmpty(httpCacheFolderPath))
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetHttpCache)),
                    httpCacheFolderPath);

                withoutErrors &= ClearCacheDirectory(httpCacheFolderPath);
            }

            if (ClearGlobalPackages)
            {
                // also clear the global NuGet packages cache
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(Settings);

                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetGlobalPackagesCache)),
                    globalPackagesFolderPath);

                withoutErrors &= ClearCacheDirectory(globalPackagesFolderPath);
            }

            if (!withoutErrors)
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheClearFailed)));
            }

            return Task.FromResult(0);
        }

        private bool ClearCacheDirectory(string folderPath)
        {
            var failedDeletes = new List<string>();
            DeleteDirectoryTree(folderPath, failedDeletes);

            if (failedDeletes.Any())
            {
                Console.WriteWarning(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CachePartiallyCleared)));

                foreach (var failedDelete in failedDeletes.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteWarning(
                        LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_FailedToDeletePath)),
                        failedDelete);
                }
                return false;
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheCleared)));
                return true;
            }
        }

        private static void DeleteDirectoryTree(string folderPath, List<string> failedDeletes)
        {
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            // When files or folders are readonly, the Directory.Delete method may not be able to delete it.
            // In addition, Directory.Delete(..., recursive: true) does NOT delete any files in the directory tree.
            DeleteFilesInDirectoryTree(folderPath, failedDeletes);

            try
            {
                SafeDeleteDirectoryTree(folderPath);
            }
            catch
            {
                // Report any other exception, or the above exceptions after a failed retry attempt. 
                // the Directory.Delete method may not be able to delete it.
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
                // If the recursive parameter is true, the user must have write permission 
                // for the current directory as well as for all subdirectories.
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
                Directory.Delete(folderPath, recursive: true);
            }
            catch (UnauthorizedAccessException)
            {
                // Try once more. 
                // This may just be caused by another process not timely releasing the file handle.
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
                    var attributes = File.GetAttributes(filePath);
                    if (attributes.HasFlag(FileAttributes.ReadOnly))
                    {
                        attributes &= ~FileAttributes.ReadOnly;
                        File.SetAttributes(filePath, attributes);
                    }
                    File.Delete(filePath);
                }
                catch
                {
                    failedDeletes.Add(filePath);
                }
            }
        }
    }
}