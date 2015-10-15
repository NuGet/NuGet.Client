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
                throw new CommandLineException(LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheClearFailed)));
            }

            return Task.FromResult(0);
        }

        private bool ClearCacheDirectory(string folderPath)
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
                return false;
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString(nameof(NuGetResources.ClearCacheCommand_CacheCleared)));
                return true;
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
                    EnsureWritable(fullFilePath);
                    File.Delete(fullFilePath);
                }
                catch
                {
                    failedDeletes.Add(fullFilePath);
                }
            }

            foreach (var deleteFolder in Directory.EnumerateDirectories(deletePath))
            {
                var deleteFolderPath = Path.GetFileName(deleteFolder);
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

        public static void EnsureWritable(string filePath)
        {
            var attributes = File.GetAttributes(filePath);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                attributes &= ~FileAttributes.ReadOnly;
                File.SetAttributes(filePath, attributes);
            }
        }
    }
}