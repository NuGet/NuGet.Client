// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class RemoteV3FindPackagePackageByIdResourceProvider : ResourceProvider
    {
        public RemoteV3FindPackagePackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource),
                nameof(RemoteV3FindPackagePackageByIdResourceProvider),
                before: nameof(RemoteV2FindPackageByIdResourceProvider))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository sourceRepository, CancellationToken token)
        {
            INuGetResource resource = null;

            var serviceIndexResource = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();

            if (serviceIndexResource != null)
            {
                var messageHandlerResource = await sourceRepository.GetResourceAsync<HttpHandlerResource>(token);
                var client = new DataClient(messageHandlerResource.MessageHandler);
                resource = new RemoteV3FindPackageByIdResource(sourceRepository, client);
            }

            return Tuple.Create(resource != null, resource);
        }
    }
}
