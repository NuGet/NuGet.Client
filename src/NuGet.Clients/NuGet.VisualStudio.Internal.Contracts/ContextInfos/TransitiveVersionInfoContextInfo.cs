// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public class TransitiveVersionInfoContextInfo : VersionInfoContextInfo
    {
        public TransitiveVersionInfoContextInfo(NuGetVersion version, long? downloadCount) : base(version, downloadCount)
        {
        }

        public override PackageDeprecationMetadataContextInfo? PackageDeprecationMetadata { get => null; internal set { } }

        public static TransitiveVersionInfoContextInfo Create(VersionInfo version)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            var versionContextInfo = new TransitiveVersionInfoContextInfo(version.Version, version.DownloadCount);

            if (version.PackageSearchMetadata != null)
            {
                TransitivePackageSearchMetadata tpsm;
                if (version.PackageSearchMetadata is TransitivePackageSearchMetadata)
                {
                    tpsm = (TransitivePackageSearchMetadata)version.PackageSearchMetadata;
                }
                else
                {
                    tpsm = new TransitivePackageSearchMetadata(version.PackageSearchMetadata, Array.Empty<PackageIdentity>());
                }
                versionContextInfo.PackageSearchMetadata = PackageSearchMetadataContextInfo.Create(tpsm);
            }

            return versionContextInfo;
        }
    }
}
