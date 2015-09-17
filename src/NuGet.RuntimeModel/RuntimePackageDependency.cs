// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.RuntimeModel
{
    public class RuntimePackageDependency : IEquatable<RuntimePackageDependency>
    {
        public string Id { get; }
        public VersionRange VersionRange { get; }

        public RuntimePackageDependency(string id, VersionRange versionRange)
        {
            Id = id;
            VersionRange = versionRange;
        }

        public RuntimePackageDependency Clone()
        {
            return new RuntimePackageDependency(Id, VersionRange);
        }

        public override string ToString()
        {
            return $"{Id} {VersionRange}";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RuntimePackageDependency);
        }

        public override int GetHashCode()
        {
            return HashCodeCombiner.GetHashCode(Id, VersionRange);
        }

        public bool Equals(RuntimePackageDependency other)
        {
            return other != null &&
                string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase) &&
                VersionRange.Equals(other.VersionRange);
        }
    }
}
