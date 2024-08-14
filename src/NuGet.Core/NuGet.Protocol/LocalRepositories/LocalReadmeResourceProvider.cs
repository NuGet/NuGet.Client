// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalReadmeResourceProvider : ResourceProvider
    {
        public LocalReadmeResourceProvider()
            : base(typeof(ReadmeResource), nameof(LocalReadmeResourceProvider))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            LocalReadmeResource readmeResource = null;

            var localResource = await source.GetResourceAsync<FindLocalPackagesResource>(token);

            if (localResource != null)
            {
                readmeResource = new LocalReadmeResource(localResource);
            }

            return new Tuple<bool, INuGetResource>(readmeResource != null, readmeResource);
        }
    }
}
