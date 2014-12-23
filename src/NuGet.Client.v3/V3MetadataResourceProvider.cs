using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3
{
    [Export(typeof(ResourceProvider))]
    [ResourceProviderMetadata("V3MetadataResourceProvider", typeof(IMetadata))]
    public class V3MetadataResourceProvider : V3ResourceProvider
    {
        public override bool TryCreateResource(PackageSource source, out Resource resource)
        {
            V3MetadataResource v3MetadataResource;
            if (base.TryCreateResource(source, out resource))
            {
                v3MetadataResource = new V3MetadataResource((V3Resource)resource);
                resource = v3MetadataResource;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
