// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Commands.Internal;

namespace NuGet.Commands.CommandRunners
{
    public static class PackageSearchRunner
    {
        const int LineSeparatorLength = 40;
        static readonly string SourceSeparator = new string('=', LineSeparatorLength);

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
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task RunAsync(
            IPackageSourceProvider sourceProvider,
            List<string> sources, string searchTerm,
            int skip,
            int take,
            bool prerelease,
            bool exactMatch,
            ILogger logger)
        {
            CancellationToken cancellationToken = CancellationToken.None;

            var listEndpoints = GetEndpointsAsync(sources, sourceProvider);
            WarnForHTTPSources(listEndpoints, logger);
            if (exactMatch)
            {
                await GetExactMatch(listEndpoints, searchTerm, prerelease, logger);
                return;
            }

            var taskList = BuildTaskListForSearch(listEndpoints, searchTerm, prerelease, skip, take, logger, cancellationToken);
            await ProcessTaskList(await taskList, logger, searchTerm);
        }

        /// <summary>
        /// Searches for an exact match of the specified search term within the provided package sources.
        /// </summary>
        /// <param name="listEndpoints">The list of package sources/endpoints.</param>
        /// <param name="searchTerm">The term to search for within the package sources.</param>
        /// <param name="prerelease">A flag indicating whether to include prerelease packages in the search.</param>
        /// <param name="logger">The logger instance to use for logging.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task GetExactMatch(IList<PackageSource> listEndpoints, string searchTerm, bool prerelease, ILogger logger)
        {
            var taskList = BuildTaskListForExactMatch(listEndpoints, searchTerm, prerelease, logger);
            await ProcessTaskList(await taskList, logger, searchTerm, true);
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
        private static async Task<List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>> BuildTaskListForSearch(
            IList<PackageSource> listEndpoints,
            string searchTerm,
            bool prerelease,
            int skip,
            int take,
            ILogger logger,
            CancellationToken cancellationToken)
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
        private static async Task<List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>> BuildTaskListForExactMatch(
            IList<PackageSource> listEndpoints,
            string searchTerm,
            bool prerelease,
            ILogger logger)
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
        /// Processes a list of tasks that fetch package metadata and displays the results in a tabulated format.
        /// </summary>
        /// <param name="taskList">A list of tasks paired with their package sources. Each task is expected to return an IEnumerable of IPackageSearchMetadata.</param>
        /// <param name="logger">The logger used for logging messages during the process.</param>
        /// <param name="isExactMatch">A boolean flag to indicate if the processing is for an exact match. If set to true, only the first result from the list will be displayed. Default value is false.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// </remarks>
        private static async Task ProcessTaskList(List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)> taskList, ILogger logger, string searchTerm, bool isExactMatch = false)
        {
            foreach (var (task, source) in taskList)
            {
                if (task == null)
                {

                    logger.LogMinimal(SourceSeparator);
                    logger.LogMinimal($"Source: {source.Name}");
                    continue;
                }

                var results = await task;

                logger.LogMinimal(SourceSeparator);
                logger.LogMinimal($"Source: {source.Name}");
                var table = new PackageSearchResultTable("Package ID", "Latest Version", "Authors", "Downloads");

                if (isExactMatch)
                {
                    var firstResult = results.FirstOrDefault();
                    if (firstResult != null)
                    {
                        PopulateTableWithResults(new[] { firstResult }, table);
                    }
                }
                else
                {
                    PopulateTableWithResults(results, table);
                }

                table.PrintResult(searchTerm: searchTerm);
            }
        }

        /// <summary>
        /// Populates the given table with package metadata results.
        /// </summary>
        /// <param name="results">An enumerable of package search metadata to be processed and added to the table.</param>
        /// <param name="table">The table where the results will be added as rows.</param>
        private static void PopulateTableWithResults(IEnumerable<IPackageSearchMetadata> results, PackageSearchResultTable table)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;

            foreach (IPackageSearchMetadata result in results)
            {
                string packageId = result.Identity.Id;
                string version = result.Identity.Version.ToNormalizedString();
                string authors = result.Authors;
                string downloads = "N/A";

                if (result.DownloadCount != null)
                {
                    downloads = string.Format(culture, "{0:N}", result.DownloadCount);
                }

                table.AddRow(packageId, version, authors, downloads);
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
    }
}
