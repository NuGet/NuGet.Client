// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class GetPackageHashRequestHandler
        : PackageRequestHandler<GetPackageHashRequest, GetPackageHashResponse>
    {
        internal GetPackageHashRequestHandler(ServiceContainer serviceContainer)
            : base(serviceContainer)
        {
        }

        internal override Task CancelAsync(
            IConnection connection,
            GetPackageHashRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<GetPackageHashResponse> RespondAsync(
            IConnection connection,
            GetPackageHashRequest request,
            CancellationToken cancellationToken)
        {
            var filePath = await DownloadPackageAsync(
                request.PackageSourceRepository,
                request.PackageId,
                request.PackageVersion,
                cancellationToken);

            if (string.IsNullOrEmpty(filePath))
            {
                return new GetPackageHashResponse(MessageResponseCode.NotFound, hash: null);
            }

            using (var stream = File.OpenRead(filePath))
            {
                var bytes = new CryptoHashProvider(request.HashAlgorithm).CalculateHash(stream);
                var hash = Convert.ToBase64String(bytes);

                return new GetPackageHashResponse(MessageResponseCode.Success, hash);
            }
        }
    }
}