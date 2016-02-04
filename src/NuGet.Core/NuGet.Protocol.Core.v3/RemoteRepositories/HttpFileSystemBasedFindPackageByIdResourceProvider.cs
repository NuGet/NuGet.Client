// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class HttpFileSystemBasedFindPackageByIdResourceProvider : ResourceProvider
    {
        private const string HttpFileSystemIndexType = "PackageBaseAddress/3.0.0";

        public HttpFileSystemBasedFindPackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource),
                nameof(HttpFileSystemBasedFindPackageByIdResourceProvider),
                before: nameof(RemoteV3FindPackagePackageByIdResourceProvider))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository sourceRepository, CancellationToken token)
        {
            INuGetResource resource = null;
            var serviceIndexResource = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();
            var packageBaseAddress = serviceIndexResource?[HttpFileSystemIndexType];

            if (packageBaseAddress != null
                && packageBaseAddress.Count > 0)
            {
                var httpSourceResource = await sourceRepository.GetResourceAsync<HttpSourceResource>(token);

                resource = new HttpFileSystemBasedFindPackageByIdResource(
                    packageBaseAddress,
                    httpSourceResource.HttpSource);
            }

            return Tuple.Create(resource != null, resource);
        }
    }
}
