// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Simple search results for V3
    /// </summary>
    public class SimpleSearchResourceV3 : SimpleSearchResource
    {
        private readonly RawSearchResourceV3 _rawSearch;

        public SimpleSearchResourceV3(RawSearchResourceV3 rawSearch)
        {
            if (rawSearch == null)
            {
                throw new ArgumentNullException("rawSearch");
            }

            _rawSearch = rawSearch;
        }

        /// <summary>
        /// Basic search
        /// </summary>
        public override async Task<IEnumerable<SimpleSearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            var results = new List<SimpleSearchMetadata>();

            foreach (var result in await _rawSearch.Search(searchTerm, filters, skip, take, cancellationToken))
            {
                var version = NuGetVersion.Parse(result["version"].ToString());
                var identity = new PackageIdentity(result["id"].ToString(), version);

                var description = result["description"].ToString();

                var allVersions = new List<NuGetVersion>();

                foreach (var versionObj in ((JArray)result["versions"]))
                {
                    allVersions.Add(NuGetVersion.Parse(versionObj["version"].ToString()));
                }

                var data = new SimpleSearchMetadata(identity, description, allVersions);

                results.Add(data);
            }

            return results;
        }
    }
}
