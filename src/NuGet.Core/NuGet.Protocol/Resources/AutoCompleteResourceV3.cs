// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Model;
using NuGet.Protocol.Utility;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class AutoCompleteResourceV3 : AutoCompleteResource
    {
        private readonly RegistrationResourceV3 _regResource;
        private readonly ServiceIndexResourceV3 _serviceIndex;
        private readonly HttpSource _client;

        public AutoCompleteResourceV3(HttpSource client, ServiceIndexResourceV3 serviceIndex, RegistrationResourceV3 regResource)
            : base()
        {
            _regResource = regResource;
            _serviceIndex = serviceIndex;
            _client = client;
        }

        public override async Task<IEnumerable<string>> IdStartsWith(
            string packageIdPrefix,
            bool includePrerelease,
            Common.ILogger log,
            CancellationToken token)
        {
            var searchUrl = _serviceIndex.GetServiceEntryUri(ServiceTypes.SearchAutocompleteService);

            if (searchUrl == null)
            {
                throw new FatalProtocolException(Strings.Protocol_MissingSearchService);
            }

            // Construct the query
            var queryUrl = new UriBuilder(searchUrl.AbsoluteUri);
            var queryString =
                "q=" + WebUtility.UrlEncode(packageIdPrefix) +
                "&prerelease=" + includePrerelease.ToString(CultureInfo.CurrentCulture).ToLowerInvariant() +
                "&semVerLevel=2.0.0";

            queryUrl.Query = queryString;

            Common.ILogger logger = log ?? Common.NullLogger.Instance;

            var queryUri = queryUrl.Uri;
            AutoCompleteModel results = await _client.ProcessStreamAsync(
                new HttpSourceRequest(queryUri, logger),
                async stream =>
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    return await JsonSerializer.DeserializeAsync(stream, JsonContext.Default.AutoCompleteModel, token);
                },
                logger,
                token);

            token.ThrowIfCancellationRequested();

            if (results?.Data == null)
            {
                return Enumerable.Empty<string>();
            }

            return results.Data
                .Where(item => item != null && item.StartsWith(packageIdPrefix, StringComparison.OrdinalIgnoreCase));
        }

        public override async Task<IEnumerable<NuGetVersion>> VersionStartsWith(
            string packageId,
            string versionPrefix,
            bool includePrerelease,
            SourceCacheContext sourceCacheContext,
            Common.ILogger log,
            CancellationToken token)
        {
            Common.ILogger logger = log ?? Common.NullLogger.Instance;

            //*TODOs : Take prerelease as parameter. Also it should return both listed and unlisted for powershell ?
            var packages = await _regResource.GetPackageMetadata(packageId, includePrerelease, false, sourceCacheContext, logger, token);
            var versions = new List<NuGetVersion>();
            foreach (var package in packages)
            {
                var version = (string)package["version"];
                if (version.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    versions.Add(new NuGetVersion(version));
                }
            }
            return versions;
        }
    }
}
