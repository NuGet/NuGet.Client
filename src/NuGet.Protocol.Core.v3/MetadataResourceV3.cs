using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Returns the full package metadata
    /// </summary>
    public class MetadataResourceV3 : MetadataResource
    {
        private RegistrationResourceV3 _regResource;
        private HttpClient _client;

        public MetadataResourceV3(HttpClient client, RegistrationResourceV3 regResource)
            : base()
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (regResource == null)
            {
                throw new ArgumentNullException("regResource");
            }

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
                var catalogEntries = await _regResource.GetPackageMetadata(id, includePrerelease, includeUnlisted, token);
                var allVersions = catalogEntries.Select(p => NuGetVersion.Parse(p["version"].ToString()));

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

        public override async Task<bool> Exists(PackageIdentity identity, bool includeUnlisted, CancellationToken token)
        {
            // TODO: get the url and just check the headers?
            var metadata = await _regResource.GetPackageMetadata(identity, token);

            // TODO: listed check
            return metadata != null;
        }

        public override async Task<bool> Exists(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            var entries = await GetVersions(packageId, includePrerelease, includeUnlisted, token);

            return entries != null && entries.Any();
        }

        public override async Task<IEnumerable<NuGetVersion>> GetVersions(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            List<NuGetVersion> results = new List<NuGetVersion>();

            var entries = await _regResource.GetPackageEntries(packageId, includeUnlisted, token);

            foreach (var catalogEntry in entries)
            {
                NuGetVersion version = null;

                if (catalogEntry["version"] != null && NuGetVersion.TryParse(catalogEntry["version"].ToString(), out version))
                {
                    if (includePrerelease || !version.IsPrerelease)
                    {
                        results.Add(version);
                    }
                }
            }

            return results;
        }
    }
}
