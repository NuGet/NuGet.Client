// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Get all the settings to be used for project restore.
    /// </summary>
    public class GetRestoreSettingsTask : Task
    {

        public static string CLEAR = "CLEAR";

        [Required]
        public string ProjectUniqueName { get; set; }

        public string[] RestoreSources { get; set; }

        public string RestorePackagesPath { get; set; }

        public string[] RestoreFallbackFolders { get; set; }

        public string RestoreConfigFile { get; set; }

        public string RestoreSolutionDirectory { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public string[] OutputSources { get; set; }

        [Output]
        public string OutputPackagesPath { get; set; }

        [Output]
        public string[] OutputFallbackFolders { get; set; }

        [Output]
        public string[] OutputConfigFilePaths { get; set; }

        private static Lazy<IMachineWideSettings> _machineWideSettings = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        public ISettings GetSettings(string projectDirectory, string restoreConfigFile)
        {
            if (string.IsNullOrEmpty(restoreConfigFile))
            {
                return Settings.LoadDefaultSettings(projectDirectory,
                    configFileName: null,
                    machineWideSettings: _machineWideSettings.Value);
            }
            else
            {
                var configFileFullPath = Path.GetFullPath(restoreConfigFile);
                var configFileName = Path.GetFileName(configFileFullPath);
                var configDirPath = Path.GetDirectoryName(restoreConfigFile);
                return Settings.LoadSpecificSettings(configDirPath, configFileName: configFileName);
            }
        }

        private ISettings ReadSettings(string solutionDirectory, string restoreDirectory, string restoreConfigFile)
        {
            if (!string.IsNullOrEmpty(solutionDirectory))
            {
                // Read the solution-level settings
                var solutionSettingsFile = Path.Combine(
                    solutionDirectory,
                    NuGetConstants.NuGetSolutionSettingsFolder);

                if (restoreConfigFile != null)
                {
                    restoreConfigFile = Path.GetFullPath(restoreConfigFile);
                }

                return Configuration.Settings.LoadDefaultSettings(
                    solutionSettingsFile,
                    configFileName: restoreConfigFile,
                    machineWideSettings: _machineWideSettings.Value);
            }

            if (string.IsNullOrEmpty(restoreConfigFile))
            {
                return Configuration.Settings.LoadDefaultSettings(
                    restoreDirectory,
                    configFileName: null,
                    machineWideSettings: _machineWideSettings.Value);
            }
            else
            {
                var configFileFullPath = Path.GetFullPath(restoreConfigFile);
                var directory = Path.GetDirectoryName(configFileFullPath);
                var configFileName = Path.GetFileName(configFileFullPath);
                return Configuration.Settings.LoadDefaultSettings(
                    directory,
                    configFileName,
                    null);
            }
        }

        public override bool Execute()
        {
            // log inputs
            var log = new MSBuildLogger(Log);
            log.LogDebug($"(in) ProjectUniqueName '{ProjectUniqueName}'");
            if (RestoreSources != null)
            {
                log.LogDebug($"(in) RestoreSources '{string.Join(";", RestoreSources.Select(p => p))}'");
            }
            if (RestorePackagesPath != null)
            {
                log.LogDebug($"(in) RestorePackagesPath '{RestorePackagesPath}'");
            }
            if (RestoreFallbackFolders != null)
            {
                log.LogDebug($"(in) RestoreFallbackFolders '{string.Join(";", RestoreFallbackFolders.Select(p => p))}'");
            }
            if (RestoreConfigFile != null)
            {
                log.LogDebug($"(in) RestoreConfigFile '{RestoreConfigFile}'");
            }

            if (RestoreSolutionDirectory != null)
            {
                log.LogDebug($"(in) RestoreSolutionDirectory '{RestoreSolutionDirectory}'");
            }

            var settings = ReadSettings(RestoreSolutionDirectory, Path.GetDirectoryName(ProjectUniqueName), RestoreConfigFile);

            if (string.IsNullOrEmpty(RestorePackagesPath))
            {
                OutputPackagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            }
            else if (StringComparer.OrdinalIgnoreCase.Compare(RestorePackagesPath, CLEAR) == 0)
            {
                RestorePackagesPath = string.Empty;
            }
            else
            {
                // Relative -> Absolute path
                OutputPackagesPath = UriUtility.GetAbsolutePathFromFile(ProjectUniqueName, RestorePackagesPath);
            }

            if (RestoreSources == null)
            {
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourcesFromProvider = packageSourceProvider.LoadPackageSources();
                OutputSources = packageSourcesFromProvider.Select(e => e.Source).ToArray();
            }
            else if (RestoreSources.Contains(CLEAR, StringComparer.OrdinalIgnoreCase))
            {
                if (RestoreSources.Length == 1)
                {
                    OutputSources = new string[] { };
                }
                else
                {
                    throw new InvalidOperationException($"{CLEAR} cannot be used in conjunction with other values.");
                }
            }
            else
            {
                // Relative -> Absolute paths
                OutputSources = RestoreSources.Select(e => UriUtility.GetAbsolutePathFromFile(ProjectUniqueName, e)).ToArray();
            }

            if (RestoreFallbackFolders == null)
            {
                OutputFallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings).ToArray();
            }
            else if (RestoreFallbackFolders.Contains(CLEAR, StringComparer.OrdinalIgnoreCase))
            {
                if (RestoreFallbackFolders.Length == 1)
                {
                    OutputFallbackFolders = new string[] { };
                }
                else
                {
                    throw new InvalidOperationException($"{CLEAR} cannot be used in conjunction with other values.");
                }

            }
            else
            {
                // Relative -> Absolute paths
                OutputFallbackFolders = RestoreFallbackFolders.Select(e => UriUtility.GetAbsolutePathFromFile(ProjectUniqueName, e)).ToArray();
            }

            var configFilePaths = new List<string>();
            foreach (var config in settings.Priority)
            {
                configFilePaths.Add(Path.GetFullPath(Path.Combine(config.Root, config.FileName)));
            }
            OutputConfigFilePaths = configFilePaths.ToArray();


            log.LogDebug($"(out) OutputPackagesPath '{OutputPackagesPath}'");
            log.LogDebug($"(out) OutputSources '{string.Join(";", OutputSources.Select(p => p))}'");
            log.LogDebug($"(out) OutputFallbackFolders '{string.Join(";", OutputFallbackFolders.Select(p => p))}'");
            log.LogDebug($"(out) OutputConfigFilePaths '{string.Join(";", OutputConfigFilePaths.Select(p => p))}'");

            return true;
        }
    }
}