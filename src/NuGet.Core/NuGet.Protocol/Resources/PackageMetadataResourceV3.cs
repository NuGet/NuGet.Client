// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Extensions;
using NuGet.Protocol.Model;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class PackageMetadataResourceV3 : PackageMetadataResource
    {
        private readonly RegistrationResourceV3 _regResource;
        private readonly ReportAbuseResourceV3 _reportAbuseResource;
        private readonly PackageDetailsUriResourceV3 _packageDetailsUriResource;
        private readonly HttpSource _client;

        public PackageMetadataResourceV3(
            HttpSource client,
            RegistrationResourceV3 regResource,
            ReportAbuseResourceV3 reportAbuseResource,
            PackageDetailsUriResourceV3 packageDetailsUriResource)
        {
            _regResource = regResource;
            _client = client;
            _reportAbuseResource = reportAbuseResource;
            _packageDetailsUriResource = packageDetailsUriResource;
        }

        /// <summary>
        /// Query nuget package list from nuget server for Consolidate UI. This implementation optimized for performance so instead of keeping giant JObject in memory we use strong types.
        /// </summary>
        /// <param name="packageId">PackageId for package we're looking.</param>
        /// <param name="includePrerelease">Whether to include PreRelease versions into result.</param>
        /// <param name="includeUnlisted">Whether to include Unlisted versions into result.</param>
        /// <param name="sourceCacheContext">SourceCacheContext for cache.</param>
        /// <param name="log">Logger Instance.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of package meta data.</returns>
        public override async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            SourceCacheContext sourceCacheContext,
            Common.ILogger log,
            CancellationToken token)
        {
            var range = VersionRange.All; // This value was already preset for Package Manager UI.

            return await GetMetadata(packageId, includePrerelease, includeUnlisted, range, sourceCacheContext, log, token);
        }

        /// <summary>
        /// Returns the registration blob for the id and version
        /// </summary>
        /// <param name="package"></param>
        /// <param name="sourceCacheContext"></param>
        /// <param name="log"></param>
        /// <param name="token"></param>
        /// <returns>Package meta data.</returns>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public override async Task<IPackageSearchMetadata> GetMetadataAsync(
            PackageIdentity package,
            SourceCacheContext sourceCacheContext,
            Common.ILogger log,
            CancellationToken token)
        {
            var includeMinVersion = true;
            var includeMaxVersion = true;

            var range = new VersionRange(package.Version, includeMinVersion, package.Version, includeMaxVersion);

            var includePrerelease = true;
            var includeUnlisted = true;

            return (await GetMetadata(package.Id, includePrerelease, includeUnlisted, range, sourceCacheContext, log, token)).SingleOrDefault();
        }

        private async Task<IEnumerable<IPackageSearchMetadata>> GetMetadata(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            VersionRange range,
            SourceCacheContext sourceCacheContext,
            ILogger log,
            CancellationToken token)
        {
            var metadataCache = new MetadataReferenceCache();
            var registrationUri = _regResource.GetUri(packageId);

            var registrationIndexResult = await LoadRangesAsync(
                _client,
                registrationUri,
                packageId,
                sourceCacheContext,
                async httpSourceResult => await ProcessRegistrationIndexAsync<RegistrationIndex>(httpSourceResult, token),
                log,
                token);

            var registrationIndex = registrationIndexResult.Index;
            var httpSourceCacheContext = registrationIndexResult.CacheContext;

            if (registrationIndex == null)
            {
                // The server returned a 404, the package does not exist
                return Enumerable.Empty<PackageSearchMetadataRegistration>();
            }

            var results = new List<PackageSearchMetadataRegistration>();

            foreach (var registrationPage in registrationIndex.Items)
            {
                if(registrationPage==null)
                {
                    throw new InvalidDataException(registrationUri.AbsoluteUri);
                }

                var lower = NuGetVersion.Parse(registrationPage.Lower);
                var upper = NuGetVersion.Parse(registrationPage.Upper);

                if (range.IsItemRangeRequired(lower, upper))
                {
                    if (registrationPage.Items == null)
                    {
                        var rangeUri = registrationPage.Url;
                        var leafRegistrationPage = await GetRegistratioIndexPageAsync(_client, rangeUri, packageId, lower, upper, httpSourceCacheContext, log, token);

                        if(registrationPage==null)
                        {
                            throw new InvalidDataException(registrationUri.AbsoluteUri);
                        }

                        ProcessRegistrationPage(leafRegistrationPage, results, range, includePrerelease,includeUnlisted, metadataCache);
                    }
                    else
                    {
                        ProcessRegistrationPage(registrationPage, results, range, includePrerelease, includeUnlisted, metadataCache);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Process RegistrationIndex and return list of RegistrationPages.
        /// </summary>
        /// <typeparam name="T">Generic type</typeparam>
        /// <param name="httpSourceResult">HttpSourceResult type object which comes from HttpSource, used for get stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns></returns>
        private async Task<T> ProcessRegistrationIndexAsync<T>(HttpSourceResult httpSourceResult, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var jsonSerializer = JsonSerializer.Create(JsonExtensions.ObjectSerializationSettings);

            using (var streamReader = new StreamReader(httpSourceResult.Stream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var registrationIndex = jsonSerializer
                    .Deserialize<T>(jsonReader);

                return await Task.FromResult(registrationIndex);
            }
        }

        /// <summary>
        /// Query RegistrationIndex from nuget server for Package Manager UI. This implementation optimized for performance so instead of keeping giant JObject in memory we use strong types.
        /// </summary>
        /// <param name="httpSource">Httpsource instance</param>
        /// <param name="registrationUri">Package registration url</param>
        /// <param name="packageId">PackageId for package we're looking.</param>
        /// <param name="cacheContext">CacheContext for cache.</param>
        /// <param name="processAsync">Func expression used for HttpSource.cs</param>
        /// <param name="log">Logger Instance.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns></returns>
        private async Task<RegistrationIndexResult> LoadRangesAsync(
            HttpSource httpSource,
            Uri registrationUri,
            string packageId,
            SourceCacheContext cacheContext,
            Func<HttpSourceResult, Task<RegistrationIndex>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            var packageIdLowerCase = packageId.ToLowerInvariant();
            var retryCount = 0;

            var httpSourceCacheContext = HttpSourceCacheContext.Create(cacheContext, retryCount);

            var index = await httpSource.GetAsync(
                new HttpSourceCachedRequest(
                    registrationUri.OriginalString,
                    $"list_{packageIdLowerCase}_index",
                    httpSourceCacheContext)
                {
                    IgnoreNotFounds = true,
                },
                async httpSourceResult => await processAsync(httpSourceResult),
                log,
                token);

            return new RegistrationIndexResult
            {
                Index = index,
                CacheContext = httpSourceCacheContext
            };
        }

        /// <summary>
        /// Process RegistrationIndex 
        /// </summary>
        /// <param name="httpSource">Httpsource instance.</param>
        /// <param name="rangeUri">Paged registration index url address.</param>
        /// <param name="packageId">PackageId for package we're checking.</param>
        /// <param name="lower">Lower bound of nuget package.</param>
        /// <param name="upper">Upper bound of nuget package.</param>
        /// <param name="httpSourceCacheContext">SourceCacheContext for cache.</param>
        /// <param name="log">Logger Instance.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns></returns>
        private async Task<RegistrationPage> GetRegistratioIndexPageAsync(
            HttpSource httpSource,
            string rangeUri,
            string packageId,
            NuGetVersion lower,
            NuGetVersion upper,
            HttpSourceCacheContext httpSourceCacheContext,
            ILogger log,
            CancellationToken token)
        {
            var packageIdLowerCase = packageId.ToLowerInvariant();
            var registrationPage = await httpSource.GetAsync(
                            new HttpSourceCachedRequest(
                                rangeUri,
                                $"list_{packageIdLowerCase}_range_{lower.ToNormalizedString()}-{upper.ToNormalizedString()}",
                                httpSourceCacheContext)
                            {
                                IgnoreNotFounds = true,
                            },
                            async httpSourceResult =>
                            {
                                return await ProcessRegistrationIndexAsync<RegistrationPage>(httpSourceResult, token);
                            },
                            log,
                            token);

            return registrationPage;
        }

        /// <summary>
        /// Process RegistrationPage
        /// </summary>
        /// <param name="registrationPage">Nuget registration page.</param>
        /// <param name="results">Used to return nuget result.</param>
        /// <param name="range">Nuget version range.</param>
        /// <param name="includePrerelease">Whether to include PreRelease versions into result.</param>
        /// <param name="includeUnlisted">Whether to include Unlisted versions into result.</param>
        private void ProcessRegistrationPage(
            RegistrationPage registrationPage,
            List<PackageSearchMetadataRegistration> results,
            VersionRange range, bool includePrerelease,
            bool includeUnlisted,
            MetadataReferenceCache metadataCache)
        {
            foreach (RegistrationLeafItem registrationLeaf in registrationPage.Items)
            {
                PackageSearchMetadataRegistration catalogEntry = registrationLeaf.CatalogEntry;
                NuGetVersion version = catalogEntry.Version;
                bool listed = catalogEntry.IsListed;

                if (range.Satisfies(catalogEntry.Version)
                    && (includePrerelease || !version.IsPrerelease)
                    && (includeUnlisted || listed))
                {
                    catalogEntry.ReportAbuseUrl = _reportAbuseResource?.GetReportAbuseUrl(catalogEntry.PackageId, catalogEntry.Version);
                    catalogEntry.PackageDetailsUrl = _packageDetailsUriResource?.GetUri(catalogEntry.PackageId, catalogEntry.Version);
                    metadataCache.GetObject(catalogEntry);
                    results.Add(catalogEntry);
                }
            }
        }
    }
}
