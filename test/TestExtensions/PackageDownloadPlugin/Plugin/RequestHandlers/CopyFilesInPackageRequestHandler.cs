// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class CopyFilesInPackageRequestHandler
        : PackageRequestHandler<CopyFilesInPackageRequest, CopyFilesInPackageResponse>
    {
        internal CopyFilesInPackageRequestHandler(ServiceContainer serviceContainer)
            : base(serviceContainer)
        {
        }

        internal override Task CancelAsync(
            IConnection connection,
            CopyFilesInPackageRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<CopyFilesInPackageResponse> RespondAsync(
            IConnection connection,
            CopyFilesInPackageRequest request,
            CancellationToken cancellationToken)
        {
            var filePath = await DownloadPackageAsync(
                request.PackageSourceRepository,
                request.PackageId,
                request.PackageVersion,
                cancellationToken);

            if (string.IsNullOrEmpty(filePath))
            {
                return new CopyFilesInPackageResponse(MessageResponseCode.NotFound, copiedFiles: null);
            }

            var logger = ServiceContainer.GetInstance<Logger>();
            var packageFileExtractor = new PackageFileExtractor(
                request.FilesInPackage,
                XmlDocFileSaveMode.None);

            using (var packageReader = new PackageArchiveReader(filePath))
            {
                var files = await packageReader.CopyFilesAsync(
                    request.DestinationFolderPath,
                    request.FilesInPackage,
                    packageFileExtractor.ExtractPackageFile,
                    logger,
                    cancellationToken);

                return new CopyFilesInPackageResponse(MessageResponseCode.Success, files);
            }
        }
    }
}