using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3
{
    public class V3MetadataResource :V3Resource,IMetadata
    {
        public V3MetadataResource(V3Resource v3Resource) : base(v3Resource) { }
        public async Task<Versioning.NuGetVersion> GetLatestVersion(string packageId)
        {
            IEnumerable<JObject> packages = await V3Client.GetPackageMetadataById(packageId);
            packages = packages.OrderByDescending(p => p["version"]);
            return new NuGetVersion((string)packages.FirstOrDefault()["version"]);           
        }

        public Task<bool> IsSatellitePackage(string packageId)
        {
            throw new NotImplementedException();
        }
    }
}
