// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.VisualStudio
{
    public class PSSearchResourceV3Provider : ResourceProvider
    {
        public PSSearchResourceV3Provider()
            : base(typeof(PSSearchResource), 
                  nameof(PSSearchResourceV3Provider),
                  NuGetResourceProviderPositions.First)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PSSearchResource resource = null;

            var searchResource = await source.GetResourceAsync<RawSearchResourceV3>(token);

            if (searchResource != null)
            {
                resource = new PowerShellSearchResourceV3(searchResource);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
