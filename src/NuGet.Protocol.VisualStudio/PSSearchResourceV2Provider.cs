using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{

    public class PSSearchResourceV2Provider : V2ResourceProvider
    {
        public PSSearchResourceV2Provider()
            : base(typeof(PSSearchResource), "V2PSSearchResourceProvider", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PSSearchResource resource = null;
            UISearchResource uiSearchResource = await source.GetResourceAsync<UISearchResource>(token);
            if (uiSearchResource != null)
            {
                resource = new PowerShellSearchResourceV2(uiSearchResource);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
