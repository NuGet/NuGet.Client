// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Shared;

namespace NuGet.Commands
{
    public class RestoreSummary
    {
        public bool Success { get; }

        public bool NoOpRestore { get; }

        public string InputPath { get; }

        public IList<string> ConfigFiles { get; }

        public IList<string> FeedsUsed { get; }

        public int InstallCount { get; }

        public IList<IRestoreLogMessage> Errors { get; }

        public RestoreSummary(bool success)
        {
            Success = success;
            NoOpRestore = false;
            InputPath = null;
            ConfigFiles = new List<string>().AsReadOnly();
            FeedsUsed = new List<string>().AsReadOnly();
            InstallCount = 0;
            Errors = new List<IRestoreLogMessage>().AsReadOnly();
        }

        public RestoreSummary(
            RestoreResult result,
            string inputPath,
            IEnumerable<string> configFiles,
            IEnumerable<SourceRepository> sourceRepositories,
            IEnumerable<RestoreLogMessage> errors)
        {
            Success = result.Success;
            NoOpRestore = result is NoOpRestoreResult;
            InputPath = inputPath;
            ConfigFiles = configFiles.AsList().AsReadOnly();
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
            IEnumerable<IRestoreLogMessage> errors)
        {
            Success = success;
            InputPath = inputPath;
            ConfigFiles = configFiles.ToList().AsReadOnly();
            FeedsUsed = feedsUsed.ToList().AsReadOnly();
            InstallCount = installCount;
            Errors = errors.ToArray();
        }

        public static void Log(ILogger logger, IEnumerable<RestoreSummary> restoreSummaries, bool logErrors = false)
        {
            if (restoreSummaries.Count() == 0)
            {
                return;
            }

            // This should only be true by nuget exe since it does not have msbuild logger
            if (logErrors)
            {
                // Display the errors summary
                foreach (var restoreSummary in restoreSummaries)
                {
                    var errors = restoreSummary
                        .Errors
                        .Where(m => m.Level == LogLevel.Error)
                        .ToList();

                    if (errors.Count == 0)
                    {
                        continue;
                    }

                    logger.LogError(string.Empty);
                    logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_ErrorSummary, restoreSummary.InputPath));
                    foreach (var error in errors)
                    {
                        foreach (var line in IndentLines(error.FormatWithCode()))
                        {
                            logger.LogError(line);
                        }
                    }
                }
            }

            // Display the information summary
            var configFiles = restoreSummaries
                .SelectMany(summary => summary.ConfigFiles)
                .Distinct();

            if (configFiles.Any())
            {
                logger.LogInformationSummary(string.Empty);
                logger.LogInformationSummary(Strings.Log_ConfigFileSummary);
                foreach (var configFile in configFiles)
                {
                    logger.LogInformationSummary($"    {configFile}");
                }
            }

            var feedsUsed = restoreSummaries
                .SelectMany(summary => summary.FeedsUsed)
                .Distinct();

            if (feedsUsed.Any())
            {
                logger.LogInformationSummary(string.Empty);
                logger.LogInformationSummary(Strings.Log_FeedsUsedSummary);
                foreach (var feedUsed in feedsUsed)
                {
                    logger.LogInformationSummary($"    {feedUsed}");
                }
            }

            var installed = restoreSummaries
                .GroupBy(summary => summary.InputPath, summary => summary.InstallCount)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Sum()))
                .Where(pair => pair.Value > 0);

            if (installed.Any())
            {
                logger.LogInformationSummary(string.Empty);
                logger.LogInformationSummary(Strings.Log_InstalledSummary);
                foreach (var pair in installed)
                {
                    logger.LogInformationSummary("    " + string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_InstalledSummaryCount,
                        pair.Value,
                        pair.Key));
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