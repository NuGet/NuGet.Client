using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    public class MetadataResourceV3Provider : ResourceProvider
    {
        public MetadataResourceV3Provider()
            : base(typeof(MetadataResource), "MetadataResourceV3Provider", "MetadataResourceV2Provider")
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            MetadataResourceV3 curResource = null;
            RegistrationResourceV3 regResource = await source.GetResourceAsync<RegistrationResourceV3>(token);

            if (regResource != null)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                curResource = new MetadataResourceV3(client, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
