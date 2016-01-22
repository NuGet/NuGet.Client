// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    public class SearchLatestResourceV3 : SearchLatestResource
    {
        private readonly RawSearchResourceV3 _searchResource;

        public SearchLatestResourceV3(RawSearchResourceV3 searchResource)
            : base()
        {
            _searchResource = searchResource;
        }

        public override async Task<IEnumerable<ServerPackageMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            var results = new List<ServerPackageMetadata>();

            var searchResultJsonObjects = await _searchResource.Search(searchTerm, filters, skip, take, cancellationToken);

            foreach (var package in searchResultJsonObjects)
            {
                // TODO: verify this parsing is needed
                var id = package.Value<string>(Properties.PackageId);
                var version = NuGetVersion.Parse(package.Value<string>(Properties.Version));
                var topPackage = new PackageIdentity(id, version);
                var iconUrl = GetUri(package, Properties.IconUrl);
                var summary = package.Value<string>(Properties.Summary);

                if (string.IsNullOrWhiteSpace(summary))
                {
                    // summary is empty. Use its description instead.
                    summary = package.Value<string>(Properties.Description);
                }

                // retrieve metadata for the top package
                results.Add(PackageMetadataParser.ParseMetadata(package));
            }

            return results;
        }

        private Uri GetUri(JObject json, string property)
        {
            if (json[property] == null)
            {
                return null;
            }
            var str = json[property].ToString();
            if (String.IsNullOrEmpty(str))
            {
                return null;
            }
            return new Uri(str);
        }
    }
}
