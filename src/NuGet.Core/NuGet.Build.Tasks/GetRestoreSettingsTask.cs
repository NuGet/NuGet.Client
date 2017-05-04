// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Configuration;

namespace NuGet.Build.Tasks
{
    public class GetRestoreSettingsTask : Task
    {
        [Required]
        public string ProjectUniqueName { get; set; }

        public string[] RestoreSources { get; set; }

        public string RestorePackagesPath { get; set; }

        public string[] RestoreFallbackFolders { get; set; }

        public string RestoreConfigFile { get; set; }


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
        public string[] ConfigFilePaths { get; set; }

        private static Lazy<IMachineWideSettings> _machineWideSettings = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        public ISettings GetSettings(string projectDirectory)
        {
            if (string.IsNullOrEmpty(RestoreConfigFile))
            {
                return Settings.LoadDefaultSettings(projectDirectory,
                    configFileName: null,
                    machineWideSettings: _machineWideSettings.Value);
            }
            else
            {
                var configFileFullPath = Path.GetFullPath(RestoreConfigFile);
                var directory = Path.GetDirectoryName(configFileFullPath);
                var configFileName = Path.GetFileName(configFileFullPath);
                return Settings.LoadSpecificSettings(projectDirectory,
                    configFileName: configFileName);
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
            var settings = GetSettings(Path.GetDirectoryName(ProjectUniqueName));

            if (string.IsNullOrEmpty(RestorePackagesPath))
            {
                OutputPackagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            }
            else
            {
                OutputPackagesPath = RestorePackagesPath;
            }

            if (RestoreSources == null)
            {
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourcesFromProvider = packageSourceProvider.LoadPackageSources();
                OutputSources = packageSourcesFromProvider.Select(e => e.Source).ToArray();
            }
            else
            {
                OutputSources = RestoreSources;
            }

            if (RestoreFallbackFolders == null)
            {
                OutputFallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings).ToArray();
            }
            else
            {
                OutputFallbackFolders = RestoreFallbackFolders;
            }

            var configFilePaths = new List<string>();
            foreach (var config in settings.Priority)
            {
                configFilePaths.Add(config.Root + config.FileName);
            }
            ConfigFilePaths = configFilePaths.ToArray();

            log.LogDebug($"(out) OutputPackagesPath '{OutputPackagesPath}'");
            log.LogDebug($"(out) OutputSources '{string.Join(";", OutputSources.Select(p => p))}'");
            log.LogDebug($"(out) OutputFallbackFolders '{string.Join(";", OutputFallbackFolders.Select(p => p))}'");
            log.LogDebug($"(out) ConfigFilePaths '{string.Join(";", ConfigFilePaths.Select(p => p))}'");

            return true;
        }
    }
}