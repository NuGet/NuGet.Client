// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.ApiApps
{
    /// <summary>
    /// Parses NuGet V3 search json containing additional PS fields into package results.
    /// </summary>
    public class ApiAppSearchResource : INuGetResource
    {
        private readonly RawSearchResourceV3 _rawSearch;

        public ApiAppSearchResource(RawSearchResourceV3 rawSearch)
        {
            _rawSearch = rawSearch;
        }

        /// <summary>
        /// Retrieve search results
        /// </summary>
        public virtual async Task<IEnumerable<ApiAppPackage>> Search(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            var jsonResults = await _rawSearch.Search(searchTerm, filters, skip, take, cancellationToken);

            return jsonResults.Select(e => Parse(e)).ToArray();
        }

        /// <summary>
        /// Parse a json package entry from search into a ApiAppSearchPackage
        /// </summary>
        private static ApiAppPackage Parse(JObject jObject)
        {
            var id = JsonHelpers.GetStringOrNull(jObject, "id");
            var version = JsonHelpers.GetVersionOrNull(jObject, "version");
            var pkgNs = JsonHelpers.GetStringOrNull(jObject, "namespace");

            var package = new ApiAppPackage(pkgNs, id, version)
                {
                    Authors = JsonHelpers.GetStringArray(jObject, "authors"),
                    CatalogEntry = JsonHelpers.GetUriOrNull(jObject, "catalogEntry"),
                    Description = JsonHelpers.GetStringOrNull(jObject, "description"),
                    DownloadCount = 0, // TODO: populate this
                    PackageContent = JsonHelpers.GetUriOrNull(jObject, "packageContent"),
                    PackageTypes = JsonHelpers.GetStringOrNull(jObject, "@type").Split(' '),
                    Registration = JsonHelpers.GetUriOrNull(jObject, "registration"),
                    Summary = JsonHelpers.GetStringOrNull(jObject, "summary"),
                    Tags = JsonHelpers.GetStringArray(jObject, "tags"),
                    TenantId = JsonHelpers.GetGuidOrEmpty(jObject, "tenantId"),
                    Title = JsonHelpers.GetStringOrNull(jObject, "title") ?? id,
                    Visibility = JsonHelpers.GetStringOrNull(jObject, "visibility")
                };

            return package;
        }
    }
}
