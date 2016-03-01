﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.Commands
{
    public static class RestoreRunner
    {
        public static async Task<IReadOnlyList<RestoreSummary>> Run(RestoreArgs restoreContext)
        {
            var maxTasks = 1;

            if (!restoreContext.DisableParallel && !RuntimeEnvironmentHelper.IsMono)
            {
                maxTasks = RestoreRequest.DefaultDegreeOfConcurrency;
            }

            if (maxTasks < 1)
            {
                maxTasks = 1;
            }

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
            var requests = new Queue<RestoreSummaryRequest>();
            var restoreTasks = new List<Task<RestoreSummary>>(maxTasks);
            var restoreSummaries = new List<RestoreSummary>(requests.Count);

            var inputs = new List<string>(restoreContext.Inputs);

            // If there are no inputs, use the current directory
            if (!inputs.Any())
            {
                inputs.Add(Path.GetFullPath("."));
            }

            // Ignore casing on windows and mac
            var comparer = (RuntimeEnvironmentHelper.IsWindows || RuntimeEnvironmentHelper.IsMacOSX) ?
                StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

            var uniqueRequest = new HashSet<string>(comparer);

            // Create requests
            foreach (var input in inputs)
            {
                foreach (var request in await CreateRequests(input, restoreContext))
                {
                    // De-dupe requests
                    if (uniqueRequest.Add(request.Request.LockFilePath))
                    {
                        requests.Enqueue(request);
                    }
                }
            }

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

                var task = Task.Run(async () => await Execute(request));
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

        private static async Task<RestoreSummary> Execute(RestoreSummaryRequest summaryRequest)
        {
            var log = summaryRequest.Request.Log;

            log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject,
                    summaryRequest.InputPath));

            // Run the restore
            var request = summaryRequest.Request;
            var command = new RestoreCommand(request);
            var sw = Stopwatch.StartNew();
            var result = await command.ExecuteAsync();

            // Commit the result
            log.LogInformation(Strings.Log_Committing);
            result.Commit(request.Log);

            sw.Stop();

            if (result.Success)
            {
                log.LogMinimal(
                    summaryRequest.InputPath + Environment.NewLine +
                        string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_RestoreComplete,
                        sw.ElapsedMilliseconds));
            }
            else
            {
                log.LogMinimal(
                    summaryRequest.InputPath + Environment.NewLine +
                        string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_RestoreFailed,
                        sw.ElapsedMilliseconds));
            }

            // Build the summary
            return new RestoreSummary(
                result,
                summaryRequest.InputPath,
                summaryRequest.Settings,
                summaryRequest.Sources,
                summaryRequest.CollectorLogger.Errors);
        }

        private static async Task<RestoreSummary> CompleteTaskAsync(List<Task<RestoreSummary>> restoreTasks)
        {
            var doneTask = await Task.WhenAny(restoreTasks);
            restoreTasks.Remove(doneTask);
            return await doneTask;
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
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_InvalidCommandLineInput,
                        input));
            }
            else
            {
                throw new FileNotFoundException(input);
            }
        }
    }
}
