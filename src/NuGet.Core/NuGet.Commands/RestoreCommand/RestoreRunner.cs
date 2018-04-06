// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Shared code to run the "restore" command for dotnet restore, nuget.exe, and VS.
    /// </summary>
    public static class RestoreRunner
    {
        /// <summary>
        /// Create requests, execute requests, and commit restore results.
        /// </summary>
        public static async Task<IReadOnlyList<RestoreSummary>> RunAsync(RestoreArgs restoreContext, CancellationToken token)
        {
            // Create requests
            var requests = await GetRequests(restoreContext);

            // Run requests
            return await RunAsync(requests, restoreContext, token);
        }

        /// <summary>
        /// Create requests, execute requests, and commit restore results.
        /// </summary>
        public static async Task<IReadOnlyList<RestoreSummary>> RunAsync(RestoreArgs restoreContext)
        {
            // Run requests
            return await RunAsync(restoreContext, CancellationToken.None);
        }

        private static async Task SetDefaultCredentialProviderAsync(ILogger logger)
        {
            if (HttpHandlerResourceV3.CredentialService == null)
            {
                var providers = await GetCredentialProvidersAsync(logger);
                HttpHandlerResourceV3.CredentialService = new Lazy<ICredentialService>(() => new CredentialService(providers, nonInteractive: false));
            }
        }

        // Add only the secure plugin. This will be done when there's nothing set
        private static async Task<IEnumerable<Credentials.ICredentialProvider>> GetCredentialProvidersAsync(ILogger logger )
        {
            var providers = new List<Credentials.ICredentialProvider>();

            var securePluginProviders = await (new SecureCredentialProviderBuilder(PluginManager.Instance, logger)).BuildAll();

            providers.AddRange(securePluginProviders);

            if (securePluginProviders.Any())
            {
                if (PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders)
                {
                    providers.Add(new DefaultCredentialsCredentialProvider());
                }
            }
            return providers;
        }

        /// <summary>
        /// Execute and commit restore requests.
        /// </summary>
        private static async Task<IReadOnlyList<RestoreSummary>> RunAsync(
            IEnumerable<RestoreSummaryRequest> restoreRequests,
            RestoreArgs restoreContext,
            CancellationToken token)
        {
            var maxTasks = GetMaxTaskCount(restoreContext);

            await SetDefaultCredentialProviderAsync(restoreContext.Log);

            var log = restoreContext.Log;

            if (maxTasks > 1)
            {
                log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_RunningParallelRestore,
                    maxTasks));
            }
            else
            {
                log.LogVerbose(Strings.Log_RunningNonParallelRestore);
            }

            // Get requests
            var requests = new Queue<RestoreSummaryRequest>(restoreRequests);
            var restoreTasks = new List<Task<RestoreSummary>>(maxTasks);
            var restoreSummaries = new List<RestoreSummary>(requests.Count);

            // Run requests
            while (requests.Count > 0)
            {
                // Throttle and wait for a task to finish if we have hit the limit
                if (restoreTasks.Count == maxTasks)
                {
                    var restoreSummary = await CompleteTaskAsync(restoreTasks);
                    restoreSummaries.Add(restoreSummary);
                }

                var request = requests.Dequeue();

                var task = Task.Run(() => ExecuteAndCommitAsync(request, token), token);
                restoreTasks.Add(task);
            }

            // Wait for all restores to finish
            while (restoreTasks.Count > 0)
            {
                var restoreSummary = await CompleteTaskAsync(restoreTasks);
                restoreSummaries.Add(restoreSummary);
            }

            // Summary
            return restoreSummaries;
        }

        /// <summary>
        /// Execute and commit restore requests.
        /// </summary>
        public static async Task<IReadOnlyList<RestoreResultPair>> RunWithoutCommit(
            IEnumerable<RestoreSummaryRequest> restoreRequests,
            RestoreArgs restoreContext)
        {
            var maxTasks = GetMaxTaskCount(restoreContext);

            await SetDefaultCredentialProviderAsync(restoreContext.Log);

            var log = restoreContext.Log;

            if (maxTasks > 1)
            {
                log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_RunningParallelRestore,
                    maxTasks));
            }
            else
            {
                log.LogVerbose(Strings.Log_RunningNonParallelRestore);
            }

            // Get requests
            var requests = new Queue<RestoreSummaryRequest>(restoreRequests);
            var restoreTasks = new List<Task<RestoreResultPair>>(maxTasks);
            var restoreResults = new List<RestoreResultPair>(maxTasks);

            // Run requests
            while (requests.Count > 0)
            {
                // Throttle and wait for a task to finish if we have hit the limit
                if (restoreTasks.Count == maxTasks)
                {
                    var restoreSummary = await CompleteTaskAsync(restoreTasks);
                    restoreResults.Add(restoreSummary);
                }

                var request = requests.Dequeue();

                var task = Task.Run(() => ExecuteAsync(request, CancellationToken.None));
                restoreTasks.Add(task);
            }

            // Wait for all restores to finish
            while (restoreTasks.Count > 0)
            {
                var restoreSummary = await CompleteTaskAsync(restoreTasks);
                restoreResults.Add(restoreSummary);
            }

            // Summary
            return restoreResults;
        }

        /// <summary>
        /// Create restore requests but do not execute them.
        /// </summary>
        public static async Task<IReadOnlyList<RestoreSummaryRequest>> GetRequests(RestoreArgs restoreContext)
        {
            // Get requests
            var requests = new List<RestoreSummaryRequest>();

            var inputs = new List<string>(restoreContext.Inputs);

            // If there are no inputs, use the current directory
            if (restoreContext.PreLoadedRequestProviders.Count < 1 && !inputs.Any())
            {
                inputs.Add(Path.GetFullPath("."));
            }

            // Ignore casing on windows and mac
            var comparer = (RuntimeEnvironmentHelper.IsWindows || RuntimeEnvironmentHelper.IsMacOSX) ?
                StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

            var uniqueRequest = new HashSet<string>(comparer);

            // Create requests
            // Pre-loaded requests
            foreach (var request in await CreatePreLoadedRequests(restoreContext))
            {
                // De-dupe requests
                if (request.Request.LockFilePath == null
                    || uniqueRequest.Add(request.Request.LockFilePath))
                {
                    requests.Add(request);
                }
            }

            // Input based requests
            foreach (var input in inputs)
            {
                var inputRequests = await CreateRequests(input, restoreContext);
                if (inputRequests.Count == 0)
                {
                    // No need to throw here - the situation is harmless, and we want to report all possible
                    // inputs that don't resolve to a project.
                    var message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Error_UnableToLocateRestoreTarget,
                            Path.GetFullPath(input));

                    await restoreContext.Log.LogAsync(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1501, message));
                }
                foreach (var request in inputRequests)
                {
                    // De-dupe requests
                    if (uniqueRequest.Add(request.Request.LockFilePath))
                    {
                        requests.Add(request);
                    }
                }
            }

            return requests;
        }

        private static int GetMaxTaskCount(RestoreArgs restoreContext)
        {
            var maxTasks = 1;

            if (!restoreContext.DisableParallel && !RuntimeEnvironmentHelper.IsMono)
            {
                maxTasks = Environment.ProcessorCount;
            }

            if (maxTasks < 1)
            {
                maxTasks = 1;
            }

            return maxTasks;
        }

        private static async Task<RestoreSummary> ExecuteAndCommitAsync(RestoreSummaryRequest summaryRequest, CancellationToken token)
        {
            var result = await ExecuteAsync(summaryRequest, token);

            return await CommitAsync(result, token);
        }

        private static async Task<RestoreResultPair> ExecuteAsync(RestoreSummaryRequest summaryRequest, CancellationToken token)
        {
            var log = summaryRequest.Request.Log;

            log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject,
                    summaryRequest.InputPath));

            // Run the restore
            var request = summaryRequest.Request;

            // Read the existing lock file, this is needed to support IsLocked=true
            // This is done on the thread and not as part of creating the request due to
            // how long it takes to load the lock file.
            if (request.ExistingLockFile == null)
            {
                request.ExistingLockFile = LockFileUtilities.GetLockFile(request.LockFilePath, log);
            }

            var command = new RestoreCommand(request);
            var result = await command.ExecuteAsync(token);

            return new RestoreResultPair(summaryRequest, result);
        }

        public static async Task<RestoreSummary> CommitAsync(RestoreResultPair restoreResult, CancellationToken token)
        {
            var summaryRequest = restoreResult.SummaryRequest;
            var result = restoreResult.Result;

            var log = summaryRequest.Request.Log;

            // Commit the result
            log.LogInformation(Strings.Log_Committing);
            await result.CommitAsync(log, token);

            if (result.Success)
            {
                log.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_RestoreComplete,
                    DatetimeUtility.ToReadableTimeFormat(result.ElapsedTime),
                    summaryRequest.InputPath));
            }
            else
            {
                log.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_RestoreFailed,
                    DatetimeUtility.ToReadableTimeFormat(result.ElapsedTime),
                    summaryRequest.InputPath));
            }

            // Remote the summary messages from the assets file. This will be removed later.
            var messages = restoreResult.Result.LockFile.LogMessages.Select(e => new RestoreLogMessage(e.Level, e.Code, e.Message));

            // Build the summary
            return new RestoreSummary(
                result,
                summaryRequest.InputPath,
                summaryRequest.ConfigFiles,
                summaryRequest.Sources,
                messages);
        }

        private static async Task<RestoreSummary> CompleteTaskAsync(List<Task<RestoreSummary>> restoreTasks)
        {
            var doneTask = await Task.WhenAny(restoreTasks);
            restoreTasks.Remove(doneTask);
            return await doneTask;
        }

        private static async Task<RestoreResultPair>
            CompleteTaskAsync(List<Task<RestoreResultPair>> restoreTasks)
        {
            var doneTask = await Task.WhenAny(restoreTasks);
            restoreTasks.Remove(doneTask);
            return await doneTask;
        }

        private static async Task<IReadOnlyList<RestoreSummaryRequest>> CreatePreLoadedRequests(
            RestoreArgs restoreContext)
        {
            var results = new List<RestoreSummaryRequest>();

            foreach (var provider in restoreContext.PreLoadedRequestProviders)
            {
                var requests = await provider.CreateRequests(restoreContext);
                results.AddRange(requests);
            }

            return results;
        }

        private static async Task<IReadOnlyList<RestoreSummaryRequest>> CreateRequests(
            string input,
            RestoreArgs restoreContext)
        {
            foreach (var provider in restoreContext.RequestProviders)
            {
                if (await provider.Supports(input))
                {
                    return await provider.CreateRequests(
                        input,
                        restoreContext);
                }
            }

            if (File.Exists(input) || Directory.Exists(input))
            {
                // Not a file or directory we know about. Try to be helpful without response.
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, GetInvalidInputErrorMessage(input), input));
            }

            throw new FileNotFoundException(input);
        }

        public static string GetInvalidInputErrorMessage(string input)
        {
            Debug.Assert(File.Exists(input) || Directory.Exists(input));
            if (File.Exists(input))
            {
                var fileExtension = Path.GetExtension(input);
                if (".json".Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return Strings.Error_InvalidCommandLineInputJson;
                }

                if (".config".Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return Strings.Error_InvalidCommandLineInputConfig;
                }
            }

            return Strings.Error_InvalidCommandLineInput;
        }
    }
}
