// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class PackageMetadataResourceLocal : PackageMetadataResource
    {
        private readonly IPackageRepository V2Client;

        public PackageMetadataResourceLocal(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            Logging.ILogger log,
            CancellationToken token)
        {
            return await Task.Run(() =>
                {
                    return V2Client.FindPackagesById(packageId)
                        .Where(p => includeUnlisted || !p.Published.HasValue || p.Published.Value.Year > 1901)
                        .Where(p => includePrerelease || String.IsNullOrEmpty(p.Version.SpecialVersion))
                        .Select(GetPackageMetadata);
                },
                token);
        }

        private static IPackageSearchMetadata GetPackageMetadata(IPackage package) => new PackageSearchMetadata(package);
    }
}
