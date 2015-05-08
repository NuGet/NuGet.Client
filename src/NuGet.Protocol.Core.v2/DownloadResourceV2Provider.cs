// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Resource provider for V2 download.
    /// </summary>
    public class DownloadResourceV2Provider : V2ResourceProvider
    {
        public DownloadResourceV2Provider()
            : base(typeof(DownloadResource), "DownloadResourceV2Provider", NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource DownloadResourceV2 = null;

            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                DownloadResourceV2 = new DownloadResourceV2(v2repo);
            }

            return new Tuple<bool, INuGetResource>(DownloadResourceV2 != null, DownloadResourceV2);
        }
    }
}
