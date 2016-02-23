﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    public class MetadataResourceV2Provider : V2ResourceProvider
    {
        public MetadataResourceV2Provider()
            : base(typeof(MetadataResource), "MetadataResourceV2Provider", NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            MetadataResource resource = null;

            if (FeedTypeUtility.GetFeedType(source.PackageSource) == FeedType.FileSystem)
            {
                var v2repo = await GetRepository(source, token);

                if (v2repo != null)
                {
                    resource = new MetadataResourceV2(v2repo);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
