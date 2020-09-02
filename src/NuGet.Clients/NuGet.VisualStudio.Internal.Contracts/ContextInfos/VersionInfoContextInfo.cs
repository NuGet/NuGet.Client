// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Threading.Tasks;
using MessagePack;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    [MessagePackObject(keyAsPropertyName: true)]
    public sealed class VersionInfoContextInfo
    {
        public VersionInfoContextInfo(NuGetVersion version)
        {
            Version = version;
        }

        public NuGetVersion Version { get; set; }
        public long DownloadCount { get; set; }

        public PackageDeprecationMetadataContextInfo? PackageDeprecationMetadata { get; set; }

        public PackageSearchMetadataContextInfo? PackageSearchMetadata { get; set; }

        public static async ValueTask<VersionInfoContextInfo> CreateAsync(VersionInfo versionInfo)
        {
            var versionContextInfo = new VersionInfoContextInfo(versionInfo.Version)
            {
                DownloadCount = versionInfo.DownloadCount.GetValueOrDefault(),
            };

            if (versionInfo.PackageSearchMetadata != null)
            {
                var packageSearchMetadataContextInfo = PackageSearchMetadataContextInfo.Create(versionInfo.PackageSearchMetadata);
                versionContextInfo.PackageSearchMetadata = packageSearchMetadataContextInfo;
                PackageDeprecationMetadata? packageDeprecationMetadata = await versionInfo.PackageSearchMetadata.GetDeprecationMetadataAsync();
                if (packageDeprecationMetadata != null)
                {
                    versionContextInfo.PackageDeprecationMetadata = PackageDeprecationMetadataContextInfo.Create(packageDeprecationMetadata);
                }
            }

            return versionContextInfo;
        }
    }
}
