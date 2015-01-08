using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public class V3MetadataResource : MetadataResource
    {
        private V3RegistrationResource _regResource;
        private HttpClient _client;

        public V3MetadataResource(HttpClient client, V3RegistrationResource regResource)
            : base()
        {
            _regResource = regResource;
            _client = client;
        }

        public override async Task<IEnumerable<KeyValuePair<string, NuGetVersion>>> GetLatestVersions(IEnumerable<string> packageIds, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            List<KeyValuePair<string, NuGetVersion>> results = new List<KeyValuePair<string, NuGetVersion>>();

            // TODO: avoid getting the same blob twice
            // TODO: run in parallel
            foreach (var id in packageIds)
            {
                var allVersions = await _regResource.Get(id, includePrerelease, includeUnlisted, token);
                var latest = allVersions.Select(p => NuGetVersion.Parse(p["version"].ToString())).OrderByDescending(p => p, VersionComparer.VersionRelease).FirstOrDefault();

                results.Add(new KeyValuePair<string, NuGetVersion>(id, latest));
            }

            return results;
        }

        public override async Task<IEnumerable<KeyValuePair<string, bool>>> ArePackagesSatellite(IEnumerable<string> packageId, CancellationToken token)
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }
    }
}
