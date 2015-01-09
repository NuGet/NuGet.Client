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
    /// <summary>
    /// Returns the full package metadata
    /// </summary>
    public class V3MetadataResource : MetadataResource
    {
        private static readonly DateTimeOffset Unpublished = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8));
        private V3RegistrationResource _regResource;
        private HttpClient _client;

        public V3MetadataResource(HttpClient client, V3RegistrationResource regResource)
            : base()
        {
            _regResource = regResource;
            _client = client;
        }

        /// <summary>
        /// Find the latest version of the package
        /// </summary>
        /// <param name="includePrerelease">include versions with prerelease labels</param>
        /// <param name="includeUnlisted">not implemented yet</param>
        public override async Task<IEnumerable<KeyValuePair<string, NuGetVersion>>> GetLatestVersions(IEnumerable<string> packageIds, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            List<KeyValuePair<string, NuGetVersion>> results = new List<KeyValuePair<string, NuGetVersion>>();

            foreach (var id in packageIds)
            {
                var registrations = await _regResource.Get(id, includePrerelease, includeUnlisted, token);
                var allVersions = registrations.Select(p => NuGetVersion.Parse(p["version"].ToString()));

                if (!includePrerelease)
                {
                    allVersions = allVersions.Where(e => !e.IsPrerelease);
                }

                // TODO: implement unlisted on the server, and then here!

                // find the latest
                var latest = allVersions.OrderByDescending(p => p, VersionComparer.VersionRelease).FirstOrDefault();

                results.Add(new KeyValuePair<string, NuGetVersion>(id, latest));
            }

            return results;
        }

        /// <summary>
        /// Not implemented yet
        /// </summary>
        public override async Task<IEnumerable<KeyValuePair<string, bool>>> ArePackagesSatellite(IEnumerable<string> packageId, CancellationToken token)
        {
            await Task.Delay(1);
            throw new NotImplementedException();
        }
    }
}
