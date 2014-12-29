using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public interface IMetadata
    {
        Task<NuGetVersion> GetLatestVersion(string packageId);
        Task<bool> IsSatellitePackage(string packageId);
    }
}
