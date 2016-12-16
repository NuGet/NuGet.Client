// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class ListResourceV2FeedResourceProvider : ResourceProvider
    {

        public ListResourceV2FeedResourceProvider() : base(
            typeof(ListResource),
            nameof(ListResourceV2FeedResourceProvider),
            NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source,
            CancellationToken token)
        {
            ListResource resource = null;

            if (await source.GetFeedType(token) == FeedType.HttpV2)// TODO NK - Does the feed type matter at all?
            {
                var serviceDocument = await source.GetResourceAsync<ODataServiceDocumentResourceV2>(token);

                var httpSource = await source.GetResourceAsync<HttpSourceResource>(token);
                var parser = new V2FeedParser(httpSource.HttpSource, serviceDocument.BaseAddress, source.PackageSource);

                var feedCapabilityResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceDocument.BaseAddress);

                resource = new ListResourceV2Feed(parser,feedCapabilityResource);
            }
            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}