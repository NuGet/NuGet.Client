// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;
using NuGet.Protocol.Model;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class DeprecationCapability : IDeprecation
    {
        public DeprecationCapability(PackageDeprecationMetadataContextInfo deprecatedInfo)
        {
            DeprecationMetadata = deprecatedInfo;
        }

        public PackageDeprecationMetadataContextInfo DeprecationMetadata { get; private set; }

        public bool IsDeprecated => DeprecationMetadata != null;

        public PackageDeprecationReason PackageDeprecationReasons
        {
            get
            {
                if (DeprecationMetadata.Reasons == null || !DeprecationMetadata.Reasons.Any())
                {
                    return PackageDeprecationReason.Unknown;
                }

                bool hasCriticalBugs = false;
                bool hasLegacy = false;

                foreach (var reason in DeprecationMetadata.Reasons)
                {
                    if (string.Equals(reason, "CriticalBugs", StringComparison.OrdinalIgnoreCase))
                    {
                        hasCriticalBugs = true;
                    }
                    else if (string.Equals(reason, "Legacy", StringComparison.OrdinalIgnoreCase))
                    {
                        hasLegacy = true;
                    }
                }

                if (hasCriticalBugs && hasLegacy)
                {
                    return PackageDeprecationReason.LegacyAndCriticalBugs;
                }

                if (hasCriticalBugs)
                {
                    return PackageDeprecationReason.CriticalBugs;
                }

                if (hasLegacy)
                {
                    return PackageDeprecationReason.Legacy;
                }

                return PackageDeprecationReason.Unknown;
            }
        }
    }
}
