// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.VisualStudio
{
    public class PSAutoCompleteResourceV3Provider : ResourceProvider
    {
        public PSAutoCompleteResourceV3Provider()
            : base(typeof(PSAutoCompleteResource), "PSAutoCompleteResourceV3Provider", "V2PSAutoCompleteResourceProvider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PSAutoCompleteResourceV3 curResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var regResource = await source.GetResourceAsync<RegistrationResourceV3>(token);
                var handlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                var client = new DataClient(handlerResource.MessageHandler);

                // construct a new resource
                curResource = new PSAutoCompleteResourceV3(client, serviceIndex, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
