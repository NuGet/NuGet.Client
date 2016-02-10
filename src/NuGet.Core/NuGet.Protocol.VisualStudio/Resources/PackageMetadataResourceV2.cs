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
    public class PackageMetadataResourceV2 : PackageMetadataResource
    {
        private readonly IPackageRepository V2Client;

        public PackageMetadataResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            IEnumerable<PackageIdentity> packages,
            CancellationToken token)
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var results = new List<IPackageSearchMetadata>();

                foreach (var group in packages.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
                {
                    if (group.Count() == 1)
                    {
                        // optimization for a single package
                        var package = group.Single();
                        var result = V2Client.FindPackage(package.Id, SemanticVersion.Parse(package.Version.ToString()));
                        if (result != null)
                        {
                            results.Add(GetPackageMetadata(result));
                        }
                    }
                    else
                    {
                        // batch mode
                        var foundPackages = V2Client.FindPackagesById(group.Key)
                            .Where(p => group.Any(e => VersionComparer.VersionRelease.Equals(e.Version, NuGetVersion.Parse(p.Version.ToString()))));
                        token.ThrowIfCancellationRequested();

                        var metadataPackages = foundPackages
                            .Select(GetPackageMetadata);
                        results.AddRange(metadataPackages);
                    }
                }

                return results;
            },
            token);
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
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
