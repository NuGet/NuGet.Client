// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Commands.Package
{
    internal record NuGetPackage : IEqualityComparer<NuGetPackage>
    {
        public required string Id { get; init; }
        public VersionRange? VersionRange { get; init; }

        public NuGetVersion? ExactVersion { get; init; }

        internal static IReadOnlyList<NuGetPackage> ParsePackagesWithVersionRange(ArgumentResult result)
        {
            return ParsePackages(result, exactVersion: false);
        }

        internal static IReadOnlyList<NuGetPackage> ParsePackagesWithExactVersions(ArgumentResult result)
        {
            return ParsePackages(result, exactVersion: true);
        }

        private static IReadOnlyList<NuGetPackage> ParsePackages(ArgumentResult result, bool exactVersion)
        {
            if (result.Tokens.Count == 0)
            {
                return [];
            }

            List<NuGetPackage> packages = new List<NuGetPackage>(result.Tokens.Count);

            foreach (var token in result.Tokens)
            {
                string? packageId;
                VersionRange? newVersionRange = null;
                NuGetVersion? newExactVersion = null;

                int separatorIndex = token.Value.IndexOf('@');

                if (separatorIndex < 0)
                {
                    packageId = token.Value;
                }
                else
                {
                    packageId = token.Value.Substring(0, separatorIndex);
                    string versionString = token.Value.Substring(separatorIndex + 1);

                    if (string.IsNullOrEmpty(versionString))
                    {
                        result.AddError(Messages.Error_MissingVersion(token.Value));
                        return [];
                    }

                    if (exactVersion)
                    {
                        if (!NuGetVersion.TryParse(versionString, out newExactVersion))
                        {
                            result.AddError(Messages.Error_InvalidVersion(versionString));
                            return [];
                        }
                    }
                    else
                    {
                        if (!VersionRange.TryParse(versionString, out newVersionRange))
                        {
                            result.AddError(Messages.Error_InvalidVersionRange(versionString));
                            return [];
                        }
                    }
                }

                NuGetPackage package = new NuGetPackage
                {
                    Id = packageId,
                    VersionRange = newVersionRange,
                    ExactVersion = newExactVersion
                };

                packages.Add(package);
            }

            return packages;
        }

        public bool Equals(NuGetPackage? x, NuGetPackage? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (!x.Id.Equals(y.Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return VersionRangeComparer.Default.Equals(x.VersionRange, y.VersionRange) &&
                VersionComparer.Compare(x.ExactVersion, y.ExactVersion, VersionComparison.Default) == 0;
        }

        public int GetHashCode([DisallowNull] NuGetPackage obj)
        {
            HashCode hash = new HashCode();
            hash.Add(obj.Id, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.VersionRange);
            hash.Add(obj.ExactVersion);
            return hash.ToHashCode();
        }
    }
}
