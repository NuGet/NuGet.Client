using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
            IEnumerable<JObject> jsonResults = await _rawSearch.Search(searchTerm, filters, skip, take, cancellationToken);

            return jsonResults.Select(e => Parse(e)).ToArray();
        }

        /// <summary>
        /// Parse a json package entry from search into a ApiAppSearchPackage
        /// </summary>
        private static ApiAppPackage Parse(JObject json)
        {
            string id = JsonHelpers.GetStringOrNull(json, "id");
            var version = JsonHelpers.GetVersionOrNull(json, "version");
            string pkgNs = JsonHelpers.GetStringOrNull(json, "namespace");

            ApiAppPackage package = new ApiAppPackage(pkgNs, id, version)
            {
                Authors = JsonHelpers.GetStringArray(json, "authors"),
                CatalogEntry = JsonHelpers.GetUriOrNull(json, "catalogEntry"),
                Description = JsonHelpers.GetStringOrNull(json, "description"),
                DownloadCount = 0, // TODO: populate this
                PackageContent = JsonHelpers.GetUriOrNull(json, "packageContent"),
                PackageTypes = JsonHelpers.GetStringOrNull(json, "@type").Split(' '),
                Registration = JsonHelpers.GetUriOrNull(json, "registration"),
                Summary = JsonHelpers.GetStringOrNull(json, "summary"),
                Tags = JsonHelpers.GetStringArray(json, "tags"),
                TenantId = JsonHelpers.GetGuidOrEmpty(json, "tenantId"),
                Title = JsonHelpers.GetStringOrNull(json, "title") ?? id,
                Visibility = JsonHelpers.GetStringOrNull(json, "visibility")
            };

            return package;
        }
    }
}