// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.CommandLine.Commands
{
    [Command(typeof(NuGetCommand), "clearcache", "ClearCacheCommandDescription", MaxArgs = 0,
            UsageSummaryResourceName = "ClearCacheCommandSummary", UsageExampleResourceName = "ClearCacheCommandExamples")]
    public class ClearCacheCommand
        : Command
    {
        [Option(typeof(NuGetCommand), "ClearCacheCommandClearGlobalPackagesDescription")]
        public bool ClearGlobalPackages { get; set; }

        public override Task ExecuteCommandAsync()
        {
            // Clear the NuGet machine cache
            if (!string.IsNullOrEmpty(MachineCache.Default?.Source))
            {
                Console.WriteLine($"{LocalizedResourceManager.GetString("ClearCacheCommand_ClearingNuGetCache")}: {MachineCache.Default.Source}");
                ClearCacheDirectory(MachineCache.Default.Source);
            }

            // Clear NuGet v3 HTTP cache
            var localAppDataFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var httpCacheFolderPath = Path.Combine(localAppDataFolderPath, "NuGet", "v3-cache");
            if (!string.IsNullOrEmpty(httpCacheFolderPath))
            {
                Console.WriteLine($"{LocalizedResourceManager.GetString("ClearCacheCommand_ClearingNuGetHttpCache")}: {httpCacheFolderPath}");
                ClearCacheDirectory(httpCacheFolderPath);
            }
            
            if (ClearGlobalPackages)
            {
                // also clear the global NuGet packages cache
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(Settings);
                Console.WriteLine($"{LocalizedResourceManager.GetString("ClearCacheCommand_ClearingNuGetGlobalPackagesCache")}: {globalPackagesFolderPath}");
                ClearCacheDirectory(globalPackagesFolderPath);
            }

            return Task.FromResult(0);
        }

        private void ClearCacheDirectory(string folderPath)
        {
            try
            {
                // Calling DeleteRecursive rather than Directory.Delete(..., recursive: true)
                // due to an infrequent exception which can be thrown from that API
                DeleteRecursive(folderPath);

                Console.WriteLine(LocalizedResourceManager.GetString("ClearCacheCommand_CacheCleared"));
            }
            catch (Exception e)
            {
                Console.WriteError($"{LocalizedResourceManager.GetString("ClearCacheCommand_UnableToClearCacheDir")}: {e.Message}");
            }
        }

        private static void DeleteRecursive(string deletePath)
        {
            if (!Directory.Exists(deletePath))
            {
                return;
            }

            foreach (var deleteFilePath in Directory.EnumerateFiles(deletePath).Select(Path.GetFileName))
            {
                File.Delete(Path.Combine(deletePath, deleteFilePath));
            }

            foreach (var deleteFolderPath in Directory.EnumerateDirectories(deletePath).Select(Path.GetFileName))
            {
                DeleteRecursive(Path.Combine(deletePath, deleteFolderPath));
                Directory.Delete(Path.Combine(deletePath, deleteFolderPath), recursive: true);
            }
        }
    }
}