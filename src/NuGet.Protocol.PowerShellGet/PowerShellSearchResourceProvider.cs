// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.PowerShellGet
{
    public class PowerShellSearchResourceProvider : ResourceProvider
    {
        public PowerShellSearchResourceProvider()
            : base(typeof(PowerShellSearchResource),
                  nameof(PowerShellSearchResource),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PowerShellSearchResource resource = null;

            // PS search depends on v3 json search
            var rawSearch = await source.GetResourceAsync<RawSearchResourceV3>();

            if (rawSearch != null)
            {
                resource = new PowerShellSearchResource(rawSearch);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
