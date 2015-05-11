// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

        public override Task<IEnumerable<string>> IdStartsWith(string packageIdPrefix, bool includePrerelease, CancellationToken token)
        {
            IEnumerable<string> result;
            if (IsLocalSource())
            {
                result = GetPackageIdsFromLocalPackageRepository(V2Client, packageIdPrefix, true);
            }
            else
            {
                result = GetPackageIdsFromHttpSourceRepository(V2Client, packageIdPrefix, true);
            }

            return Task.FromResult(result);
        }

        public override Task<IEnumerable<NuGetVersion>> VersionStartsWith(string packageId, string versionPrefix, bool includePrerelease, CancellationToken token)
        {
            IEnumerable<NuGetVersion> result;
            if (IsLocalSource())
            {
                result = GetPackageVersionsFromLocalPackageRepository(V2Client, packageId, versionPrefix, includePrerelease);
            }
            else
            {
                result = GetPackageversionsFromHttpSourceRepository(V2Client, packageId, versionPrefix, includePrerelease);
            }

            return Task.FromResult(result);
        }

        private static IEnumerable<string> GetPackageIdsFromHttpSourceRepository(IPackageRepository packageRepository, string searchFilter, bool includePrerelease)
        {
            var packageSourceUri = new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/", packageRepository.Source.TrimEnd('/')));
            var apiEndpointUri = new UriBuilder(new Uri(packageSourceUri, @"package-ids"))
                {
                    Query = "partialId=" + searchFilter + "&" + "includePrerelease=" + includePrerelease.ToString()
                };
            return GetResults(apiEndpointUri.Uri);
        }

        private static IEnumerable<NuGetVersion> GetPackageversionsFromHttpSourceRepository(IPackageRepository packageRepository, string packageId, string versionPrefix, bool includePrerelease)
        {
            var packageSourceUri = new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/", packageRepository.Source.TrimEnd('/')));
            var apiEndpointUri = new UriBuilder(new Uri(packageSourceUri, @"package-versions/" + packageId))
                {
                    Query = "includePrerelease=" + includePrerelease.ToString()
                };
            var versions = GetResults(apiEndpointUri.Uri).ToList();
            versions = versions.Where(item => item.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            return versions.Select(item => NuGetVersion.Parse(item));
        }

        private static IEnumerable<string> GetPackageIdsFromLocalPackageRepository(IPackageRepository packageRepository, string searchFilter, bool includePrerelease)
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
        }

        protected IEnumerable<NuGetVersion> GetPackageVersionsFromLocalPackageRepository(IPackageRepository packageRepository, string packageId, string versionPrefix, bool includePrerelease)
        {
            var packages = packageRepository.GetPackages().Where(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));

            if (!includePrerelease)
            {
                packages = packages.Where(p => p.IsReleaseVersion());
            }

            var versions = packages.Select(p => p.Version.ToString()).ToList();
            versions = versions.Where(item => item.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            return versions.Select(item => NuGetVersion.Parse(item));
        }

        private static IEnumerable<string> GetResults(Uri apiEndpointUri)
        {
            var jsonSerializer = new DataContractJsonSerializer(typeof(string[]));
            var httpClient = new HttpClient(apiEndpointUri);
            using (var stream = new MemoryStream())
            {
                httpClient.DownloadData(stream);
                stream.Seek(0, SeekOrigin.Begin);
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
