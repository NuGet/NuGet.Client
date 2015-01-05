using System.ComponentModel.Composition;
using System.Threading.Tasks;
using NuGet.Client.VisualStudio.Models;

namespace NuGet.Client.V3.VisualStudio
{
    [Export(typeof(ResourceProvider))]
    [ResourceProviderMetadata("VsV3SearchResourceProvider", typeof(IVisualStudioUISearch))]
    public class V3VisualStudioUISearchResourceProvider : V3ResourceProvider
    {
        public async override Task<Resource> Create(PackageSource source)
        {
            Resource resource = await base.Create(source);
            if (resource != null)
            {
                var vsV3SearchResource = new V3VisualstudioUISearchResource((V3Resource)resource);
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