// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.LocalRepositories;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    /// <summary>
    /// A <see cref="ResourceProvider" /> for <see cref="FindPackageByIdResource" /> over v2 NuGet feeds.
    /// </summary>
    public class RemoteV2FindPackageByIdResourceProvider : ResourceProvider
    {
        public RemoteV2FindPackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource), name: nameof(RemoteV2FindPackageByIdResourceProvider), before: nameof(LocalV2FindPackageByIdResourceProvider))
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository sourceRepository, CancellationToken token)
        {
            INuGetResource resource = null;

            if (sourceRepository.PackageSource.IsHttp
                &&
                !sourceRepository.PackageSource.Source.EndsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                resource = new RemoteV2FindPackageByIdResource(
                    sourceRepository.PackageSource, 
                    async () => (await sourceRepository.GetResourceAsync<HttpHandlerResource>(token)));
            }

            return Task.FromResult(Tuple.Create(resource != null, resource));
        }
    }
}
