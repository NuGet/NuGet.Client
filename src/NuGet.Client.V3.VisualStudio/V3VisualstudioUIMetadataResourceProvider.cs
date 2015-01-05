using NuGet.Client.VisualStudio.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    [Export(typeof(ResourceProvider))]
    [ResourceProviderMetadata("VsV3MetadataResourceProvider", typeof(IVisualStudioUIMetadata))]
    public class V3VisualStudioUIMetadataResourceProvider : V3ResourceProvider
    {
        public async override Task<Resource> Create(PackageSource source)
        {
            Resource resource = await base.Create(source);
            if (resource != null)
            {
                var vsV3MetadataResource = new V3VisualStudioUIMetadataResource((V3Resource)resource);
                resource = vsV3MetadataResource;
                return resource;
            }
            else
            {
                return null;
            }
        }
    }
}
