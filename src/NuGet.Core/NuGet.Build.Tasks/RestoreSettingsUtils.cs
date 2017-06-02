// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// This class contains the logic for the settings using during restore. 
    /// It contains methods with the settings precedence logic as well.
    /// </summary>
    public class RestoreSettingsUtils
    {
        public static readonly string Clear = nameof(Clear);

        public static ISettings ReadSettings(string solutionDirectory, string restoreDirectory, string restoreConfigFile, Lazy<IMachineWideSettings> machineWideSettings) 
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
                    machineWideSettings: machineWideSettings.Value);
            }

            if (string.IsNullOrEmpty(restoreConfigFile))
            {
                return Configuration.Settings.LoadDefaultSettings(
                    restoreDirectory,
                    configFileName: null,
                    machineWideSettings: machineWideSettings.Value);
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

        public static IList<string> GetFallbackFolders(string projectFullPath, ISettings settings, IEnumerable<string> fallbackFolders)
        {
            if (ShouldReadFromSettings(fallbackFolders))
            {
                fallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings);
            }
            else
            {
                fallbackFolders = HandleClear(fallbackFolders);
            }

            return fallbackFolders.Select(e => UriUtility.GetAbsolutePathFromFile(projectFullPath, e)).ToList();
        }

        public static IList<PackageSource> GetSources(string projectFullPath, ISettings settings, IEnumerable<string> sources)
        {

            if (ShouldReadFromSettings(sources))
            {
                sources = SettingsUtility.GetEnabledSources(settings).Select(e => e.Source);
            }
            else
            {
                sources = HandleClear(sources);
            }

            return sources.Select(e => new PackageSource(UriUtility.GetAbsolutePathFromFile(projectFullPath, e))).ToList();
        }

        public static string GetPackagesPath(string projectFullPath, ISettings settings, string packagePath)
        {
            if (string.IsNullOrEmpty(packagePath))
            {
                return SettingsUtility.GetGlobalPackagesFolder(settings);
            }

            return UriUtility.GetAbsolutePathFromFile(projectFullPath, packagePath);
        }


        private static bool ShouldReadFromSettings(IEnumerable<string> values)
        {
            return !values.Any() && values.All(e => !StringComparer.OrdinalIgnoreCase.Equals(Clear, e));
        }

        private static IEnumerable<string> HandleClear(IEnumerable<string> values)
        {
            if (values.Any(e => StringComparer.OrdinalIgnoreCase.Equals(Clear, e)))
            {
                return Enumerable.Empty<string>();
            }
            return values;
        }

    }
}
