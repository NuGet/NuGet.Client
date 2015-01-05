using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public interface IVisualStudioUIMetadata
    {
       Task<VisualStudioUIPackageMetadata> GetPackageMetadataForVisualStudioUI(string packageId, NuGetVersion version);
       Task<IEnumerable<VisualStudioUIPackageMetadata>> GetPackageMetadataForAllVersionsForVisualStudioUI(string packageId);
    }
}
