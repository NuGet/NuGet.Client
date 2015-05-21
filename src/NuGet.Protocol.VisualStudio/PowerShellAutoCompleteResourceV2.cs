// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.v2;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class PowerShellAutoCompleteResourceV2 : PSAutoCompleteResource
    {
        private readonly IPackageRepository V2Client;

        public PowerShellAutoCompleteResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public PowerShellAutoCompleteResourceV2(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public override async Task<IEnumerable<string>> IdStartsWith(
            string packageIdPrefix,
            bool includePrerelease,
            CancellationToken token)
        {
            IEnumerable<string> result;
            if (IsLocalSource())
            {
                result = await GetPackageIdsFromLocalPackageRepository(V2Client, packageIdPrefix, true, token);
            }
            else
            {
                result = await GetPackageIdsFromHttpSourceRepository(V2Client, packageIdPrefix, true, token);
            }

            return result;
        }

        public override async Task<IEnumerable<NuGetVersion>> VersionStartsWith(
            string packageId,
            string versionPrefix,
            bool includePrerelease,
            CancellationToken token)
        {
            IEnumerable<NuGetVersion> result;
            if (IsLocalSource())
            {
                result = await GetPackageVersionsFromLocalPackageRepository(V2Client, packageId, versionPrefix, includePrerelease, token);
            }
            else
            {
                result = await GetPackageversionsFromHttpSourceRepository(V2Client, packageId, versionPrefix, includePrerelease, token);
            }

            return result;
        }

        private static async Task<IEnumerable<string>> GetPackageIdsFromHttpSourceRepository(
            IPackageRepository packageRepository,
            string searchFilter,
            bool includePrerelease,
            CancellationToken token)
        {
            var packageSourceUri = new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/", packageRepository.Source.TrimEnd('/')));
            var apiEndpointUri = new UriBuilder(new Uri(packageSourceUri, @"package-ids"))
            {
                Query = "partialId=" + searchFilter + "&" + "includePrerelease=" + includePrerelease.ToString()
            };
            return await GetResults(apiEndpointUri.Uri, token);
        }

        private static async Task<IEnumerable<NuGetVersion>> GetPackageversionsFromHttpSourceRepository(
            IPackageRepository packageRepository,
            string packageId,
            string versionPrefix,
            bool includePrerelease,
            CancellationToken token)
        {
            var packageSourceUri = new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/", packageRepository.Source.TrimEnd('/')));
            var apiEndpointUri = new UriBuilder(new Uri(packageSourceUri, @"package-versions/" + packageId))
            {
                Query = "includePrerelease=" + includePrerelease.ToString()
            };

            var results = await GetResults(apiEndpointUri.Uri, token);
            var versions = results.ToList();
            versions = versions.Where(item => item.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            return versions.Select(item => NuGetVersion.Parse(item));
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

        private static async Task<IEnumerable<string>> GetResults(Uri apiEndpointUri, CancellationToken token)
        {
            var jsonSerializer = new DataContractJsonSerializer(typeof(string[]));
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                var httpResponseMessage = await httpClient.GetAsync(apiEndpointUri, token);
                var stream = await httpResponseMessage.Content.ReadAsStreamAsync();
                return jsonSerializer.ReadObject(stream) as string[];
            }
        }

        private bool IsLocalSource()
        {
            var packageSource = new Configuration.PackageSource(V2Client.Source);
            return !packageSource.IsHttp;
        }
    }
}
