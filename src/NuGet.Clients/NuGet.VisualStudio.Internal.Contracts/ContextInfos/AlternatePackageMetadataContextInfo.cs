// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.VisualStudio.Internal.Contracts
{
    public sealed class AlternatePackageMetadataContextInfo
    {
        public AlternatePackageMetadataContextInfo(string packageId, VersionRange range)
        {
            PackageId = packageId;
            Range = range;
        }

        public string PackageId { get; }
        public VersionRange Range { get; }

        public static AlternatePackageMetadataContextInfo Create(AlternatePackageMetadata alternatePackageMetadata)
        {
            return new AlternatePackageMetadataContextInfo(alternatePackageMetadata.PackageId, alternatePackageMetadata.Range);
        }
    }
}
