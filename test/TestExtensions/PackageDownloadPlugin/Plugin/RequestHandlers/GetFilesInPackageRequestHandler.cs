// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class GetFilesInPackageRequestHandler
        : PackageRequestHandler<GetFilesInPackageRequest, GetFilesInPackageResponse>
    {
        internal GetFilesInPackageRequestHandler(ServiceContainer serviceContainer)
            : base(serviceContainer)
        {
        }

        internal override Task CancelAsync(
            IConnection connection,
            GetFilesInPackageRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<GetFilesInPackageResponse> RespondAsync(
            IConnection connection,
            GetFilesInPackageRequest request,
            CancellationToken cancellationToken)
        {
            var filePath = await DownloadPackageAsync(
                request.PackageSourceRepository,
                request.PackageId,
                request.PackageVersion,
                cancellationToken);

            if (string.IsNullOrEmpty(filePath))
            {
                return new GetFilesInPackageResponse(MessageResponseCode.NotFound, files: null);
            }

            using (var packageReader = new PackageArchiveReader(filePath))
            {
                var files = await packageReader.GetFilesAsync(cancellationToken);

                return new GetFilesInPackageResponse(MessageResponseCode.Success, files);
            }
        }
    }
}