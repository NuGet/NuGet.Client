using System.ComponentModel.Composition;
using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using System.Threading;
using NuGet.Protocol.Core.v2;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.VisualStudio
{

    public class UISearchResourceV2Provider : V2ResourceProvider
    {
        public UISearchResourceV2Provider()
            : base(typeof(UISearchResource), "UISearchResourceV2Provider", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            UISearchResourceV2 resource = null;
            V2Resource v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new UISearchResourceV2(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}