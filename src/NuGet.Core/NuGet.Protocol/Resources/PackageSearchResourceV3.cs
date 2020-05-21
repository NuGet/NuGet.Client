// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public class PackageSearchResourceV3 : PackageSearchResource
    {
        private readonly RawSearchResourceV3 _rawSearchResource;

        public PackageSearchResourceV3(RawSearchResourceV3 searchResource)
            : base()
        {
            _rawSearchResource = searchResource;
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filter, int skip, int take, Common.ILogger log, CancellationToken cancellationToken)
        {
            var metadataCache = new MetadataReferenceCache();
            IEnumerable<PackageSearchMetadata> searchResultMetadata = new List<PackageSearchMetadata>();

            searchResultMetadata = await _rawSearchResource.Search(
                    (httpSource, uri) => httpSource.ProcessHttpStreamAsync(
                        new HttpSourceRequest(uri, Common.NullLogger.Instance),
                        s => ProcessHttpStreamIncrementallyAsync(s, take, cancellationToken),
                        log,
                        cancellationToken),
                    searchTerm,
                    filter,
                    skip,
                    take,
                    Common.NullLogger.Instance,
                    cancellationToken);

            var searchResults = searchResultMetadata
                .Select(m => m.WithVersions(() => GetVersions(m, filter)))
                .Select(m => metadataCache.GetObject((PackageSearchMetadataBuilder.ClonedPackageSearchMetadata)m))
                .ToArray();

            return searchResults;

        }

        private static IEnumerable<VersionInfo> GetVersions(PackageSearchMetadata metadata, SearchFilter filter)
        {
            var versions = metadata.ParsedVersions;

            // TODO: in v2, we only have download count for all versions, not per version.
            // To be consistent, in v3, we also use total download count for now.
            var totalDownloadCount = versions.Select(v => v.DownloadCount).Sum();
            versions = versions
                .Select(v => v.Version)
                .Where(v => filter.IncludePrerelease || !v.IsPrerelease)
                .Concat(new[] { metadata.Version })
                .Distinct()
                .Select(v => new VersionInfo(v, totalDownloadCount))
                .ToArray();

            return versions;
        }

        private async Task<IEnumerable<PackageSearchMetadata>> ProcessHttpStreamIncrementallyAsync(HttpResponseMessage httpInitialResponse, int take, CancellationToken token)
        {
            return (await ProcessHttpStreamWithoutBufferAsync(httpInitialResponse, take, token)).Data;
        }

        private async Task<V3SearchResults> ProcessHttpStreamWithoutBufferAsync(HttpResponseMessage httpInitialResponse, int take, CancellationToken token)
        {
            if (httpInitialResponse == null)
            {
                return null;
            }

            var _newtonsoftConvertersSerializer = new JsonSerializer();
            _newtonsoftConvertersSerializer.Converters.Add(new Converters.V3SearchResultsConverter(take));
            
            using (var stream = await httpInitialResponse.Content.ReadAsStreamAsync())
            using (var streamReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return _newtonsoftConvertersSerializer.Deserialize<V3SearchResults>(jsonReader);
            }
        }
    }
}
