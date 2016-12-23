// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class ListCommandResourceLegacyV2Provider : ResourceProvider
    {
        public ListCommandResourceLegacyV2Provider()
            : base(
                  typeof(ListCommandResource),
                  nameof(ListCommandResourceLegacyV2Provider),
                  nameof(ListCommandResourceLocalPackagesProvider)) { }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {           

            ListCommandResource listCommandResource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV2)
            {

            var url = source.PackageSource.Source;
            if (source.PackageSource.ProtocolVersion == 2 ||
                (source.PackageSource.IsHttp &&
                 !url.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                listCommandResource = new ListCommandResource(source.PackageSource.Source);
            }

            }

            var result = new Tuple<bool, INuGetResource>(listCommandResource != null, listCommandResource);
            return result;
        }
    }
}
