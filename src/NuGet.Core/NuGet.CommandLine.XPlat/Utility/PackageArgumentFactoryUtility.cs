// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.CommandLine.XPlat.Commands.Package;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal static class PackageArgumentFactoryUtility
    {
        public static PackageArgument<NuGetVersion> CreateForVersion(string id, NuGetVersion version)
        {
            return new PackageArgument<NuGetVersion>(new NuGetVersionEqualityComparer())
            {
                Id = id,
                Version = version
            };
        }

        public static PackageArgument<VersionRange> CreateForVersionRange(string id, VersionRange version)
        {
            return new PackageArgument<VersionRange>(new VersionRangeEqualityComparer())
            {
                Id = id,
                Version = version
            };
        }
    }
}
