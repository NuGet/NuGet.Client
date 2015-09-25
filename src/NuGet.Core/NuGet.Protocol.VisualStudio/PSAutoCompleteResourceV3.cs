// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Protocol.Core.v3.Data;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class PSAutoCompleteResourceV3 : PSAutoCompleteResource
    {
        private readonly RegistrationResourceV3 _regResource;
        private readonly ServiceIndexResourceV3 _serviceIndex;
        private readonly DataClient _client;

        public PSAutoCompleteResourceV3(DataClient client, ServiceIndexResourceV3 serviceIndex, RegistrationResourceV3 regResource)
            : base()
        {
            _regResource = regResource;
            _serviceIndex = serviceIndex;
            _client = client;
        }

        public override async Task<IEnumerable<string>> IdStartsWith(
            string packageIdPrefix,
            bool includePrerelease,
            CancellationToken token)
        {
            var searchUrl = _serviceIndex[ServiceTypes.SearchAutocompleteService].FirstOrDefault();

            if (searchUrl == null)
            {
                throw new NuGetProtocolException(Strings.Protocol_MissingSearchService);
            }

            // Construct the query
            var queryUrl = new UriBuilder(searchUrl.AbsoluteUri);
            var queryString =
                "q=" + packageIdPrefix + "&includePrerelease=" + includePrerelease;

            queryUrl.Query = queryString;

            var queryUri = queryUrl.Uri;
            var results = await _client.GetJObjectAsync(queryUri, token);
            token.ThrowIfCancellationRequested();
            if (results == null)
            {
                return Enumerable.Empty<string>();
            }
            var data = results.Value<JArray>("data");
            if (data == null)
            {
                return Enumerable.Empty<string>();
            }

            // Resolve all the objects
            var outputs = new List<string>();
            foreach (var result in data)
            {
                if (result != null)
                {
                    outputs.Add(result.ToString());
                }
            }

            return outputs.Where(item => item.StartsWith(packageIdPrefix, StringComparison.OrdinalIgnoreCase));
        }

        public override async Task<IEnumerable<NuGetVersion>> VersionStartsWith(
            string packageId,
            string versionPrefix,
            bool includePrerelease,
            CancellationToken token)
        {
            //*TODOs : Take prerelease as parameter. Also it should return both listed and unlisted for powershell ? 
            var packages = await _regResource.GetPackageMetadata(packageId, includePrerelease, false, token);
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
