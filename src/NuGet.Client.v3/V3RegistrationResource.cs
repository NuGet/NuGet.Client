using Newtonsoft.Json.Linq;
using NuGet.Data;
using NuGet.PackagingCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public class V3RegistrationResource : INuGetResource
    {
        // cache all json retrieved in this resource, the resource *should* be thrown away after the operation is done
        private readonly ConcurrentDictionary<Uri, JObject> _cache;

        private readonly DataClient _client;
        private readonly Uri _baseUrl;

        public V3RegistrationResource(DataClient client, Uri baseUrl)
        {
            _client = client;
            _baseUrl = baseUrl;
            _cache = new ConcurrentDictionary<Uri, JObject>();
        }

        public virtual Uri GetUri(string packageId)
        {
            return new Uri(_baseUrl.AbsoluteUri.TrimEnd('/') + "/" + packageId.ToLowerInvariant() + "/index.json");
        }

        public virtual Uri GetUri(PackageIdentity package)
        {
            throw new NotImplementedException();
        }

        public virtual async Task<JObject> GetPackage(PackageIdentity identity, CancellationToken token)
        {
            var allVersions = await Get(identity.Id, true, true, token);

            return allVersions.FirstOrDefault(p => String.Equals(p["version"].ToString(), identity.Version.ToNormalizedString(), StringComparison.OrdinalIgnoreCase));
        }

        public virtual async Task<IEnumerable<JObject>> Get(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            // TODO: use filters

            Uri uri = GetUri(packageId);

            JObject regJson = null;
            if (!_cache.TryGetValue(uri, out regJson))
            {
                regJson = await _client.GetJObjectAsync(uri);

                _cache.TryAdd(uri, regJson);
            }

            // Descend through the items to find all the versions
            return await Descend((JArray)regJson["items"], token);
        }

        private async Task<IEnumerable<JObject>> Descend(JArray json, CancellationToken token)
        {
            List<IEnumerable<JObject>> lists = new List<IEnumerable<JObject>>();
            List<JObject> items = new List<JObject>();
            lists.Add(items);
            foreach (var item in json)
            {
                string type = item["@type"].ToString();
                if (Equals(type, "catalog:CatalogPage"))
                {
                    lists.Add(await Descend((JArray)item["items"], token));
                }
                else if (Equals(type, "Package"))
                {
                    // inlined - this will go away soon
                    if (item["catalogEntry"] != null)
                    {
                        JObject entry = (JObject)item["catalogEntry"];

                        // add in the download url
                        entry["packageContent"] = item["packageContent"];

                        items.Add(entry);
                    }
                    else
                    {
                        Uri catalogUri = new Uri(item["catalogEntry"]["@id"].ToString());

                        JObject catalogPage = null;
                        if (!_cache.TryGetValue(catalogUri, out catalogPage))
                        {
                            catalogPage = await _client.GetJObjectAsync(catalogUri, token);

                            // add in the download url
                            catalogPage["packageContent"] = item["packageContent"];

                            _cache.TryAdd(catalogUri, catalogPage);
                        }

                        items.Add((JObject)catalogPage);
                    }
                }
            }
            // Flatten the list and return it
            IEnumerable<JObject> flattenedObject = lists.SelectMany(t => t).ToList();
            return flattenedObject;
        }
    }
}
