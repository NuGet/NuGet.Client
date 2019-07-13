// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public class NuGetVersionWithDeprecationInfo : IComparable<NuGetVersionWithDeprecationInfo>
    {
        public NuGetVersionWithDeprecationInfo(NuGetVersion version, bool isDeprecated)
        {
            Version = version;
            IsDeprecated = isDeprecated;
        }

        public NuGetVersion Version { get; private set; }

        public bool IsDeprecated { get; private set; }

        public int CompareTo(NuGetVersionWithDeprecationInfo other)
        {
            return Version.CompareTo(other.Version);
        }

        public override string ToString()
        {
            if (IsDeprecated)
            {
                return Version.ToString() + " (D)";
            }

            return Version.ToString();
        }
    }
}