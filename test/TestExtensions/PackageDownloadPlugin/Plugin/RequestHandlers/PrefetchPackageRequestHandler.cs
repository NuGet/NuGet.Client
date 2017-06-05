// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class PrefetchPackageRequestHandler
        : PackageRequestHandler<PrefetchPackageRequest, PrefetchPackageResponse>
    {
        internal PrefetchPackageRequestHandler(ServiceContainer serviceContainer)
            : base(serviceContainer)
        {
        }

        internal override Task CancelAsync(
            IConnection connection,
            PrefetchPackageRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<PrefetchPackageResponse> RespondAsync(
            IConnection connection,
            PrefetchPackageRequest request,
            CancellationToken cancellationToken)
        {
            var filePath = await DownloadPackageAsync(
                request.PackageSourceRepository,
                request.PackageId,
                request.PackageVersion,
                cancellationToken);
            var responseCode = string.IsNullOrEmpty(filePath) ? MessageResponseCode.NotFound : MessageResponseCode.Success;

            return new PrefetchPackageResponse(responseCode);
        }
    }
}