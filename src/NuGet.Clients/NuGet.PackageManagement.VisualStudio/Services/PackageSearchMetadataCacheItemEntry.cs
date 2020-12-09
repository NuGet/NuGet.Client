// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    internal sealed class PackageSearchMetadataCacheItemEntry
    {
        private readonly IPackageSearchMetadata _packageSearchMetadata;
        private readonly IPackageMetadataProvider _packageMetadataProvider;

        public PackageSearchMetadataCacheItemEntry(IPackageSearchMetadata packageSearchMetadata, IPackageMetadataProvider packageMetadataProvider)
        {
            _packageSearchMetadata = packageSearchMetadata;
            _packageMetadataProvider = packageMetadataProvider;
        }

        public ValueTask<PackageDeprecationMetadataContextInfo?> PackageDeprecationMetadataContextInfo => GetPackageDeprecationMetadataContextInfoAsync();
        public ValueTask<PackageSearchMetadataContextInfo> DetailedPackageSearchMetadataContextInfo => GetDetailedPackageSearchMetadataContextInfoAsync();

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
