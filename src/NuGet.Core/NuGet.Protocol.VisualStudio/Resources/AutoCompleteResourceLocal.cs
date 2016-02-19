// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class AutoCompleteResourceLocal : AutoCompleteResource
    {
        private readonly IPackageRepository V2Client;

        public AutoCompleteResourceLocal(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public AutoCompleteResourceLocal(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public override async Task<IEnumerable<string>> IdStartsWith(
            string packageIdPrefix,
            bool includePrerelease,
            Logging.ILogger log,
            CancellationToken token)
        {
            return await GetPackageIdsFromLocalPackageRepository(V2Client, packageIdPrefix, true, token);
        }

        public override async Task<IEnumerable<NuGetVersion>> VersionStartsWith(
            string packageId,
            string versionPrefix,
            bool includePrerelease,
            Logging.ILogger log,
            CancellationToken token)
        {
            return await GetPackageVersionsFromLocalPackageRepository(V2Client, packageId, versionPrefix, includePrerelease, token);
        }

        private static async Task<IEnumerable<string>> GetPackageIdsFromLocalPackageRepository(
            IPackageRepository packageRepository,
            string searchFilter,
            bool includePrerelease,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Cancellation token is not passed to NuGet.Core layer as it doesn't act upon it.
            return await Task.Run(() =>
            {
                IEnumerable<IPackage> packages = packageRepository.GetPackages();

                if (!String.IsNullOrEmpty(searchFilter))
                {
                    packages = packages.Where(p => p.Id.StartsWith(searchFilter, StringComparison.OrdinalIgnoreCase));
                }

                if (!includePrerelease)
                {
                    packages = packages.Where(p => p.IsReleaseVersion());
                }

                return packages.Select(p => p.Id)
                    .Distinct()
                    .Take(30);
            },
            token);
        }

        protected async Task<IEnumerable<NuGetVersion>> GetPackageVersionsFromLocalPackageRepository(
            IPackageRepository packageRepository,
            string packageId,
            string versionPrefix,
            bool includePrerelease,
            CancellationToken token)
        {
            return await Task.Run(() =>
            {
                var packages = packageRepository.GetPackages().Where(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
                token.ThrowIfCancellationRequested();

                if (!includePrerelease)
                {
                    packages = packages.Where(p => p.IsReleaseVersion());
                }

                var versions = packages.Select(p => p.Version.ToString()).ToList();
                versions = versions.Where(item => item.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
                return versions.Select(item => NuGetVersion.Parse(item));
            },
            token);
        }
    }
}
