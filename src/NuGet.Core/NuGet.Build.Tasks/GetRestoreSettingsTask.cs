// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Get all the settings to be used for project restore.
    /// </summary>
#if !NETFRAMEWORK
    [MSBuildMultiThreadableTask]
#endif
    public class GetRestoreSettingsTask : Microsoft.Build.Utilities.Task
#if !NETFRAMEWORK
        , IMultiThreadableTask
#endif
    {

#if !NETFRAMEWORK
        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;
#endif

        private readonly IEnvironmentVariableReader _environmentVariableReader;

        public GetRestoreSettingsTask()
            : this(EnvironmentVariableWrapper.Instance)
        {
        }

        internal GetRestoreSettingsTask(IEnvironmentVariableReader environmentVariableReader)
        {
            _environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
        }

        [Required]
        public string ProjectUniqueName { get; set; }

        public string[] RestoreSources { get; set; }

        public string RestorePackagesPath { get; set; }

        public string RestoreRepositoryPath { get; set; }

        public string[] RestoreFallbackFolders { get; set; }

        public string RestoreConfigFile { get; set; }

        public string RestoreSolutionDirectory { get; set; }

        /// <summary>
        /// The root directory from which to talk to find the config files. Used by the CLI in Dotnet Tool install
        /// </summary>
        public string RestoreRootConfigDirectory { get; set; }

        /// <summary>
        /// Settings read with TargetFramework set
        /// </summary>
        public ITaskItem[] RestoreSettingsPerFramework { get; set; }

        /// <summary>
        /// Command line value of RestorePackagesPath
        /// </summary>
        public string RestorePackagesPathOverride { get; set; }

        public string RestoreRepositoryPathOverride { get; set; }

        /// <summary>
        /// Command line value of RestoreSources
        /// </summary>
        public string[] RestoreSourcesOverride { get; set; }

        /// <summary>
        /// Command line value of RestoreFallbackFolders
        /// </summary>
        public string[] RestoreFallbackFoldersOverride { get; set; }

        /// <summary>
        /// Restore style for the project
        /// </summary>
        public string RestoreProjectStyle { get; set; }

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
        public string OutputRepositoryPath { get; set; }

        [Output]
        public string[] OutputFallbackFolders { get; set; }

        [Output]
        public string[] OutputConfigFilePaths { get; set; }

        private static Lazy<IMachineWideSettings> _machineWideSettings = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        public override bool Execute()
        {
#if DEBUG
            var debugRestoreTask = _environmentVariableReader.GetEnvironmentVariable("DEBUG_RESTORE_SETTINGS_TASK");
            if (!string.IsNullOrEmpty(debugRestoreTask) && debugRestoreTask.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debugger.Launch();
            }
#endif
            var log = new MSBuildLogger(Log);

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

                var absoluteMSBuildStartupDirectory = MakeAbsolute(MSBuildStartupDirectory);
                var absoluteProjectUniqueName = MakeAbsolute(ProjectUniqueName);
                var absoluteProjectDirectory = Path.GetDirectoryName(absoluteProjectUniqueName);

                // Settings
                // Find the absolute path of nuget.config, this should only be set on the command line. Setting the path in project files
                // is something that could happen, but it is not supported.
                var absoluteConfigFilePath = GetGlobalAbsolutePath(absoluteMSBuildStartupDirectory, RestoreConfigFile);
                string restoreDir;
                // To match non-msbuild behavior, we only default the restoreDir for non-PackagesConfig scenarios.
                if (string.IsNullOrEmpty(RestoreRootConfigDirectory))
                {
                    restoreDir = absoluteProjectDirectory;
                }
                else
                {
                    restoreDir = MakeAbsolute(RestoreRootConfigDirectory);
                }
                var settings = RestoreSettingsUtils.ReadSettings(MakeAbsolute(RestoreSolutionDirectory), restoreDir, absoluteConfigFilePath, _machineWideSettings);
                OutputConfigFilePaths = settings.GetConfigFilePaths().ToArray();

                // PackagesPath
                OutputPackagesPath = RestoreSettingsUtils.GetValue(
                    () => GetGlobalAbsolutePath(absoluteMSBuildStartupDirectory, RestorePackagesPathOverride),
                    () => string.IsNullOrEmpty(RestorePackagesPath) ? null : UriUtility.GetAbsolutePathFromFile(absoluteProjectUniqueName, RestorePackagesPath),
                    () => SettingsUtility.GetGlobalPackagesFolder(settings));

                OutputRepositoryPath = RestoreSettingsUtils.GetValue(
                    () => GetGlobalAbsolutePath(absoluteMSBuildStartupDirectory, RestoreRepositoryPathOverride),
                    () => string.IsNullOrEmpty(RestoreRepositoryPath) ? null : UriUtility.GetAbsolutePathFromFile(absoluteProjectUniqueName, RestoreRepositoryPath),
                    () => SettingsUtility.GetRepositoryPath(settings));

                // Sources
                OutputSources = BuildTasksUtility.GetSources(
                    absoluteMSBuildStartupDirectory,
                    absoluteProjectDirectory,
                    RestoreSources,
                    RestoreSourcesOverride,
                    GetPropertyValues(RestoreSettingsPerFramework, "RestoreAdditionalProjectSources"),
                    settings);

                // Fallback folders
                OutputFallbackFolders = BuildTasksUtility.GetFallbackFolders(
                    absoluteMSBuildStartupDirectory,
                    absoluteProjectDirectory,
                    RestoreFallbackFolders, RestoreFallbackFoldersOverride,
                    GetPropertyValues(RestoreSettingsPerFramework, "RestoreAdditionalProjectFallbackFolders"),
                    GetPropertyValues(RestoreSettingsPerFramework, "RestoreAdditionalProjectFallbackFoldersExcludes"),
                    settings);
            }
            catch (Exception ex)
            {
                // Log exceptions with error codes if they exist.
                ExceptionUtilities.LogException(ex, log);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Read a metadata property from each item and split the values.
        /// Nulls and empty values are ignored.
        /// </summary>
        private static IEnumerable<string> GetPropertyValues(ITaskItem[] items, string key)
        {
            if (items == null)
            {
                return Enumerable.Empty<string>();
            }

            return items.SelectMany(e => MSBuildStringUtility.Split(BuildTasksUtility.GetPropertyIfExists(e, key)));
        }


#if NETFRAMEWORK
#pragma warning disable CA1822 
#endif
        private string MakeAbsolute(string path)
        {
#if !NETFRAMEWORK
            return string.IsNullOrEmpty(path) ? path : TaskEnvironment.GetAbsolutePath(path).Value;
#else
            return path;
#endif
        }
#if NETFRAMEWORK
#pragma warning restore CA1822
#endif

        /// <summary>
        /// Resolves a command-line override path against the (absolute) MSBuild startup directory. A relative
        /// <paramref name="path" /> is combined with that absolute base, so the common case does not depend on the
        /// process working directory. Note that UriUtility does not fully absolutize every path shape: rooted or
        /// drive-relative values (for example <c>\foo</c> or <c>C:</c>) can still be resolved by
        /// <see cref="Path.GetFullPath(string)" /> against the current directory/drive. That is pre-existing UriUtility
        /// behavior (identical on .NET Framework) for these unusual inputs and is out of scope for this task; non-file
        /// values (URLs, UNC shares) are returned by UriUtility unchanged.
        /// </summary>
        private static string GetGlobalAbsolutePath(string absoluteMSBuildStartupDirectory, string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                return UriUtility.GetAbsolutePath(absoluteMSBuildStartupDirectory, path);
            }

            return path;
        }
    }
}
