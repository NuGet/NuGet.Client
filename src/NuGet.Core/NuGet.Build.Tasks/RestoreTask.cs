// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// .NET Core compatible restore task for PackageReference and UWP project.json projects.
    /// </summary>
    public class RestoreTask : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
    {
#if IS_DESKTOP
        private const string HttpUserAgent = "NuGet Desktop MSBuild Task";
#else
        private const string HttpUserAgent = "NuGet .NET Core MSBuild Task";
#endif

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// DG file entries
        /// </summary>
        [Required]
        public ITaskItem[] RestoreGraphItems { get; set; }

        /// <summary>
        /// Disable parallel project restores and downloads
        /// </summary>
        public bool RestoreDisableParallel { get; set; }

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

        /// <summary>
        /// Force restore, skip no op
        /// </summary>
        public bool RestoreForce { get; set; }

        /// <summary>
        /// Do not display Errors and Warnings to the user. 
        /// The Warnings and Errors are written into the assets file and will be read by an sdk target.
        /// </summary>
        public bool HideWarningsAndErrors { get; set; }

        /// <summary>
        /// Set this property if you want to get an interactive restore
        /// </summary>
        public bool Interactive { get; set; }

        /// <summary>
        /// Reevaluate resotre graph even with a lock file, skip no op as well.
        /// </summary>
        public bool RestoreForceEvaluate { get; set; }

        public override bool Execute()
        {
#if DEBUG
            var debugPackTask = Environment.GetEnvironmentVariable("DEBUG_RESTORE_TASK");
            if (!string.IsNullOrEmpty(debugPackTask) && debugPackTask.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
#if IS_CORECLR
                Console.WriteLine("Waiting for debugger to attach.");
                Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }
                Debugger.Break();
#else
            Debugger.Launch();
#endif
            }
#endif
            var log = new MSBuildLogger(Log);

            NuGet.Common.Migrations.MigrationRunner.Run();

            // Log inputs
            log.LogDebug($"(in) RestoreGraphItems Count '{RestoreGraphItems?.Count() ?? 0}'");
            log.LogDebug($"(in) RestoreDisableParallel '{RestoreDisableParallel}'");
            log.LogDebug($"(in) RestoreNoCache '{RestoreNoCache}'");
            log.LogDebug($"(in) RestoreIgnoreFailedSources '{RestoreIgnoreFailedSources}'");
            log.LogDebug($"(in) RestoreRecursive '{RestoreRecursive}'");
            log.LogDebug($"(in) RestoreForce '{RestoreForce}'");
            log.LogDebug($"(in) HideWarningsAndErrors '{HideWarningsAndErrors}'");
            log.LogDebug($"(in) RestoreForceEvaluate '{RestoreForceEvaluate}'");

            try
            {
                return ExecuteAsync(log).Result;
            }
            catch (AggregateException ex) when (_cts.Token.IsCancellationRequested && ex.InnerException is TaskCanceledException)
            {
                // Canceled by user
                log.LogError(Strings.RestoreCanceled);
                return false;
            }
            catch (Exception e)
            {
                ExceptionUtilities.LogException(e, log);
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
                    DisableParallel = RestoreDisableParallel,
                    Log = log,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    PreLoadedRequestProviders = providers,
                    CachingSourceProvider = sourceProvider,
                    AllowNoOp = !RestoreForce,
                    HideWarningsAndErrors = HideWarningsAndErrors,
                    RestoreForceEvaluate = RestoreForceEvaluate
                };

                if (restoreContext.DisableParallel)
                {
                    HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
                }

                DefaultCredentialServiceUtility.SetupDefaultCredentialService(log, !Interactive);

                _cts.Token.ThrowIfCancellationRequested();

                var restoreSummaries = await RestoreRunner.RunAsync(restoreContext, _cts.Token);

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
#if IS_CORECLR
            UserAgent.SetUserAgentString(new UserAgentStringBuilder(HttpUserAgent)
                .WithOSDescription(RuntimeInformation.OSDescription));
#else
            // OS description is set by default on Desktop
            UserAgent.SetUserAgentString(new UserAgentStringBuilder(HttpUserAgent));
#endif
        }

        public void Cancel()
        {
            _cts.Cancel();
        }

        public void Dispose()
        {
            _cts.Dispose();
        }
    }
}