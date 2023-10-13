// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;

namespace NuGet.Commands.CommandRunners
{
    enum Verbosity
    {
        Normal,
        Quiet,
        Detailed
    }
    public static class PackageSearchRunner
    {
        const int LineSeparatorLength = 20;
        static readonly string SourceSeparator = new string('=', LineSeparatorLength);
        static readonly string PackageSeparator = new string('-', LineSeparatorLength);

        /// <summary>
        /// Runs the search operation asynchronously using the provided parameters.
        /// </summary>
        /// <param name="sourceProvider">The provider for package sources.</param>
        /// <param name="sources">The list of package sources.</param>
        /// <param name="searchTerm">The term to search for within the package sources.</param>
        /// <param name="skip">The number of results to skip.</param>
        /// <param name="take">The number of results to retrieve.</param>
        /// <param name="prerelease">A flag indicating whether to include prerelease packages in the search.</param>
        /// <param name="exactMatch">A flag indicating whether to perform an exact match search.</param>
        /// <param name="verbosity">The verbosity level for logging.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task RunAsync(IPackageSourceProvider sourceProvider, List<string> sources, string searchTerm, int skip, int take, bool prerelease, bool exactMatch, int verbosity, ILogger logger)
        {
            CancellationToken cancellationToken = CancellationToken.None;

            var listEndpoints = GetEndpointsAsync(sources, sourceProvider);
            WarnForHTTPSources(listEndpoints, logger);

            if (exactMatch)
            {
                await GetExactMatch(listEndpoints, searchTerm, prerelease, LineSeparatorLength, (Verbosity)verbosity, logger);
                return;
            }

            var taskList = BuildTaskListForSearch(listEndpoints, searchTerm, prerelease, skip, take, logger, cancellationToken);
            await ProcessTaskList(await taskList, (Verbosity)verbosity, logger);
        }

        /// <summary>
        /// Searches for an exact match of the specified search term within the provided package sources.
        /// </summary>
        /// <param name="listEndpoints">The list of package sources/endpoints.</param>
        /// <param name="searchTerm">The term to search for within the package sources.</param>
        /// <param name="prerelease">A flag indicating whether to include prerelease packages in the search.</param>
        /// <param name="lineSeparatorLength">The length of the line separator (used for formatting).</param>
        /// <param name="verbosity">The verbosity level for logging.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task GetExactMatch(IList<PackageSource> listEndpoints, string searchTerm, bool prerelease, int lineSeparatorLength, Verbosity verbosity, ILogger logger)
        {
            var taskList = BuildTaskListForExactMatch(listEndpoints, searchTerm, prerelease, logger);
            await ProcessTaskList(await taskList, verbosity, logger);
        }

        /// <summary>
        /// Builds a list of tasks that perform package search operations.
        /// </summary>
        /// <param name="listEndpoints">The list of package sources/endpoints.</param>
        /// <param name="searchTerm">The term to search for within the package sources.</param>
        /// <param name="prerelease">A flag indicating whether to include prerelease packages in the search.</param>
        /// <param name="skip">The number of results to skip.</param>
        /// <param name="take">The number of results to retrieve.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of tasks that perform package search operations.</returns>
        private static async Task<List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>> BuildTaskListForSearch(IList<PackageSource> listEndpoints, string searchTerm, bool prerelease, int skip, int take, ILogger logger, CancellationToken cancellationToken)
        {
            var taskList = new List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>();

            foreach (var source in listEndpoints)
            {
                var repository = Repository.Factory.GetCoreV3(source);
                var resource = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken);

                taskList.Add(
                    resource == null ? (null, source) :
                    (Task.Run(() => resource.SearchAsync(searchTerm, new SearchFilter(includePrerelease: prerelease), skip, take, logger, cancellationToken)), source)
                );
            }

            return taskList;
        }

        /// <summary>
        /// Builds a list of tasks that perform exact match package search operations.
        /// </summary>
        /// <param name="listEndpoints">The list of package sources/endpoints.</param>
        /// <param name="searchTerm">The term to search for within the package sources.</param>
        /// <param name="prerelease">A flag indicating whether to include prerelease packages in the search.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <returns>A list of tasks that perform exact match package search operations.</returns>
        private static async Task<List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>> BuildTaskListForExactMatch(IList<PackageSource> listEndpoints, string searchTerm, bool prerelease, ILogger logger)
        {
            var taskList = new List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>();

            foreach (var endpoint in listEndpoints)
            {
                var cancellationToken = CancellationToken.None;
                using var cache = new SourceCacheContext();
                var repository = Repository.Factory.GetCoreV3(endpoint);
                var resource = await repository.GetResourceAsync<PackageMetadataResource>();

                taskList.Add(
                    resource == null ? (null, endpoint) :
                    (Task.Run(() => resource.GetMetadataAsync(searchTerm, includePrerelease: prerelease, includeUnlisted: false, cache, logger, cancellationToken)), endpoint)
                );
            }

            return taskList;
        }

        /// <summary>
        /// Processes the given task list by awaiting each task and logging the results.
        /// </summary>
        /// <param name="taskList">The list of tasks to process.</param>
        /// <param name="verbosity">The verbosity level for logging.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task ProcessTaskList(List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)> taskList, Verbosity verbosity, ILogger logger)
        {
            foreach (var (task, source) in taskList)
            {
                if (task == null)
                {
                    logger.LogMinimal(SourceSeparator);
                    System.Console.WriteLine($"Source: {source.Name}");
                    System.Console.WriteLine(PackageSeparator);
                    System.Console.WriteLine("Failed to obtain a search resource.");
                    logger.LogMinimal(PackageSeparator);
                    System.Console.WriteLine();
                    continue;
                }

                var results = await task;

                logger.LogMinimal(SourceSeparator);
                System.Console.WriteLine($"Source: {source.Name}"); // System.Console is used so that output is not suppressed by Verbosity.Quiet
                if (results.Any())
                {
                    if (verbosity == Verbosity.Quiet)
                    {
                        System.Console.WriteLine(PackageSeparator);
                    }
                    PrintResults(results, verbosity, logger);
                }
                else
                {
                    System.Console.WriteLine(PackageSeparator);
                    System.Console.WriteLine("No results found.");
                    logger.LogMinimal(PackageSeparator);
                    System.Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Retrieves a list of package sources based on provided sources or default configuration sources.
        /// </summary>
        /// <param name="sources">The list of package sources provided.</param>
        /// <param name="sourceProvider">The provider for package sources.</param>
        /// <returns>A list of package sources.</returns>
        private static IList<PackageSource> GetEndpointsAsync(List<string> sources, IPackageSourceProvider sourceProvider)
        {
            List<PackageSource> configurationSources = sourceProvider.LoadPackageSources()
                .Where(p => p.IsEnabled)
                .ToList();

            IList<PackageSource> packageSources;
            if (sources.Count > 0)
            {
                packageSources = sources
                    .Select(s => PackageSourceProviderExtensions.ResolveSource(configurationSources, s))
                    .ToList();
            }
            else
            {
                packageSources = configurationSources;
            }
            return packageSources;
        }

        /// <summary>
        /// Warns the user if the provided package sources use insecure HTTP connections.
        /// </summary>
        /// <param name="packageSources">The list of package sources to check.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        private static void WarnForHTTPSources(IList<PackageSource> packageSources, ILogger logger)
        {
            List<PackageSource> httpPackageSources = null;
            foreach (PackageSource packageSource in packageSources)
            {
                if (packageSource.IsHttp && !packageSource.IsHttps && !packageSource.AllowInsecureConnections)
                {
                    if (httpPackageSources == null)
                    {
                        httpPackageSources = new();
                    }
                    httpPackageSources.Add(packageSource);
                }
            }

            if (httpPackageSources != null && httpPackageSources.Count != 0)
            {
                if (httpPackageSources.Count == 1)
                {
                    logger.LogWarning(
                        string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage,
                        "search",
                        httpPackageSources[0]));
                }
                else
                {
                    logger.LogWarning(
                        string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage_MultipleSources,
                        "search",
                        Environment.NewLine + string.Join(Environment.NewLine, httpPackageSources.Select(e => e.Name))));
                }
            }
        }

        /// <summary>
        /// Prints the search results to the console based on the verbosity level.
        /// </summary>
        /// <param name="results">The package search results to print.</param>
        /// <param name="verbosity">The verbosity level for logging.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        private static void PrintResults(IEnumerable<IPackageSearchMetadata> results, Verbosity verbosity, ILogger logger)
        {
            string packageSeparator = new string('-', LineSeparatorLength);

            foreach (IPackageSearchMetadata result in results)
            {
                logger.LogMinimal(packageSeparator);

                CultureInfo culture = CultureInfo.CurrentCulture;

                StringBuilder content = new StringBuilder();
                content.Append($"> {result.Identity.Id} | {result.Identity.Version.ToNormalizedString()}"); // Basic info (Name | Version)

                if (verbosity != Verbosity.Quiet)
                {
                    if (result.DownloadCount != null)
                    {
                        string downloads = string.Format(culture, "{0:N}", result.DownloadCount);
                        content.Append($" | Downloads: {downloads.Substring(0, downloads.Length - 3)}");
                    }
                    else
                    {
                        content.Append(" | Downloads: N/A");
                    }
                }

                Console.WriteLine(content.ToString());

                if (verbosity != Verbosity.Quiet && result.Description != null)
                {
                    string description = result.Description;

                    if (verbosity == Verbosity.Normal && description.Length > 100)
                    {
                        description = description.Substring(0, 100) + "...";
                    }

                    logger.LogVerbose(description);
                }
            }

            logger.LogMinimal(packageSeparator);
            Console.WriteLine();
        }

    }
}
