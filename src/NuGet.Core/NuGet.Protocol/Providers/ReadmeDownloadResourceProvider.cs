// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class ReadmeDownloadResourceProvider : ResourceProvider
    {
        public ReadmeDownloadResourceProvider()
            : base(typeof(ReadmeDownloadResource), nameof(ReadmeDownloadResourceProvider))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ReadmeDownloadResource curResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
                var client = httpSourceResource.HttpSource;

                // Repository signature information init
                var repositorySignatureResource = await source.GetResourceAsync<RepositorySignatureResource>(token);
                repositorySignatureResource?.UpdateRepositorySignatureInfo();

                // If index.json contains a flat container resource use that to directly
                // construct package download urls.
                var packageBaseAddress = serviceIndex.GetServiceEntryUri(ServiceTypes.PackageBaseAddress6120)?.AbsoluteUri;

                if (packageBaseAddress != null)
                {
                    curResource = new ReadmeDownloadResource(source.PackageSource.Source, client, packageBaseAddress);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
