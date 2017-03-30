﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalPackageMetadataResource : PackageMetadataResource
    {
        private readonly FindLocalPackagesResource _localResource;

        public LocalPackageMetadataResource(FindLocalPackagesResource localResource)
        {
            if (localResource == null)
            {
                throw new ArgumentNullException(nameof(localResource));
            }

            _localResource = localResource;
        }

        public override Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            ILogger log,
            CancellationToken token)
        {
            // All packages are considered listed within a local repo

            return Task.Run<IEnumerable<IPackageSearchMetadata>>(() =>
            {
                return _localResource.FindPackagesById(packageId, log, token)
                    .Where(p => includePrerelease || !p.Identity.Version.IsPrerelease)
                    .Select(GetPackageMetadata)
                    .ToList();
            },
            token);
        }

        public override Task<IPackageSearchMetadata> GetMetadataAsync(
            PackageIdentity package,
            ILogger log,
            CancellationToken token)
        {
            return Task.Run<IPackageSearchMetadata>(() =>
                {
                    var packageInfo = _localResource.GetPackage(package, log, token);
                    if (packageInfo != null)
                    {
                        return GetPackageMetadata(packageInfo);
                    }
                    return null;
                },
            token);
        }

        private static IPackageSearchMetadata GetPackageMetadata(LocalPackageInfo package) => new LocalPackageSearchMetadata(package);
    }
}
