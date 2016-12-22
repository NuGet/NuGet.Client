// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// .NET Core compatible restore task for PackageReference and UWP project.json projects.
    /// </summary>
    public class RestoreTask : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// DG file entries
        /// </summary>
        [Required]
        public ITaskItem[] RestoreGraphItems { get; set; }

        /// <summary>
        /// NuGet sources, ; delimited
        /// </summary>
        public string RestoreSources { get; set; }

        /// <summary>
        /// NuGet fallback folders
        /// </summary>
        public string RestoreFallbackFolders { get; set; }

        /// <summary>
        /// User packages folder
        /// </summary>
        public string RestorePackagesPath { get; set; }

        /// <summary>
        /// Disable parallel project restores and downloads
        /// </summary>
        public bool RestoreDisableParallel { get; set; }

        /// <summary>
        /// NuGet.Config path
        /// </summary>
        public string RestoreConfigFile { get; set; }

        /// <summary>
        /// Disable the web cache
        /// </summary>
        public bool RestoreNoCache { get; set; }

        /// <summary>
        /// Ignore errors from package sources
        /// </summary>
        public bool RestoreIgnoreFailedSources { get; set; }

        /// <summary>
        /// Restore all projects.
        /// </summary>
        public bool RestoreRecursive { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);

            // Log inputs
            log.LogDebug($"(in) RestoreGraphItems Count '{RestoreGraphItems?.Count() ?? 0}'");
            log.LogDebug($"(in) RestoreSources '{RestoreSources}'");
            log.LogDebug($"(in) RestorePackagesPath '{RestorePackagesPath}'");
            log.LogDebug($"(in) RestoreFallbackFolders '{RestoreFallbackFolders}'");
            log.LogDebug($"(in) RestoreDisableParallel '{RestoreDisableParallel}'");
            log.LogDebug($"(in) RestoreConfigFile '{RestoreConfigFile}'");
            log.LogDebug($"(in) RestoreNoCache '{RestoreNoCache}'");
            log.LogDebug($"(in) RestoreIgnoreFailedSources '{RestoreIgnoreFailedSources}'");
            log.LogDebug($"(in) RestoreRecursive '{RestoreRecursive}'");

            try
            {
                return ExecuteAsync(log).Result;
            }
            catch (Exception e)
            {
                // Log the error
                if (ExceptionLogger.Instance.ShowStack)
                {
                    log.LogError(e.ToString());
                }
                else
                {
                    log.LogError(ExceptionUtilities.DisplayMessage(e));
                }

                // Log the stack trace as verbose output.
                log.LogVerbose(e.ToString());

                return false;
            }
        }

        private async Task<bool> ExecuteAsync(Common.ILogger log)
        {
            if (RestoreGraphItems.Length < 1)
            {
                log.LogWarning(Strings.NoProjectsProvidedToTask);
                return true;
            }

            // Set user agent and connection settings.
            ConfigureProtocol();

            // Convert to the internal wrapper
            var wrappedItems = RestoreGraphItems.Select(MSBuildUtility.WrapMSBuildItem);

            //var graphLines = RestoreGraphItems;
            var providerCache = new RestoreCommandProvidersCache();

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = RestoreNoCache;
                cacheContext.IgnoreFailedSources = RestoreIgnoreFailedSources;

                // Pre-loaded request provider containing the graph file
                var providers = new List<IPreLoadedRestoreRequestProvider>();

                var dgFile = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);

                if (dgFile.Restore.Count < 1)
                {
                    // Restore will fail if given no inputs, but here we should skip it and provide a friendly message.
                    log.LogMinimal(Strings.NoProjectsToRestore);
                    return true;
                }

                // Add all child projects
                if (RestoreRecursive)
                {
                    BuildTasksUtility.AddAllProjectsForRestore(dgFile);
                }

                providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dgFile));

                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = cacheContext,
                    LockFileVersion = LockFileFormat.Version,
                    ConfigFile = MSBuildStringUtility.TrimAndGetNullForEmpty(RestoreConfigFile),
                    DisableParallel = RestoreDisableParallel,
                    GlobalPackagesFolder = RestorePackagesPath,
                    Log = log,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    PreLoadedRequestProviders = providers,
                    CachingSourceProvider = sourceProvider
                };

                if (!string.IsNullOrEmpty(RestoreSources))
                {
                    var sources = MSBuildStringUtility.Split(RestoreSources);
                    restoreContext.Sources.AddRange(sources);
                }

                if (restoreContext.DisableParallel)
                {
                    HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
                }

                var restoreSummaries = await RestoreRunner.Run(restoreContext);

                // Summary
                RestoreSummary.Log(log, restoreSummaries);

                return restoreSummaries.All(x => x.Success);
            }
        }

        private static void ConfigureProtocol()
        {
            // Set connection limit
            NetworkProtocolUtility.SetConnectionLimit();

            // Set user agent string used for network calls
            SetUserAgent();

            // This method has no effect on .NET Core.
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();
        }

        private static void SetUserAgent()
        {
            var agent = "NuGet MSBuild Task";

#if IS_CORECLR
            UserAgent.SetUserAgentString(new UserAgentStringBuilder(agent)
                .WithOSDescription(RuntimeInformation.OSDescription));
#else
            // OS description is set by default on Desktop
            UserAgent.SetUserAgentString(new UserAgentStringBuilder(agent));
#endif
        }
    }
}