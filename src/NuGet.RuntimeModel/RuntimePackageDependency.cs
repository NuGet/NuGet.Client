// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.RuntimeModel
{
    public class RuntimePackageDependency
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
    }
}
