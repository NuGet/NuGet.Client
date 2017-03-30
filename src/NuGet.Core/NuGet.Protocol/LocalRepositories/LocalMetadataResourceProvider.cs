﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalMetadataResourceProvider : ResourceProvider
    {
        public LocalMetadataResourceProvider()
            : base(typeof(MetadataResource), nameof(LocalMetadataResourceProvider), NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            INuGetResource resource = null;

            var localResource = await source.GetResourceAsync<FindLocalPackagesResource>(token);

            if (localResource != null)
            {
                resource = new LocalMetadataResource(localResource);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
