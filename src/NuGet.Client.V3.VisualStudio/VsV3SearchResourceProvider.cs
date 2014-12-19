using NuGet.Client.V3;
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
    [ResourceProviderMetadata("VsV3SearchResourceProvider", typeof(IVsSearch))]
    public class VsV3SearchResourceProvider : V3ResourceProvider
    {
        public bool TryCreateResource(PackageSource source, out Resource resource)
        {
            VsV3SearchResource vsV3SearchResource;
            if (base.TryCreateResource(source, out resource))
            {
                vsV3SearchResource = new VsV3SearchResource((V3Resource)resource);
                resource = vsV3SearchResource;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
