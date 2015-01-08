using Newtonsoft.Json.Linq;
using NuGet.Data;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public class V3RegistrationResource : INuGetResource
    {
        private readonly DataClient _client;
        private readonly Uri _baseUrl;

        public V3RegistrationResource(DataClient client, Uri baseUrl)
        {
            _client = client;
            _baseUrl = baseUrl;
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

            var catalogPackage = await _client.GetJObjectAsync(uri);

            if (catalogPackage["HttpStatusCode"] != null)
            {
                // Got an error response from the data client, so just return an empty array
                return Enumerable.Empty<JObject>();
            }
            // Descend through the items to find all the versions
            return await Descend((JArray)catalogPackage["items"], token);
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
                    var resolved = await _client.GetJObjectAsync(new Uri(item["catalogEntry"]["@id"].ToString()), token);
                    resolved["packageContent"] = item["packageContent"];
                    items.Add((JObject)resolved);
                }
            }
            // Flatten the list and return it
            IEnumerable<JObject> flattenedObject = lists.SelectMany(t => t).ToList();
            return flattenedObject;
        }

    }
}
