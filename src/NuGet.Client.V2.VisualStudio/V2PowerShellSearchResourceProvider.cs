using NuGet.Client.VisualStudio;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V2.VisualStudio
{
    
    [NuGetResourceProviderMetadata(typeof(PSSearchResource), "V2PSSearchResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2PSSearchResourceProvider : V2ResourceProvider
    {
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PSSearchResource resource = null;
            UISearchResource uiSearchResource = await source.GetResourceAsync<UISearchResource>(token);
            if (uiSearchResource != null)
            {
                resource = new V2PowerShellSearchResource(uiSearchResource);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
