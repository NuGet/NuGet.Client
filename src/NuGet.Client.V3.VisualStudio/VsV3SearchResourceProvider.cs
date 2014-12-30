using System.ComponentModel.Composition;
using System.Threading.Tasks;
using NuGet.Client.VisualStudio.Models;

namespace NuGet.Client.V3.VisualStudio
{
    [Export(typeof(ResourceProvider))]
    [ResourceProviderMetadata("VsV3SearchResourceProvider", typeof(IVsSearch))]
    public class VsV3SearchResourceProvider : V3ResourceProvider
    {
        public async override Task<Resource> Create(PackageSource source)
        {
            Resource resource = await base.Create(source);
            if (resource != null)
            {
                var vsV3SearchResource = new VsV3SearchResource((V3Resource)resource);
                resource = vsV3SearchResource;
                return resource;
            }
            else
            {
                return null;
            }
        }
    }
}