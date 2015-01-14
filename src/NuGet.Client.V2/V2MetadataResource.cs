using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    public class V2MetadataResource : MetadataResource
    {
        private readonly IPackageRepository V2Client;
         public V2MetadataResource(IPackageRepository repo)
        {
            V2Client = repo;
        }
         public V2MetadataResource(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }     

        public override Task<IEnumerable<KeyValuePair<string, bool>>> ArePackagesSatellite(IEnumerable<string> packageId, System.Threading.CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task<IEnumerable<KeyValuePair<string, NuGetVersion>>> GetLatestVersions(IEnumerable<string> packageIds, bool includePrerelease, bool includeUnlisted, System.Threading.CancellationToken token)
        {
            return Task.Factory.StartNew(() =>
            {
               List<KeyValuePair<string, NuGetVersion>> results = new List<KeyValuePair<string, NuGetVersion>>();
                foreach (var id in packageIds)
                {
                    SemanticVersion latestVersion = V2Client.FindPackagesById(id).OrderByDescending(p => p.Version).FirstOrDefault().Version;
                    //  return new NuGetVersion(latestVersion.Version, latestVersion.SpecialVersion);
                    results.Add(new KeyValuePair<string, NuGetVersion>(id, new NuGetVersion(latestVersion.Version, latestVersion.SpecialVersion)));
                }
                return results.AsEnumerable();
            });
        }
    }
}
