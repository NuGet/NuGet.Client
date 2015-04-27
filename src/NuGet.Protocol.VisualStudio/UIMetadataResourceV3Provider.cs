using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Protocol.Core.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    public class UIMetadataResourceV3Provider : ResourceProvider
    {
        private readonly DataClient _client;

        public UIMetadataResourceV3Provider()
            : this(new DataClient())
        {

        }

        public UIMetadataResourceV3Provider(DataClient client)
            : base(typeof(UIMetadataResource), "UIMetadataResourceV3Provider", "UIMetadataResourceV2Provider")
        {
            _client = client;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            UIMetadataResourceV3 curResource = null;

            if (await source.GetResourceAsync<ServiceIndexResourceV3>(token) != null)
            {
                var regResource = await source.GetResourceAsync<RegistrationResourceV3>();
                var reportAbuseResource = await source.GetResourceAsync<ReportAbuseResourceV3>();

                // construct a new resource
                curResource = new UIMetadataResourceV3(_client, regResource, reportAbuseResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
