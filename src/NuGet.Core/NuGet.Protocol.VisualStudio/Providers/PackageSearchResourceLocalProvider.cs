// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;

namespace NuGet.Protocol.VisualStudio
{
    public class PackageSearchResourceLocalProvider : V2ResourceProvider
    {
        public PackageSearchResourceLocalProvider()
            : base(typeof(PackageSearchResource), nameof(PackageSearchResourceLocalProvider), NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source,
                                                                          CancellationToken token)
        {
            PackageSearchResourceLocal resource = null;

            if (FeedTypeUtility.GetFeedType(source.PackageSource) == FeedType.FileSystem)
            {
                var v2repo = await GetRepository(source, token);

                if (v2repo != null)
                {
                    resource = new PackageSearchResourceLocal(v2repo);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
