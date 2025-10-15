// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

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
                Messages.Error_InvalidVersion,
                (IEqualityComparer<NuGetVersion?>)VersionComparer.Default);
        }

        public static IReadOnlyList<PackageArgument<VersionRange>> ParseWithVersionRange(ArgumentResult result)
        {
            return PackageArgument<VersionRange>.Parse(
                result,
                VersionRange.TryParse,
                Messages.Error_InvalidVersionRange,
                (IEqualityComparer<VersionRange?>)VersionRangeComparer.Default);
        }
    }
}
