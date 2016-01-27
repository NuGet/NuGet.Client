﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;

namespace NuGet.Protocol.VisualStudio
{
    public class UISearchResourceV2Provider : V2ResourceProvider
    {
        public UISearchResourceV2Provider()
            : base(typeof(UISearchResource), nameof(UISearchResourceV2Provider), NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source,
                                                                          CancellationToken token)
        {
            UISearchResourceV2 resource = null;

            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new UISearchResourceV2(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
