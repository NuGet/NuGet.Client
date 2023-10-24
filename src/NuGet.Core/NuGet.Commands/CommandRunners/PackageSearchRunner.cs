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
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var listEndpoints = GetEndpointsAsync(sources, sourceProvider);
            WarnForHTTPSources(listEndpoints, logger);

            List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)> taskList;
            if (exactMatch)
            {
                taskList = await BuildTaskListForExactMatch(listEndpoints, searchTerm, prerelease, logger, cancellationToken);
            }
            else
            {
                taskList = await BuildTaskListForSearch(listEndpoints, searchTerm, prerelease, skip, take, logger, cancellationToken);
            }

            var result = new PackageSearchResult(
                taskList,
                logger,
                searchTerm,
                exactMatch);
            await result.PrintResultTablesAsync();
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
                    (resource.SearchAsync(searchTerm, new SearchFilter(includePrerelease: prerelease), skip, take, logger, cancellationToken), source)
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
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of tasks that perform exact match package search operations.</returns>
        private static async Task<List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>> BuildTaskListForExactMatch(
            IList<PackageSource> listEndpoints,
            string searchTerm,
            bool prerelease,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var taskList = new List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)>();

            foreach (var endpoint in listEndpoints)
            {
                using var cache = new SourceCacheContext();
                var repository = Repository.Factory.GetCoreV3(endpoint);
                var resource = await repository.GetResourceAsync<PackageMetadataResource>();
                taskList.Add(
                    resource == null ? (null, endpoint) :
                    (resource.GetMetadataAsync(searchTerm, includePrerelease: prerelease, includeUnlisted: false, cache, logger, cancellationToken), endpoint)
                );
            }

            return taskList;
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
