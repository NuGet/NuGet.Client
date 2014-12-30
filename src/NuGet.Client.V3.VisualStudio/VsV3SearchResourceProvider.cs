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
        public async override Task<Resource> Create(PackageSource source)
        {
            VsV3SearchResource vsV3SearchResource;
            Resource resource = await base.Create(source);            
            vsV3SearchResource = new VsV3SearchResource((V3Resource)resource);
            resource = vsV3SearchResource;
            return resource;
        }
    }
}
