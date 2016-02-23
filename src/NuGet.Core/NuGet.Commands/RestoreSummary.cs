// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    public class RestoreSummary
    {
        public bool Success { get; }

        public string InputPath { get; }

        public IList<string> ConfigFiles { get; }

        public IList<string> FeedsUsed { get; }

        public int InstallCount { get; }

        public IList<string> Errors { get; }

        public RestoreSummary(bool success)
        {
            Success = success;
            InputPath = null;
            ConfigFiles = new List<string>().AsReadOnly();
            FeedsUsed = new List<string>().AsReadOnly();
            InstallCount = 0;
            Errors = new List<string>().AsReadOnly();
        }

        public RestoreSummary(
            RestoreResult result,
            string inputPath,
            ISettings settings,
            IEnumerable<SourceRepository> sourceRepositories,
            IEnumerable<string> errors)
        {
            Success = result.Success;
            InputPath = inputPath;
            ConfigFiles = settings
                .Priority
                .Select(childSettings => Path.Combine(childSettings.Root, childSettings.FileName))
                .ToList()
                .AsReadOnly();
            FeedsUsed = sourceRepositories
                .Select(source => source.PackageSource.Source)
                .ToList()
                .AsReadOnly();
            InstallCount = result.GetAllInstalled().Count;
            Errors = errors.ToArray();
        }

        public RestoreSummary(
            bool success,
            string inputPath,
            IEnumerable<string> configFiles,
            IEnumerable<string> feedsUsed,
            int installCount,
            IEnumerable<string> errors)
        {
            Success = success;
            InputPath = inputPath;
            ConfigFiles = configFiles.ToList().AsReadOnly();
            FeedsUsed = feedsUsed.ToList().AsReadOnly();
            InstallCount = installCount;
            Errors = errors.ToArray();
        }

        public static void Log(ILogger logger, IEnumerable<RestoreSummary> restoreSummaries, bool showInformation)
        {
            if (!restoreSummaries.Any())
            {
                return;
            }

            // Display the errors summary
            foreach (var restoreSummary in restoreSummaries)
            {
                if (!restoreSummary.Errors.Any())
                {
                    continue;
                }

                logger.LogSummary(string.Empty);
                logger.LogSummary(string.Format(CultureInfo.CurrentCulture, Strings.Log_ErrorSummary, restoreSummary.InputPath));
                foreach (var error in restoreSummary.Errors)
                {
                    foreach (var line in IndentLines(error))
                    {
                        logger.LogSummary(line);
                    }
                }
            }

            if (showInformation)
            {
                // Display the information summary
                var configFiles = restoreSummaries
                    .SelectMany(summary => summary.ConfigFiles)
                    .Distinct();

                if (configFiles.Any())
                {
                    logger.LogSummary(string.Empty);
                    logger.LogSummary(Strings.Log_ConfigFileSummary);
                    foreach (var configFile in configFiles)
                    {
                        logger.LogSummary($"    {configFile}");
                    }
                }

                var feedsUsed = restoreSummaries
                    .SelectMany(summary => summary.FeedsUsed)
                    .Distinct();

                if (feedsUsed.Any())
                {
                    logger.LogSummary(string.Empty);
                    logger.LogSummary(Strings.Log_FeedsUsedSummary);
                    foreach (var feedUsed in feedsUsed)
                    {
                        logger.LogSummary($"    {feedUsed}");
                    }
                }

                var installed = restoreSummaries
                    .GroupBy(summary => summary.InputPath, summary => summary.InstallCount)
                    .Select(group => new KeyValuePair<string, int>(group.Key, group.Sum()))
                    .Where(pair => pair.Value > 0);

                if (installed.Any())
                {
                    logger.LogSummary(string.Empty);
                    logger.LogSummary(Strings.Log_InstalledSummary);
                    foreach (var pair in installed)
                    {
                        logger.LogSummary("    " + string.Format(CultureInfo.CurrentCulture, Strings.Log_InstalledSummaryCount, pair.Value, pair.Key));
                    }
                }
            }
        }

        private static IEnumerable<string> IndentLines(string input)
        {
            using (var reader = new StringReader(input))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return $"    {line.TrimEnd()}";
                }
            }
        }
    }
}