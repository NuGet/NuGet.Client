// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Licenses;
using NuGet.Shared;

namespace NuGet.Packaging
{
    public class LicenseMetadata : IEquatable<LicenseMetadata>
    {
        public static Version EmptyVersion = new Version(1, 0, 0);
        public static Version CurrentVersion = new Version(1, 0, 0);
        public static Uri DeprecateUrl = new Uri("https://aka.ms/deprecateLicenseUrl");

        public LicenseType? Type { get; }
        public string License { get; }
        public NuGetLicenseExpression LicenseExpression { get; }
        public Version Version { get; }

        public LicenseMetadata(LicenseType? type, string license, NuGetLicenseExpression expression, Version version)
        {
            Type = type;
            License = license ?? throw new ArgumentNullException(nameof(license));
            LicenseExpression = expression;
            Version = version ?? EmptyVersion;
        }

        public bool Equals(LicenseMetadata other)
        {
            return Equals(other);
        }

        public override bool Equals(object obj)
        {
            var metadata = obj as LicenseMetadata;
            return metadata != null &&
                   Type == metadata.Type &&
                   License.Equals(metadata.License) &&
                   LicenseExpression.Equals(metadata.License) &&
                   Version == metadata.Version;
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(Type);
            combiner.AddObject(License);
            combiner.AddObject(LicenseExpression);
            combiner.AddObject(Version);

            return combiner.CombinedHash;
        }
    }

    public enum LicenseType
    {
        File,
        Expression,
    }
}
