// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    public class ListCommandResourceV3Provider : ResourceProvider
    {
        public ListCommandResourceV3Provider()
            : base(
                  typeof(ListCommandResource),
                  nameof(ListCommandResourceV3Provider),
                  "ListCommandResourceV2Provider") { }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            ListCommandResource listCommandResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var baseUrl = serviceIndex[ServiceTypes.LegacyGallery].FirstOrDefault();
                if (baseUrl != null)
                {
                    listCommandResource = new ListCommandResource(baseUrl.AbsoluteUri);
                }
            }

            var result = new Tuple<bool, INuGetResource>(listCommandResource != null, listCommandResource);
            return result;
        }
    }
}
