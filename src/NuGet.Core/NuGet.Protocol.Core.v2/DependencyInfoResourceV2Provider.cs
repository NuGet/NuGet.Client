// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    public class DependencyInfoResourceV2Provider : V2ResourceProvider
    {
        public DependencyInfoResourceV2Provider()
            : base(typeof(DependencyInfoResource), "DependencyInfoResourceV2Provider", NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DependencyInfoResource DependencyInfoResourceV2 = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                DependencyInfoResourceV2 = new DependencyInfoResourceV2(v2repo, source);
            }

            return Tuple.Create<bool, INuGetResource>(DependencyInfoResourceV2 != null, DependencyInfoResourceV2);
        }
    }
}
