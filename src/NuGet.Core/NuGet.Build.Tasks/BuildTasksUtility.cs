// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if IS_CORECLR
using System.Runtime.InteropServices;
#endif
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Build.Tasks
{
    public static class BuildTasksUtility
    {
        public static void LogInputParam(Common.ILogger log, string name, params string[] values)
        {
            LogTaskParam(log, "in", name, values);
        }

        public static void LogOutputParam(Common.ILogger log, string name, params string[] values)
        {
            LogTaskParam(log, "out", name, values);
        }

        private static void LogTaskParam(Common.ILogger log, string direction, string name, params string[] values)
        {
            var stringValues = values?.Select(s => s) ?? Enumerable.Empty<string>();

            log.Log(Common.LogLevel.Debug, $"({direction}) {name} '{string.Join(";", stringValues)}'");
        }

        /// <summary>
        /// Add all restorable projects to the restore list.
        /// This is the behavior for --recursive
        /// </summary>
        public static void AddAllProjectsForRestore(DependencyGraphSpec spec)
        {
            // Add everything from projects except for packages.config and unknown project types
            foreach (var project in spec.Projects.Where(DoesProjectSupportRestore))
            {
                spec.AddRestore(project.RestoreMetadata.ProjectUniqueName);
            }
        }

        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key)
        {
            CopyPropertyIfExists(item, properties, key, key);
        }

        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key, string toKey)
        {
            var wrapper = new MSBuildTaskItem(item);

            var propertyValue = wrapper.GetProperty(key);

            if (!string.IsNullOrEmpty(propertyValue)
                && !properties.ContainsKey(key))
            {
                properties.Add(toKey, propertyValue);
            }
        }

        /// <summary>
        /// Determines if the specified <see cref="PackageSpec" /> supports restore.
        /// </summary>
        /// <param name="packageSpec">A <see cref="PackageSpec" /> for a project.</param>
        /// <returns><code>true</code> if the project supports restore, otherwise <code>false</code>.</returns>
        internal static bool DoesProjectSupportRestore(PackageSpec packageSpec)
        {
            return RestorableTypes.Contains(packageSpec.RestoreMetadata.ProjectStyle);
        }

        public static string GetPropertyIfExists(ITaskItem item, string key)
        {
            var wrapper = new MSBuildTaskItem(item);

            var propertyValue = wrapper.GetProperty(key);

            if (!string.IsNullOrEmpty(propertyValue))
            {
                return propertyValue;
            }

            return null;
        }

        public static void AddPropertyIfExists(IDictionary<string, string> properties, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)
                && !properties.ContainsKey(key))
            {
                properties.Add(key, value);
            }
        }

        public static void AddPropertyIfExists(IDictionary<string, string> properties, string key, string[] value)
        {
            if (value!=null && !properties.ContainsKey(key))
            {
                properties.Add(key, string.Concat(value.Select(e => e + ";")));
            }
        }

        private static HashSet<ProjectStyle> RestorableTypes = new HashSet<ProjectStyle>()
        {
            ProjectStyle.DotnetCliTool,
            ProjectStyle.PackageReference,
            ProjectStyle.Standalone,
            ProjectStyle.ProjectJson
        };

        internal static async Task<bool> RestoreAsync(
            DependencyGraphSpec dependencyGraphSpec,
            bool interactive,
            bool recursive,
            bool noCache,
            bool ignoreFailedSources,
            bool disableParallel,
            bool force,
            bool forceEvaluate,
            bool hideWarningsAndErrors,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            if(dependencyGraphSpec == null)
            {
                throw new ArgumentNullException(nameof(dependencyGraphSpec));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            try
            {
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(log, !interactive);

                // Set connection limit
                NetworkProtocolUtility.SetConnectionLimit();

                // Set user agent string used for network calls
#if IS_CORECLR
                UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet .NET Core MSBuild Task")
                    .WithOSDescription(RuntimeInformation.OSDescription));
#else
                // OS description is set by default on Desktop
                UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet Desktop MSBuild Task"));
#endif

                // This method has no effect on .NET Core.
                NetworkProtocolUtility.ConfigureSupportedSslProtocols();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    cacheContext.NoCache = noCache;
                    cacheContext.IgnoreFailedSources = ignoreFailedSources;

                    // Pre-loaded request provider containing the graph file
                    var providers = new List<IPreLoadedRestoreRequestProvider>();

                    if (dependencyGraphSpec.Restore.Count < 1)
                    {
                        // Restore will fail if given no inputs, but here we should skip it and provide a friendly message.
                        log.LogMinimal(Strings.NoProjectsToRestore);
                        return true;
                    }

                    // Add all child projects
                    if (recursive)
                    {
                        AddAllProjectsForRestore(dependencyGraphSpec);
                    }

                    providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dependencyGraphSpec));

                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        LockFileVersion = LockFileFormat.Version,
                        // 'dotnet restore' fails on slow machines (https://github.com/NuGet/Home/issues/6742)
                        // The workaround is to pass the '--disable-parallel' option.
                        // We apply the workaround by default when the system has 1 cpu.
                        // This will fix restore failures on VMs with 1 CPU and containers with less or equal to 1 CPU assigned.
                        DisableParallel = Environment.ProcessorCount == 1 ? true : disableParallel,
                        Log = log,
                        MachineWideSettings = new XPlatMachineWideSetting(),
                        PreLoadedRequestProviders = providers,
                        AllowNoOp = !force,
                        HideWarningsAndErrors = hideWarningsAndErrors,
                        RestoreForceEvaluate = forceEvaluate
                    };

                    if (restoreContext.DisableParallel)
                    {
                        HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    var restoreSummaries = await RestoreRunner.RunAsync(restoreContext, cancellationToken);

                    // Summary
                    RestoreSummary.Log(log, restoreSummaries);

                    return restoreSummaries.All(x => x.Success);
                }
            }
            finally
            {
                // The CredentialService lifetime is for the duration of the process. We should not leave a potentially unavailable logger. 
                // We need to update the delegating logger with a null instance
                // because the tear downs of the plugins and similar rely on idleness and process exit.
                DefaultCredentialServiceUtility.UpdateCredentialServiceDelegatingLogger(NullLogger.Instance);
            }
        }

        /// <summary>
        /// Determines the restore style of a project.
        /// </summary>
        /// <param name="restoreProjectStyle">An optional user supplied restore style.</param>
        /// <param name="hasPackageReferenceItems">A <see cref="bool"/> indicating whether or not the project has any PackageReference items.</param>
        /// <param name="projectJsonPath">An optional path to the project's project.json file.</param>
        /// <param name="projectDirectory">The full path to the project directory.</param>
        /// <param name="projectName">The name of the project file.</param>
        /// <param name="log">An <see cref="NuGet.Common.ILogger"/> object used to log messages.</param>
        /// <returns>A <see cref="Tuple{ProjectStyle, Boolean}"/> containing the project style and a value indicating if the project is using a style that is compatible with PackageReference.
        /// If the value of <paramref name="restoreProjectStyle"/> is not empty and could not be parsed, <code>null</code> is returned.</returns>
        internal static (ProjectStyle ProjectStyle, bool IsPackageReferenceCompatibleProjectStyle) GetProjectRestoreStyle(string restoreProjectStyle, bool hasPackageReferenceItems, string projectJsonPath, string projectDirectory, string projectName, Common.ILogger log)
        {
            ProjectStyle projectStyle;

            // Allow a user to override by setting RestoreProjectStyle in the project.
            if (!string.IsNullOrWhiteSpace(restoreProjectStyle))
            {
                if (!Enum.TryParse(restoreProjectStyle, ignoreCase: true, out projectStyle))
                {
                    // Any value that is not recognized is treated as Unknown
                    projectStyle = ProjectStyle.Unknown;
                }
            }
            else if (hasPackageReferenceItems)
            {
                // If any PackageReferences exist treat it as PackageReference. This has priority over project.json.
                projectStyle = ProjectStyle.PackageReference;
            }
            else if (!string.IsNullOrWhiteSpace(projectJsonPath))
            {
                // If this is not a PackageReference project check if project.json or projectName.project.json exists.
                projectStyle = ProjectStyle.ProjectJson;
            }
            else if (ProjectHasPackagesConfigFile(projectDirectory, projectName))
            {
                // If this is not a PackageReference or ProjectJson project check if packages.config or packages.ProjectName.config exists
                projectStyle = ProjectStyle.PackagesConfig;
            }
            else
            {
                // This project is either a packages.config project or one that does not use NuGet at all.
                projectStyle = ProjectStyle.Unknown;
            }

            bool isPackageReferenceCompatibleProjectStyle = projectStyle == ProjectStyle.PackageReference || projectStyle == ProjectStyle.DotnetToolReference;

            return (projectStyle, isPackageReferenceCompatibleProjectStyle);
        }

        /// <summary>
        /// Determines if the project has a packages.config file.
        /// </summary>
        /// <param name="projectDirectory">The full path of the project directory.</param>
        /// <param name="projectName">The name of the project file.</param>
        /// <returns><code>true</code> if a packages.config exists next to the project, otherwise <code>false</code>.</returns>
        private static bool ProjectHasPackagesConfigFile(string projectDirectory, string projectName)
        {
            if(string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new ArgumentNullException(nameof(projectDirectory));
            }

            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentNullException(nameof(projectName));
            }

            string packagesConfigPath = Path.Combine(projectDirectory, NuGetConstants.PackageReferenceFile);

            if (File.Exists(packagesConfigPath))
            {
                return true;
            }

            packagesConfigPath = Path.Combine(projectDirectory, $"packages.{projectName}.config");

            if (File.Exists(packagesConfigPath))
            {
                return true;
            }

            return false;
        }
    }
}
