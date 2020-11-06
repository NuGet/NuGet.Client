// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Shared;
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
            PackageDeprecationMetadataContextInfo = GetPackageDeprecationMetadataContextInfoAsync();
            DetailedPackageSearchMetadataContextInfo = GetDetailedPackageSearchMetadataContextInfoAsync();
        }

        public ValueTask<IReadOnlyCollection<VersionInfoContextInfo>> AllVersionsContextInfo { get; }
        public ValueTask<PackageDeprecationMetadataContextInfo?> PackageDeprecationMetadataContextInfo { get; }
        public ValueTask<PackageSearchMetadataContextInfo> DetailedPackageSearchMetadataContextInfo { get; }

        public static string GetCacheId(string packageId, bool includePrerelease, IReadOnlyCollection<PackageSourceContextInfo> packageSources)
        {
            var hashCodeCombiner = new HashCodeCombiner();
            hashCodeCombiner.AddSequence(packageSources);
            hashCodeCombiner.AddStringIgnoreCase(packageId);
            hashCodeCombiner.AddObject(includePrerelease.GetHashCode());
            return hashCodeCombiner.CombinedHash.ToString(CultureInfo.InvariantCulture);
        }

        private async ValueTask<IReadOnlyCollection<VersionInfoContextInfo>> GetVersionInfoContextInfoAsync()
        {
            IEnumerable<VersionInfo> versions = await _packageSearchMetadata.GetVersionsAsync();
            IEnumerable<Task<VersionInfoContextInfo>> versionContextInfoTasks = versions.Select(async v => await VersionInfoContextInfo.CreateAsync(v));
            return await Task.WhenAll(versionContextInfoTasks);
        }

        private async ValueTask<PackageDeprecationMetadataContextInfo?> GetPackageDeprecationMetadataContextInfoAsync()
        {
            PackageDeprecationMetadata? deprecationMetadata = await _packageSearchMetadata.GetDeprecationMetadataAsync();
            if (deprecationMetadata == null)
            {
                return null;
            }
            return NuGet.VisualStudio.Internal.Contracts.PackageDeprecationMetadataContextInfo.Create(deprecationMetadata);
        }

        private async ValueTask<PackageSearchMetadataContextInfo> GetDetailedPackageSearchMetadataContextInfoAsync()
        {
            IPackageSearchMetadata detailedMetadata = await _packageMetadataProvider.GetPackageMetadataAsync(_packageSearchMetadata.Identity, includePrerelease: true, CancellationToken.None);
            return PackageSearchMetadataContextInfo.Create(detailedMetadata);
        }
    }
}
