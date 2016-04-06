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
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class AutoCompleteResourceV2Feed : AutoCompleteResource
    {
        private readonly HttpSource _httpSource;
        private readonly Uri _baseUri;

        public AutoCompleteResourceV2Feed(HttpSourceResource httpSourceResource, string baseAddress, Configuration.PackageSource packageSource)
        {
            if (httpSourceResource == null)
            {
                throw new ArgumentNullException(nameof(httpSourceResource));
            }

            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            _httpSource = httpSourceResource.HttpSource;

            _baseUri = UriUtility.CreateSourceUri($"{baseAddress}/");
        }

        public override async Task<IEnumerable<string>> IdStartsWith(
            string packageIdPrefix,
            bool includePrerelease,
            Common.ILogger log,
            CancellationToken token)
        {
            var apiEndpointUri = new UriBuilder(new Uri(_baseUri, @"package-ids"))
            {
                Query = $"partialId={packageIdPrefix}&includePrerelease={includePrerelease.ToString()}"
            };

            return await GetResults(apiEndpointUri.Uri, log, token);
        }

        public override async Task<IEnumerable<NuGetVersion>> VersionStartsWith(
            string packageId,
            string versionPrefix,
            bool includePrerelease,
            Common.ILogger log,
            CancellationToken token)
        {
            var apiEndpointUri = new UriBuilder(new Uri(_baseUri, @"package-versions/" + packageId))
            {
                Query = $"includePrerelease={includePrerelease.ToString()}"
            };

            var results = await GetResults(apiEndpointUri.Uri, log, token);
            var versions = results.ToList();
            versions = versions.Where(item => item.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return versions.Select(item => NuGetVersion.Parse(item));
        }

        private async Task<IEnumerable<string>> GetResults(
            Uri apiEndpointUri,
            Common.ILogger logger,
            CancellationToken token)
        {
            return await _httpSource.ProcessStreamAsync(
                   uri: apiEndpointUri,
                   ignoreNotFounds: false,
                   processAsync: stream =>
                   {
                       using (var reader = new StreamReader(stream))
                       using (var jsonReader = new JsonTextReader(reader))
                       {
                           var serializer = JsonSerializer.Create();
                           var json = serializer.Deserialize<string[]>(jsonReader);
                           return Task.FromResult(json);
                       }
                   },
                   log: logger,
                   token: token);
        }
    }
}
