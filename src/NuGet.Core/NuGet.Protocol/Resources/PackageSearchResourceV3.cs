// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public class PackageSearchResourceV3 : PackageSearchResource
    {
        private readonly RawSearchResourceV3 _rawSearchResource;
        // Maximum limit we try to read from http stream, 20MB. Maybe we can read this value from NugGet.Config file to override it.
        internal const int MaxBytesToRead = 21 * 1048576;
        

        public PackageSearchResourceV3(RawSearchResourceV3 searchResource)
            : base()
        {
            _rawSearchResource = searchResource;
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filter, int skip, int take, Common.ILogger log, CancellationToken cancellationToken)
        {
            var metadataCache = new MetadataReferenceCache();
            IEnumerable<PackageSearchMetadata> searchResultMetadata = new List<PackageSearchMetadata>();

            if (UseNew == 0)
            {
                var searchResultJsonObjects = await _rawSearchResource.Search(searchTerm, filter, skip, take, Common.NullLogger.Instance, cancellationToken);

                searchResultMetadata = searchResultJsonObjects
                    .Select(s => s.FromJToken<PackageSearchMetadata>());
            }
            else if (UseNew == 1)
            {
                searchResultMetadata = await _rawSearchResource.Search(
                    (httpSource, uri) => httpSource.ProcessStreamAsync(
                        new HttpSourceRequest(uri, Common.NullLogger.Instance),
                        s => ProgressStreamAsync(s, cancellationToken),
                        log,
                        cancellationToken),
                    searchTerm,
                    filter,
                    skip,
                    take,
                    Common.NullLogger.Instance,
                    cancellationToken);
            }
            else if (UseNew == 2)
            {
                searchResultMetadata = await _rawSearchResource.Search(
                        (httpSource, uri) => httpSource.ProcessHttpStreamAsync(
                            new HttpSourceRequest(uri, Common.NullLogger.Instance),
                            s => ProcessPartialHttpAsync(s, cancellationToken),
                            log,
                            cancellationToken),
                        searchTerm,
                        filter,
                        skip,
                        take,
                        Common.NullLogger.Instance,
                        cancellationToken);
            }
            else if (UseNew == 3)
            {
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
            }

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

        #region Inspired by joelverhagen
        private async Task<IEnumerable<PackageSearchMetadata>> ProgressStreamAsync(
            Stream stream,
            CancellationToken token)
        {
            using (var seekableStream = await stream.AsSeekableStreamAsync(token))
            using (var streamReader = new StreamReader(seekableStream))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                var response = JsonExtensions.JsonObjectSerializer.Deserialize<V3SearchResponse>(jsonTextReader);
                return response.Data ?? Enumerable.Empty<PackageSearchMetadata>();
            }
        }

        private class V3SearchResponse
        {
            [JsonProperty("data")]
            public List<PackageSearchMetadata> Data { get; set; }
        }
        #endregion

        #region Read partial Http stream then process it, maybe combine with zivkan solution.
        private async Task<IEnumerable<PackageSearchMetadata>> ProcessPartialHttpAsync(HttpResponseMessage httpInitialResponse, CancellationToken token)
        {
            if (httpInitialResponse == null)
            {
                return null;
            }

            var rawData = new List<byte>();
            var result = new List<PackageSearchMetadata>();
            var isMoreToRead = true;

            using (var stream = await httpInitialResponse.Content.ReadAsStreamAsync())
            {
                var totalRead = 0L;
                var buffer = new byte[262144]; // 2^18 = 256KB, it should be enough for 99.9% request. It'll go out this buffer nuget server is rogue or something is wrong on server side.

                do
                {
                    token.ThrowIfCancellationRequested();

                    var read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read == 0)
                    {
                        isMoreToRead = false;
                    }
                    else
                    {
                        var actualData = new byte[read];
                        buffer.ToList().CopyTo(0, actualData, 0, read);
                        rawData.AddRange(actualData);
                        totalRead += read;

                        // There some rogue server not honoring our parameters and returning everything which is more than 100MB data.
                        // We need to stop somepoint otherwise some regue server can return gigabytes data that always causes memory overflow or application starts not responding.
                        // Put hard stop at MaxBytesToRead since it can be done with under 100KB data (See MaxBytesToRead).
                        if (totalRead >= MaxBytesToRead)
                        {
                            break;
                        }
                    }
                } while (isMoreToRead);
            }

            if (!isMoreToRead && rawData.Any())
            {
                result.AddRange(await ProcessFullStreamData(rawData, token));
            }
            else if (isMoreToRead && rawData.Any())
            {
                result.AddRange(ProcessPartialStream(rawData));
            }

            return result;
        }


        private async Task<IEnumerable<PackageSearchMetadata>> ProcessFullStreamData(List<byte> rawData, CancellationToken token)
        {
            using (var stream = new MemoryStream(rawData.ToArray()))
            using (var seekableStream = await stream.AsSeekableStreamAsync(token))
            using (var streamReader = new StreamReader(seekableStream))
            using (var jsonTextReader = new JsonTextReader(streamReader))
            {
                var response = JsonExtensions.JsonObjectSerializer.Deserialize<V3SearchResponse>(jsonTextReader);
                return response.Data ?? Enumerable.Empty<PackageSearchMetadata>();
            }
        }

        private IEnumerable<PackageSearchMetadata> ProcessPartialStream(List<byte> rawData)
        {
            if (rawData.Count < MaxBytesToRead)
            {
                throw new ArgumentException($"rawData should be more than {MaxBytesToRead}. This method is only designed for huge Http stream read (See MaxBytesToRead).");
            }

            string jsonStr = Encoding.UTF8.GetString(rawData.ToArray());

            var lastValidTag = jsonStr.LastIndexOf("\"}]},{\"@id\":\"", StringComparison.Ordinal);

            // There should be at least one valid closing tag before our cut off due to MaxBytesToRead limit since json string for at least MaxBytesToRead data.
            if (lastValidTag < 0)
            {
                throw new InvalidDataException($"Not proper json, close tag not found");
            }

            // Properly trim by last good nuget package.
            jsonStr = jsonStr.Substring(0, lastValidTag);
            //Add back proper closing tag.
            jsonStr += "\"}]}]}";

            using (var jsonTextReader = new JsonTextReader(new StringReader(jsonStr)))
            { 
                var response = JsonExtensions.JsonObjectSerializer.Deserialize<V3SearchResponse>(jsonTextReader);
                return response.Data ?? Enumerable.Empty<PackageSearchMetadata>();
            }
        }
        #endregion

        #region Inspired by zivkan
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
        #endregion
    }
}
