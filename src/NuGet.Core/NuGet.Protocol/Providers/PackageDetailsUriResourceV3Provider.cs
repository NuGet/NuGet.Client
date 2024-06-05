// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class PackageDetailsUriResourceV3Provider : ResourceProvider
    {
        public PackageDetailsUriResourceV3Provider()
            : base(typeof(PackageDetailsUriResourceV3),
                  nameof(PackageDetailsUriResourceV3),
                  NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PackageDetailsUriResourceV3 resource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);
            if (serviceIndex != null)
            {
                var uri = serviceIndex.GetServiceEntryUri(ServiceTypes.PackageDetailsUriTemplate);
                // Check if the source is not HTTPS.
                if (uri.Scheme == Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && !source.PackageSource.AllowInsecureConnections)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_HttpServiceIndexUsage, source.PackageSource.SourceUri, uri));
                }
                resource = PackageDetailsUriResourceV3.CreateOrNull(uri?.OriginalString);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
