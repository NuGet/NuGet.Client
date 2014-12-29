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
        public override bool TryCreateResource(PackageSource source, out Resource resource)
        {
            V3DownloadResource v3DownloadResource;
            if (base.TryCreateResource(source, out resource))
            {
                v3DownloadResource = new V3DownloadResource((V3Resource)resource);
                resource = v3DownloadResource;
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
