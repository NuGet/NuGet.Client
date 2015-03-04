using NuGet.Protocol.Core.v3.Data;
using NuGet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.VisualStudio
{
    public class UISearchResourceV3Provider : ResourceProvider
    {
        private readonly DataClient _client;

        public UISearchResourceV3Provider()
            : this(new DataClient())
        {

        }

        public UISearchResourceV3Provider(DataClient client)
            : base(typeof(UISearchResource), "UISearchResourceV3Provider", "UISearchResourceV2Provider")
        {
            _client = client;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            UISearchResourceV3 curResource = null;
            ServiceIndexResourceV3 serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var rawSearch = await source.GetResourceAsync<RawSearchResourceV3>(token);
                var metadataResource = await source.GetResourceAsync<UIMetadataResource>(token);

                curResource = new UISearchResourceV3(rawSearch, metadataResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
