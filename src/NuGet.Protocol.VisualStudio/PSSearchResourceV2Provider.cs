// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;

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
            var uiSearchResource = await source.GetResourceAsync<UISearchResource>(token);
            if (uiSearchResource != null)
            {
                resource = new PowerShellSearchResourceV2(uiSearchResource);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
