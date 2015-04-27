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
    public class PSAutoCompleteResourceV3Provider : ResourceProvider
    {
        private readonly DataClient _client;

        public PSAutoCompleteResourceV3Provider()
            : this(new DataClient())
        {

        }

        public PSAutoCompleteResourceV3Provider(DataClient client)
            : base(typeof(PSAutoCompleteResource), "PSAutoCompleteResourceV3Provider", "V2PSAutoCompleteResourceProvider")
        {
            _client = client;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PSAutoCompleteResourceV3 curResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var regResource = await source.GetResourceAsync<RegistrationResourceV3>(token);

                // construct a new resource
                curResource = new PSAutoCompleteResourceV3(_client, serviceIndex, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
