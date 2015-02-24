using Newtonsoft.Json.Linq;
using NuGet.Data;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Registration blob reader
    /// </summary>
    public class V3RegistrationResource : INuGetResource
    {
        // cache all json retrieved in this resource, the resource *should* be thrown away after the operation is done
        private readonly ConcurrentDictionary<Uri, JObject> _cache;

        private readonly ResourceSelector _resourceSelector;
        private readonly HttpClient _client;
        private readonly IEnumerable<Uri> _packageDisplayMetadataUriTemplates;
        private readonly IEnumerable<Uri> _packageVersionDisplayMetadataUriTemplates;

        private static readonly VersionRange AllVersions = new VersionRange(null, true, null, true, true);

        public V3RegistrationResource(ResourceSelector resourceSelector, HttpClient client, IEnumerable<Uri> packageDisplayMetadataUriTemplates, IEnumerable<Uri> packageVersionDisplayMetadataUriTemplates)
        {
            if (resourceSelector == null)
            {
                throw new ArgumentNullException("resourceSelector");
            }

            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (packageDisplayMetadataUriTemplates == null || !packageDisplayMetadataUriTemplates.Any())
            {
                throw new ArgumentNullException("packageDisplayMetadataUriTemplates");
            }

            if (packageVersionDisplayMetadataUriTemplates == null || !packageVersionDisplayMetadataUriTemplates.Any())
            {
                throw new ArgumentNullException("packageVersionDisplayMetadataUriTemplates");
            }

            _resourceSelector = resourceSelector;
            _client = client;
            _packageDisplayMetadataUriTemplates = packageDisplayMetadataUriTemplates;
            _packageVersionDisplayMetadataUriTemplates = packageVersionDisplayMetadataUriTemplates;
            _cache = new ConcurrentDictionary<Uri, JObject>();
        }

        /// <summary>
        /// Constructs the URI of a registration index blob
        /// </summary>
        /// <param name="packageId">The package id (natural casing)</param>
        /// <param name="cancellationToken">The cancellation token to terminate HTTP requests</param>
        /// <returns>The first URL available from the resource, with the URI template applied.</returns>
        public virtual async Task<Uri> GetUriAsync(string packageId, CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new InvalidOperationException();
            }

            var selectedResource = await _resourceSelector.DetermineResourceUrlAsync(
                    Utility.ApplyPackageIdToUriTemplate(_packageDisplayMetadataUriTemplates, packageId), cancellationToken);
            if (selectedResource == null)
            {
                selectedResource = Utility.ApplyPackageIdToUriTemplate(_packageDisplayMetadataUriTemplates.First(), packageId);
            }
            return selectedResource;
        }

        /// <summary>
        /// Constructs the URI of a registration blob with a specific version
        /// </summary>
        public virtual async Task<Uri> GetUriAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            if (String.IsNullOrEmpty(id) || version == null)
            {
                throw new InvalidOperationException();
            }

            return await GetUriAsync(new PackageIdentity(id, version), cancellationToken);
        }

        /// <summary>
        /// Constructs the URI of a registration blob with a specific version
        /// </summary>
        public virtual async Task<Uri> GetUriAsync(PackageIdentity package, CancellationToken cancellationToken)
        {
            if (package == null || package.Id == null || package.Version == null)
            {
                throw new InvalidOperationException();
            }

            var selectedResource = await _resourceSelector.DetermineResourceUrlAsync(
                    Utility.ApplyPackageIdVersionToUriTemplate(_packageVersionDisplayMetadataUriTemplates, package.Id, package.Version), cancellationToken);
            if (selectedResource == null)
            {
                selectedResource = Utility.ApplyPackageIdVersionToUriTemplate(_packageVersionDisplayMetadataUriTemplates.First(), package.Id, package.Version);
            }
            return selectedResource;
        }

        /// <summary>
        /// Returns the registration blob for the id and version
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<JObject> GetPackageMetadata(PackageIdentity identity, CancellationToken token)
        {
            return (await GetPackageMetadata(identity.Id, new VersionRange(identity.Version, true, identity.Version, true), true, true, token)).SingleOrDefault();
        }

        /// <summary>
        /// Returns inlined catalog entry items for each registration blob
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<IEnumerable<JObject>> GetPackageMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            return await GetPackageMetadata(packageId, AllVersions, includePrerelease, includeUnlisted, token);
        }

        /// <summary>
        /// Returns inlined catalog entry items for each registration blob
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<IEnumerable<JObject>> GetPackageMetadata(string packageId, VersionRange range, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            List<JObject> results = new List<JObject>();

            var entries = await GetPackageEntries(packageId, includeUnlisted, token);

            foreach (var entry in entries)
            {
                JToken catalogEntry = entry["catalogEntry"];

                if (catalogEntry != null)
                {
                    NuGetVersion version;

                    if (catalogEntry["version"] != null && NuGetVersion.TryParse(catalogEntry["version"].ToString(), out version))
                    {
                        if (range.Satisfies(version) && (includePrerelease || !version.IsPrerelease))
                        {
                            if (catalogEntry["published"] != null)
                            {
                                DateTime published = catalogEntry["published"].ToObject<DateTime>();

                                if (published.Year > 1901 || includeUnlisted)
                                {
                                    // add in the download url
                                    if (entry["packageContent"] != null)
                                    {
                                        catalogEntry["packageContent"] = entry["packageContent"];
                                    }

                                    results.Add(entry["catalogEntry"] as JObject);
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns catalog:CatalogPage items
        /// </summary>
        public virtual async Task<IEnumerable<JObject>> GetPages(string packageId, CancellationToken token)
        {
            List<JObject> results = new List<JObject>();

            JObject indexJson = await GetIndex(packageId, token);

            var items = indexJson["items"] as JArray;

            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item["@type"] != null && StringComparer.Ordinal.Equals(item["@type"].ToString(), "catalog:CatalogPage"))
                    {
                        if (item["items"] != null)
                        {
                            // normal inline page
                            results.Add(item as JObject);
                        }
                        else
                        {
                            // fetch the page
                            string url = item["@id"].ToString();

                            JObject catalogPage = await GetJson(new Uri(url), token);

                            results.Add(catalogPage);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all index entries of type Package within the given range and filters
        /// </summary>
        public virtual async Task<IEnumerable<JObject>> GetPackageEntries(string packageId, bool includeUnlisted, CancellationToken token)
        {
            List<JObject> results = new List<JObject>();

            var pages = await GetPages(packageId, token);

            foreach (JObject catalogPage in pages)
            {
                JArray array = catalogPage["items"] as JArray;

                if (array != null)
                {
                    foreach (JToken item in array)
                    {
                        if (item["@type"] != null && StringComparer.Ordinal.Equals(item["@type"].ToString(), "Package"))
                        {
                            // TODO: listed check
                            results.Add(item as JObject);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns the index.json registration page for a package.
        /// </summary>
        public virtual async Task<JObject> GetIndex(string packageId, CancellationToken cancellationToken)
        {
            Uri uri = await GetUriAsync(packageId, cancellationToken);

            return await GetJson(uri, cancellationToken);
        }

        /// <summary>
        /// Retrieve and cache json safely
        /// </summary>
        protected virtual async Task<JObject> GetJson(Uri uri, CancellationToken token)
        {
            JObject json = null;
            if (uri != null && !_cache.TryGetValue(uri, out json))
            {
                var response = await _client.GetAsync(uri, token);

                // ignore missing blobs
                if (response.IsSuccessStatusCode)
                {
                    // throw on bad files
                    json = JObject.Parse(await response.Content.ReadAsStringAsync());
                }
                else
                {
                    // cache an empty object so we don't continually retry
                    json = new JObject();
                }

                _cache.TryAdd(uri, json);
            }

            return json;
        }
    }
}
