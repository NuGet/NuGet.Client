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

        public PackageDeprecationMetadataContextInfo? DeprecationMetadata { get; }

        public bool IsDeprecated => DeprecationMetadata != null;

        public PackageDeprecationReasonEnum PackageDeprecationReasons
        {
            get
            {
                if (DeprecationMetadata?.Reasons == null || !DeprecationMetadata.Reasons.Any())
                {
                    return PackageDeprecationReasonEnum.Unknown;
                }

                bool hasCriticalBugs = false;
                bool hasLegacy = false;

                foreach (var reason in DeprecationMetadata.Reasons)
                {
                    if (string.Equals(reason, PackageDeprecationReason.CriticalBugs, StringComparison.OrdinalIgnoreCase))
                    {
                        hasCriticalBugs = true;
                    }
                    else if (string.Equals(reason, PackageDeprecationReason.Legacy, StringComparison.OrdinalIgnoreCase))
                    {
                        hasLegacy = true;
                    }
                }

                if (hasCriticalBugs && hasLegacy)
                {
                    return PackageDeprecationReasonEnum.LegacyAndCriticalBugs;
                }

                if (hasCriticalBugs)
                {
                    return PackageDeprecationReasonEnum.CriticalBugs;
                }

                if (hasLegacy)
                {
                    return PackageDeprecationReasonEnum.Legacy;
                }

                return PackageDeprecationReasonEnum.Unknown;
            }
        }
    }
}
