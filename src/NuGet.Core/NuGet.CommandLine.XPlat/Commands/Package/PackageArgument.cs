// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;

namespace NuGet.CommandLine.XPlat.Commands.Package
{
    internal record PackageArgument<TVersion> : IEqualityComparer<PackageArgument<TVersion>>
    {
        public required string Id { get; init; }
        public required TVersion? Version { get; init; }
        internal delegate bool TryParseVersion(string value, out TVersion version);
        private readonly IEqualityComparer<TVersion?> _versionComparer;

        public PackageArgument(IEqualityComparer<TVersion?> versionComparer)
        {
            _versionComparer = versionComparer;
        }

        public static IReadOnlyList<PackageArgument<TVersion>> Parse(
            ArgumentResult result,
            TryParseVersion parseVersion,
            Func<string, string> getInvalidVersionMessage,
            IEqualityComparer<TVersion?> versionComparer)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(parseVersion);
            ArgumentNullException.ThrowIfNull(getInvalidVersionMessage);

            if (result.Tokens.Count == 0)
            {
                return [];
            }

            List<PackageArgument<TVersion>> packages = new List<PackageArgument<TVersion>>(result.Tokens.Count);

            foreach (var token in result.Tokens)
            {
                string? packageId;
                TVersion? newVersion;
                int separatorIndex = token.Value.IndexOf('@');

                if (separatorIndex < 0)
                {
                    packageId = token.Value;
                    newVersion = default;
                }
                else
                {
                    packageId = token.Value[..separatorIndex];
                    string versionString = token.Value[(separatorIndex + 1)..];

                    if (string.IsNullOrEmpty(versionString))
                    {
                        result.AddError(Messages.Error_MissingVersion(token.Value));
                        return [];
                    }

                    if (!parseVersion(versionString, out newVersion))
                    {
                        result.AddError(getInvalidVersionMessage(versionString));
                        return [];
                    }
                }

                var package = new PackageArgument<TVersion>(versionComparer)
                {
                    Id = packageId,
                    Version = newVersion
                };
                packages.Add(package);
            }

            return packages;
        }

        public bool Equals(PackageArgument<TVersion>? x, PackageArgument<TVersion>? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (!x.Id!.Equals(y.Id, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return _versionComparer.Equals(x.Version, y.Version);
        }

        public int GetHashCode([DisallowNull] PackageArgument<TVersion> obj)
        {
            HashCode hash = new HashCode();
            hash.Add(obj.Id);
            hash.Add(obj.Version);
            return hash.ToHashCode();
        }
    }
}
