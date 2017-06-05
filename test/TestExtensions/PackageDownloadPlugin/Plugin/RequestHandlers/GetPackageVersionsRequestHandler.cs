// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class GetPackageVersionsRequestHandler
        : RequestHandler<GetPackageVersionsRequest, GetPackageVersionsResponse>
    {
        private readonly ServiceContainer _serviceContainer;

        internal GetPackageVersionsRequestHandler(ServiceContainer serviceContainer)
        {
            Assert.IsNotNull(serviceContainer, nameof(serviceContainer));

            _serviceContainer = serviceContainer;
        }

        internal override Task CancelAsync(
            IConnection connection,
            GetPackageVersionsRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<GetPackageVersionsResponse> RespondAsync(
            IConnection connection,
            GetPackageVersionsRequest request,
            CancellationToken cancellationToken)
        {
            var downloader = _serviceContainer.GetInstance<PackageDownloader>();
            var packageSource = new PackageSource(request.PackageSourceRepository);

            var versions = await downloader.GetPackageVersionsAsync(packageSource, request.PackageId, cancellationToken);
            var responseCode = versions == null ? MessageResponseCode.NotFound : MessageResponseCode.Success;

            return new GetPackageVersionsResponse(responseCode, versions);
        }
    }
}