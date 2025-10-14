// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using NuGet.CommandLine.XPlat.Commands.Package;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Utility
{
    internal static class PackageArgumentParserUtility
    {
        public static IReadOnlyList<PackageArgument<NuGetVersion>> ParseWithVersion(ArgumentResult result)
        {
            return PackageArgument<NuGetVersion>.Parse(
                result,
                NuGetVersion.TryParse,
                versionString => Messages.Error_InvalidVersion(versionString),
                versionComparer: new NuGetVersionEqualityComparer());
        }

        public static IReadOnlyList<PackageArgument<VersionRange>> ParseWithVersionRange(ArgumentResult result)
        {
            return PackageArgument<VersionRange>.Parse(
                result,
                VersionRange.TryParse,
                versionString => Messages.Error_InvalidVersionRange(versionString),
                versionComparer: new VersionRangeEqualityComparer());
        }
    }

#nullable enable

    internal sealed class NuGetVersionEqualityComparer : IEqualityComparer<NuGetVersion?>
    {
        public bool Equals(NuGetVersion? x, NuGetVersion? y)
        {
            return VersionComparer.Compare(x, y, VersionComparison.Default) == 0;
        }

        public int GetHashCode(NuGetVersion? obj)
        {
            return obj != null ? new VersionComparer().GetHashCode(obj) : 0;
        }
    }

    internal sealed class VersionRangeEqualityComparer : IEqualityComparer<VersionRange?>
    {
        public bool Equals(VersionRange? x, VersionRange? y)
        {
            return VersionRangeComparer.Default.Equals(x, y);
        }

        public int GetHashCode(VersionRange? obj)
        {
            return obj != null ? VersionRangeComparer.Default.GetHashCode(obj) : 0;
        }
    }
}
