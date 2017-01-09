// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    public class ListCommandResourceV2Provider : V2ResourceProvider
    {
        public ListCommandResourceV2Provider()
            : base(
                  typeof(ListCommandResource),
                  nameof(ListCommandResourceV2Provider),
                  NuGetResourceProviderPositions.Last) { }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            ListCommandResource listCommandResource = null;

            var v2repo = await GetRepository(source, token);

            if (v2repo != null
                && v2repo.V2Client != null
                && !string.IsNullOrEmpty(v2repo.V2Client.Source))
            {
                // For a V2 package source, the source url is the list endpoint as well
                listCommandResource = new ListCommandResource(v2repo.V2Client.Source);
            }

            var result = new Tuple<bool, INuGetResource>(listCommandResource != null, listCommandResource);
            return result;
        }
    }
}
