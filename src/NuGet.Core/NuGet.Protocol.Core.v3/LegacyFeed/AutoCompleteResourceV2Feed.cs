// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class AutoCompleteResourceV2Feed : AutoCompleteResource
    {
        private readonly HttpSource _httpSource;
        private readonly Uri _baseUri;

        public AutoCompleteResourceV2Feed(HttpSourceResource httpSourceResource, Configuration.PackageSource packageSource)
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

            var withoutTrailingSlash = packageSource.Source.TrimEnd('/');

            _baseUri = new Uri($"{withoutTrailingSlash}/");
        }

        public override async Task<IEnumerable<string>> IdStartsWith(
            string packageIdPrefix,
            bool includePrerelease,
            Logging.ILogger log,
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
            Logging.ILogger log,
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
            Logging.ILogger logger,
            CancellationToken token)
        {
            using (var httpResponseMessage = await _httpSource.GetAsync(apiEndpointUri, logger, token))
            {
                httpResponseMessage.EnsureSuccessStatusCode();

                var json = await httpResponseMessage.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<string[]>(json);
            }
        }
    }
}
