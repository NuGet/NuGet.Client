// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class CopyNupkgFileRequestHandler
        : PackageRequestHandler<CopyNupkgFileRequest, CopyNupkgFileResponse>
    {
        internal CopyNupkgFileRequestHandler(ServiceContainer serviceContainer)
            : base(serviceContainer)
        {
        }

        internal override Task CancelAsync(
            IConnection connection,
            CopyNupkgFileRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<CopyNupkgFileResponse> RespondAsync(
            IConnection connection,
            CopyNupkgFileRequest request,
            CancellationToken cancellationToken)
        {
            var exposeNupkgFilesToNuGet = GetExposeNupkgFilesToNuGet(request.PackageSourceRepository);

            if (!exposeNupkgFilesToNuGet)
            {
                return new CopyNupkgFileResponse(MessageResponseCode.NotFound);
            }

            var filePath = await DownloadPackageAsync(
                request.PackageSourceRepository,
                request.PackageId,
                request.PackageVersion,
                cancellationToken);

            if (string.IsNullOrEmpty(filePath))
            {
                return new CopyNupkgFileResponse(MessageResponseCode.NotFound);
            }

            try
            {
                File.Copy(filePath, request.DestinationFilePath);
            }
            catch (IOException)
            {
                return new CopyNupkgFileResponse(MessageResponseCode.Error);
            }

            return new CopyNupkgFileResponse(MessageResponseCode.Success);
        }

        private bool GetExposeNupkgFilesToNuGet(string packageSourceRepository)
        {
            var configuration = ServiceContainer.GetInstance<PluginConfiguration>();

            var pluginPackageSource = configuration.PluginPackageSources
                .Where(source => string.Equals(source.PackageSource.Source, packageSourceRepository, StringComparison.OrdinalIgnoreCase))
                .Single();

            return pluginPackageSource.ExposeNupkgFilesToNuGet;
        }
    }
}