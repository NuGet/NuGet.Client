using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3
{
    [Export(typeof(ResourceProvider))]
    [ResourceProviderMetadata("V3DownloadResourceProvider", typeof(IDownload))]
    public class V3DownloadResourceProvider : V3ResourceProvider
    {
        public async override Task<Resource> Create(PackageSource source)
        {
            V3DownloadResource v3DownloadResource;
            Resource resource = await base.Create(source);           
            v3DownloadResource = new V3DownloadResource((V3Resource)resource);
            resource = v3DownloadResource;
            return resource;
           
        }
    }
}
