using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3
{
    /// <summary>
    /// Provides the download metatdata for a given package from a V3 server endpoint.
    /// </summary>
    public class V3DownloadResource : V3Resource,IDownload
    {
        public V3DownloadResource(V3Resource v3Resource)
            : base(v3Resource) { }
        public async Task<PackageDownloadMetadata> GetNupkgUrlForDownload(PackageIdentity identity)
        {
           JObject metadata =  await V3Client.GetPackageMetadata(identity.Id, identity.Version); //*TODOs: Need to see if this can be optimized.
           Uri uri  = new Uri((string)metadata["packageContent"]);
           return new PackageDownloadMetadata(uri);
          
        }
    }
}
