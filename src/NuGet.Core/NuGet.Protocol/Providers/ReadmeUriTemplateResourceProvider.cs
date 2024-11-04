// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    internal class ReadmeUriTemplateResourceProvider : ResourceProvider
    {
        public ReadmeUriTemplateResourceProvider()
            : base(typeof(ReadmeUriTemplateResource),
                  nameof(ReadmeUriTemplateResource),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ReadmeUriTemplateResource resource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);
            if (serviceIndex != null)
            {
                var uriTemplate = serviceIndex.GetServiceEntryUri(ServiceTypes.ReadmeFileUrl)?.OriginalString;

                // construct a new resource
                resource = string.IsNullOrWhiteSpace(uriTemplate) ? null : new ReadmeUriTemplateResource(uriTemplate);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
