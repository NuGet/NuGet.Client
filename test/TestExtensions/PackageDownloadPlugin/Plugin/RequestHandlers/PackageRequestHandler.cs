// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal abstract class PackageRequestHandler<TRequest, TResponse>
        : RequestHandler<TRequest, TResponse>
            where TRequest : class
            where TResponse : class
    {
        protected ServiceContainer ServiceContainer { get; }

        protected PackageRequestHandler(ServiceContainer serviceContainer)
        {
            Assert.IsNotNull(serviceContainer, nameof(serviceContainer));

            ServiceContainer = serviceContainer;
        }

        protected async Task<string> DownloadPackageAsync(
            string packageSourceRepository,
            string packageId,
            string packageVersion,
            CancellationToken cancellationToken)
        {
            var downloader = ServiceContainer.GetInstance<PackageDownloader>();
            var packageSource = new PackageSource(packageSourceRepository);
            var packageIdentity = new PackageIdentity(packageId, NuGetVersion.Parse(packageVersion));

            return await downloader.DownloadPackageAsync(packageSource, packageIdentity, cancellationToken);
        }
    }
}