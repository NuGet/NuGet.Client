// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    internal sealed class PackageSearchMetadataCacheObject
    {
        private readonly IPackageSearchMetadata _packageSearchMetadata;
        private readonly IPackageMetadataProvider _packageMetadataProvider;

        public PackageSearchMetadataCacheObject(IPackageSearchMetadata packageSearchMetadata, IPackageMetadataProvider packageMetadataProvider)
        {
            _packageSearchMetadata = packageSearchMetadata;
            _packageMetadataProvider = packageMetadataProvider;
            AllVersionsContextInfo = GetVersionInfoContextInfoAsync();
            PackageDeprecrationMetadataContextInfo = GetPackageDeprecationMetadataContextInfoAsync();
            DetailedPackageSearchMetadataContextInfo = GetDetailedPackageSearchMetadataContextInfoAsync();
        }

        public Task<IReadOnlyCollection<VersionInfoContextInfo>> AllVersionsContextInfo { get; }
        public Task<PackageDeprecationMetadataContextInfo?> PackageDeprecrationMetadataContextInfo { get; }
        public Task<PackageSearchMetadataContextInfo> DetailedPackageSearchMetadataContextInfo { get; }

        public static string GetCacheId(string packageId, bool includePrerelease, IReadOnlyCollection<PackageSourceContextInfo> packageSources)
        {
            string packageSourcesString = string.Join(" ", packageSources.Select(ps => ps.Name));
            return string.Concat(packageId, ":", includePrerelease, " - ", packageSourcesString);
        }

        private async Task<IReadOnlyCollection<VersionInfoContextInfo>> GetVersionInfoContextInfoAsync()
        {
            IEnumerable<VersionInfo> versions = await _packageSearchMetadata.GetVersionsAsync();
            IEnumerable<Task<VersionInfoContextInfo>> versionContextInfoTasks = versions.Select(async v => await VersionInfoContextInfo.CreateAsync(v));
            return await Task.WhenAll(versionContextInfoTasks);
        }

        private async Task<PackageDeprecationMetadataContextInfo?> GetPackageDeprecationMetadataContextInfoAsync()
        {
            PackageDeprecationMetadata? deprecationMetadata = await _packageSearchMetadata.GetDeprecationMetadataAsync();
            if (deprecationMetadata == null)
            {
                return null;
            }
            return PackageDeprecationMetadataContextInfo.Create(deprecationMetadata);
        }

        private async Task<PackageSearchMetadataContextInfo> GetDetailedPackageSearchMetadataContextInfoAsync()
        {
            IPackageSearchMetadata detailedMetadata = await _packageMetadataProvider.GetPackageMetadataAsync(_packageSearchMetadata.Identity, includePrerelease: true, CancellationToken.None);
            return PackageSearchMetadataContextInfo.Create(detailedMetadata);
        }
    }
}
