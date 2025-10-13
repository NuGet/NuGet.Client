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
    internal record NuGetPackageWithNuGetVersion : IEqualityComparer<NuGetPackageWithNuGetVersion>
    {
        public required string Id { get; init; }

        public NuGetVersion? NuGetVersion { get; init; }

        internal static IReadOnlyList<NuGetPackageWithNuGetVersion> Parse(ArgumentResult result)
        {
            if (result.Tokens.Count == 0)
            {
                return [];
            }

            var packages = new List<NuGetPackageWithNuGetVersion>(result.Tokens.Count);

            foreach (var token in result.Tokens)
            {
                string? packageId;
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

                    if (!NuGetVersion.TryParse(versionString, out newExactVersion))
                    {
                        result.AddError(Messages.Error_InvalidVersion(versionString));
                        return [];
                    }
                }

                var package = new NuGetPackageWithNuGetVersion
                {
                    Id = packageId,
                    NuGetVersion = newExactVersion
                };

                packages.Add(package);
            }

            return packages;
        }

        public bool Equals(NuGetPackageWithNuGetVersion? x, NuGetPackageWithNuGetVersion? y)
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

            return VersionComparer.Compare(x.NuGetVersion, y.NuGetVersion, VersionComparison.Default) == 0;
        }

        public int GetHashCode([DisallowNull] NuGetPackageWithNuGetVersion obj)
        {
            HashCode hash = new HashCode();
            hash.Add(obj.Id, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.NuGetVersion);
            return hash.ToHashCode();
        }
    }
}
