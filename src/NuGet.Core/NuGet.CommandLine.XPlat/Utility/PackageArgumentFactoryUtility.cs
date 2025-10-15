// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using NuGet.CommandLine.XPlat.Commands.Package;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal static class PackageArgumentFactoryUtility
    {
        public static PackageArgument<NuGetVersion> CreateForVersion(string id, NuGetVersion version)
        {
            return new PackageArgument<NuGetVersion>((IEqualityComparer<NuGetVersion?>)VersionComparer.Default)
            {
                Id = id,
                Version = version
            };
        }

        public static PackageArgument<VersionRange> CreateForVersionRange(string id, VersionRange version)
        {
            return new PackageArgument<VersionRange>((IEqualityComparer<VersionRange?>)VersionRangeComparer.Default)
            {
                Id = id,
                Version = version
            };
        }
    }
}
