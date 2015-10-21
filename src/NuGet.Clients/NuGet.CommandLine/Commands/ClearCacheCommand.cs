// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
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
            var success = true;

            // Clear the NuGet machine cache
            if (!string.IsNullOrEmpty(MachineCache.Default?.Source))
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetCache)),
                    MachineCache.Default.Source);

                success = ClearCacheDirectory(MachineCache.Default.Source);
            }

            // Clear NuGet v3 HTTP cache
            var httpCacheFolderPath = SettingsUtility.GetHttpCacheFolder(Settings);
            if (!string.IsNullOrEmpty(httpCacheFolderPath))
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetHttpCache)),
                    httpCacheFolderPath);

                success &= ClearCacheDirectory(httpCacheFolderPath);
            }

            if (ClearGlobalPackages)
            {
                // also clear the global NuGet packages cache
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(Settings);

                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetGlobalPackagesCache)),
                    globalPackagesFolderPath);

                success &= ClearCacheDirectory(globalPackagesFolderPath);
            }

            if (!success)
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheClearFailed)));
            }
            else
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheCleared)));
            }

            return Task.FromResult(0);
        }

        private static bool ClearCacheDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                // Non-issue.
                return true;
            }

            try
            {
                // When files or (sub)folders are readonly, the Directory.Delete method may not be able to delete it.
                MakeFilesWritableInDirectoryTree(folderPath);

                // Deletes the specified directory and any subdirectories and files in the directory.
                // When deleting a directory that contains a reparse point, such as a symbolic link or a mount point:
                // * If the reparse point is a directory, such as a mount point, 
                //   it is unmounted and the mount point is deleted. 
                //   This method does not recurse through the reparse point. 
                // * If the reparse point is a symbolic link to a file, 
                //   the reparse point is deleted and not the target of the symbolic link.
                Directory.Delete(folderPath, recursive: true);

                return true;
            }
            catch (DirectoryNotFoundException)
            {
                // Should not happen, but it is a non-issue.
                return true;
            }
            catch
            {
                // Report failure.
                return false;
            }
        }
        
        private static void MakeFilesWritableInDirectoryTree(string folderPath)
        {
            // Using the default SearchOption.TopDirectoryOnly, as SearchOption.AllDirectories would also
            // include reparse points such as mounted drives and symbolic links in the search.
            foreach (var subFolderPath in Directory.EnumerateDirectories(folderPath))
            {
                var directoryInfo = new DirectoryInfo(subFolderPath);
                if (!directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    MakeFilesWritableInDirectoryTree(subFolderPath);
                }
            }

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var filePath = Path.Combine(folderPath, Path.GetFileName(file));
                var attributes = File.GetAttributes(filePath);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    attributes &= ~FileAttributes.ReadOnly;
                    File.SetAttributes(filePath, attributes);
                }
            }
        }
    }
}