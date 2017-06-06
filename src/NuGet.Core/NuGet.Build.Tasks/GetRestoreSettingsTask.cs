// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Get all the settings to be used for project restore.
    /// </summary>
    public class GetRestoreSettingsTask : Task
    {
        [Required]
        public string ProjectUniqueName { get; set; }

        public string[] RestoreSources { get; set; }

        public string RestorePackagesPath { get; set; }

        public string[] RestoreFallbackFolders { get; set; }

        public string RestoreConfigFile { get; set; }

        public string RestoreSolutionDirectory { get; set; }

        public string[] RestoreAdditionalProjectSources { get; set; }

        public string[] RestoreAdditionalProjectFallbackFolders { get; set; }

        /// <summary>
        /// Command line value of RestorePackagesPath
        /// </summary>
        public string RestorePackagesPathOverride { get; set; }

        /// <summary>
        /// Command line value of RestoreSources
        /// </summary>
        public string[] RestoreSourcesOverride { get; set; }

        /// <summary>
        /// Command line value of RestoreFallbackFolders
        /// </summary>
        public string[] RestoreFallbackFoldersOverride { get; set; }

        /// <summary>
        /// Original working directory
        /// </summary>
        [Required]
        public string MSBuildStartupDirectory { get; set; }

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

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);

            // Log Inputs
            BuildTasksUtility.LogInputParam(log, nameof(ProjectUniqueName), ProjectUniqueName);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreSources), RestoreSources);
            BuildTasksUtility.LogInputParam(log, nameof(RestorePackagesPath), RestorePackagesPath);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreFallbackFolders), RestoreFallbackFolders);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreConfigFile), RestoreConfigFile);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreSolutionDirectory), RestoreSolutionDirectory);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreAdditionalProjectSources), RestoreAdditionalProjectSources);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreAdditionalProjectFallbackFolders), RestoreAdditionalProjectFallbackFolders);
            BuildTasksUtility.LogInputParam(log, nameof(RestorePackagesPathOverride), RestorePackagesPathOverride);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreSourcesOverride), RestoreSourcesOverride);
            BuildTasksUtility.LogInputParam(log, nameof(RestoreFallbackFoldersOverride), RestoreFallbackFoldersOverride);

            try
            {
                // Validate inputs
                if (RestoreSourcesOverride == null
                    && MSBuildRestoreUtility.LogErrorForClearIfInvalid(RestoreSources, ProjectUniqueName, log))
                {
                    // Fail due to invalid source combination
                    return false;
                }

                if (RestoreFallbackFoldersOverride == null
                    && MSBuildRestoreUtility.LogErrorForClearIfInvalid(RestoreFallbackFolders, ProjectUniqueName, log))
                {
                    // Fail due to invalid fallback combination
                    return false;
                }

                // Settings
                var settings = RestoreSettingsUtils.ReadSettings(RestoreSolutionDirectory, Path.GetDirectoryName(ProjectUniqueName), RestoreConfigFile, _machineWideSettings);
                OutputConfigFilePaths = SettingsUtility.GetConfigFilePaths(settings).ToArray();

                // PackagesPath
                OutputPackagesPath = RestoreSettingsUtils.GetValue(
                    () => string.IsNullOrEmpty(RestorePackagesPathOverride) ? null : UriUtility.GetAbsolutePath(MSBuildStartupDirectory, RestorePackagesPathOverride),
                    () => string.IsNullOrEmpty(RestorePackagesPath) ? null : UriUtility.GetAbsolutePathFromFile(ProjectUniqueName, RestorePackagesPath),
                    () => SettingsUtility.GetGlobalPackagesFolder(settings));

                // Sources
                var currentSources = RestoreSettingsUtils.GetValue(
                    () => RestoreSourcesOverride?.Select(e => UriUtility.GetAbsolutePath(MSBuildStartupDirectory, e)).ToArray(),
                    () => MSBuildRestoreUtility.ContainsClearKeyword(RestoreSources) ? new string[0] : null,
                    () => RestoreSources?.Select(e => UriUtility.GetAbsolutePathFromFile(ProjectUniqueName, e)).ToArray(),
                    () => (new PackageSourceProvider(settings)).LoadPackageSources().Select(e => e.Source).ToArray());

                // Append additional sources
                // Escape strings to avoid xplat path issues with msbuild.
                OutputSources = AppendItems(currentSources, RestoreAdditionalProjectSources)
                    .Select(MSBuildStringUtility.EscapeForwardSlashes)
                    .ToArray();

                // Fallback folders
                var currentFallbackFolders = RestoreSettingsUtils.GetValue(
                    () => RestoreFallbackFoldersOverride?.Select(e => UriUtility.GetAbsolutePath(MSBuildStartupDirectory, e)).ToArray(),
                    () => MSBuildRestoreUtility.ContainsClearKeyword(RestoreFallbackFolders) ? new string[0] : null,
                    () => RestoreFallbackFolders?.Select(e => UriUtility.GetAbsolutePathFromFile(ProjectUniqueName, e)).ToArray(),
                    () => SettingsUtility.GetFallbackPackageFolders(settings).ToArray());

                // Append additional fallback folders
                OutputFallbackFolders = AppendItems(currentFallbackFolders, RestoreAdditionalProjectFallbackFolders);
            }
            catch (Exception ex)
            {
                // Log exceptions with error codes if they exist.
                ExceptionUtilities.LogException(ex, log);
                return false;
            }

            // Log Outputs
            BuildTasksUtility.LogOutputParam(log, nameof(OutputPackagesPath), OutputPackagesPath);
            BuildTasksUtility.LogOutputParam(log, nameof(OutputSources), OutputSources);
            BuildTasksUtility.LogOutputParam(log, nameof(OutputFallbackFolders), OutputFallbackFolders);
            BuildTasksUtility.LogOutputParam(log, nameof(OutputConfigFilePaths), OutputConfigFilePaths);

            return true;
        }

        private string[] AppendItems(string[] current, string[] additional)
        {
            if (additional == null || additional.Length == 0)
            {
                // noop
                return current;
            }

            var additionalAbsolute = additional.Select(e => UriUtility.GetAbsolutePathFromFile(ProjectUniqueName, e));

            return current.Concat(additionalAbsolute).ToArray();
        }
    }
}