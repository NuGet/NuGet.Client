// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3
{
    public class MetadataResourceV3Provider : ResourceProvider
    {
        public MetadataResourceV3Provider()
            : base(typeof(MetadataResource),
                  nameof(MetadataResourceV3Provider),
                  "MetadataResourceV2Provider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            MetadataResourceV3 curResource = null;
            var regResource = await source.GetResourceAsync<RegistrationResourceV3>(token);

            if (regResource != null)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                var client = new DataClient(messageHandlerResource);

                curResource = new MetadataResourceV3(client, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
