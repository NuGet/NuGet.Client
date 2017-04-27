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
            System.Diagnostics.Debugger.Launch();

            var settings = GetSettings(Path.GetDirectoryName(ProjectUniqueName));

            // Log inputs
            if (string.IsNullOrEmpty(RestorePackagesPath))
            {
                OutputPackagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            } else
            {
                OutputPackagesPath = RestorePackagesPath;
            }

            if (RestoreSources == null)
            {
                var packageSourceProvider = new PackageSourceProvider(settings);
                var packageSourcesFromProvider = packageSourceProvider.LoadPackageSources();
                OutputSources = packageSourcesFromProvider.Select(e => e.Source).ToArray();
            } else
            {
                OutputSources = RestoreSources;
            }

            if(RestoreFallbackFolders == null)
            {
                OutputFallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings).ToArray();
            } else
            {
                OutputFallbackFolders = RestoreFallbackFolders;
            }


            return true;
        }
    }
}