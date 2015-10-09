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
            int commandResult = 0;

            // Clear the NuGet machine cache
            if (!string.IsNullOrEmpty(MachineCache.Default?.Source))
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetCache)),
                    MachineCache.Default.Source);

                commandResult = ClearCacheDirectory(MachineCache.Default.Source);
            }

            // Clear NuGet v3 HTTP cache
            var localAppDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var httpCacheFolderPath = Path.Combine(localAppDataFolderPath, "NuGet", "v3-cache");
            if (!string.IsNullOrEmpty(httpCacheFolderPath))
            {
                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetHttpCache)),
                    httpCacheFolderPath);

                var result = ClearCacheDirectory(httpCacheFolderPath);
                if (commandResult == 0)
                {
                    commandResult = result;
                }
            }

            if (ClearGlobalPackages)
            {
                // also clear the global NuGet packages cache
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(Settings);

                Console.WriteLine(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_ClearingNuGetGlobalPackagesCache)),
                    globalPackagesFolderPath);

                var result = ClearCacheDirectory(globalPackagesFolderPath);
                if (commandResult == 0)
                {
                    commandResult = result;
                }
            }

            if (commandResult != 0)
            {
                throw new Exception(LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheClearFailed)));
            }

            return Task.FromResult(commandResult);
        }

        private int ClearCacheDirectory(string folderPath)
        {
            // Calling DeleteRecursive rather than Directory.Delete(..., recursive: true)
            // due to an infrequent exception which can be thrown from that API
            var failedDeletes = new List<string>();
            DeleteRecursive(folderPath, failedDeletes);

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
                return 1;
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheCleared)));
                return 0;
            }
        }

        private static void DeleteRecursive(string deletePath, List<string> failedDeletes)
        {
            if (!Directory.Exists(deletePath))
            {
                return;
            }

            foreach (var deleteFile in Directory.EnumerateFiles(deletePath))
            {
                var deleteFilePath = Path.GetFileName(deleteFile);
                var fullFilePath = Path.Combine(deletePath, deleteFilePath);
                try
                {
                    File.Delete(fullFilePath);
                }
                catch
                {
                    failedDeletes.Add(fullFilePath);
                }
            }

            foreach (var deleteFolderPath in Directory.EnumerateDirectories(deletePath).Select(Path.GetFileName))
            {
                var fullDirectoryPath = Path.Combine(deletePath, deleteFolderPath);
                DeleteRecursive(fullDirectoryPath, failedDeletes);

                try
                {
                    Directory.Delete(fullDirectoryPath, recursive: true);
                }
                catch
                {
                    failedDeletes.Add(fullDirectoryPath);
                }
            }
        }
    }
}