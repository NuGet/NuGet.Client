// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;

namespace NuGet.Protocol.VisualStudio
{
    public class AutoCompleteResourceLocalProvider : V2ResourceProvider
    {
        public AutoCompleteResourceLocalProvider()
            : base(
                  typeof(AutoCompleteResource),
                  nameof(AutoCompleteResourceLocalProvider),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            AutoCompleteResourceLocal resource = null;

            if (FeedTypeUtility.GetFeedType(source.PackageSource) == FeedType.FileSystem)
            {
                var v2repo = await GetRepository(source, token);

                if (v2repo != null)
                {
                    resource = new AutoCompleteResourceLocal(v2repo);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
