// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class DeprecationCapability : IDeprecated
    {
        private static readonly string StarAll = VersionRangeFormatter.Instance.Format("p", VersionRange.Parse("*"), VersionRangeFormatter.Instance);
        private static readonly string StarAllFloating = VersionRangeFormatter.Instance.Format("p", VersionRange.Parse("*-*"), VersionRangeFormatter.Instance);

        public DeprecationCapability(PackageDeprecationMetadataContextInfo deprecatedInfo)
        {
            _deprecated = deprecatedInfo;
        }

        private PackageDeprecationMetadataContextInfo _deprecated;
        public PackageDeprecationMetadataContextInfo DeprecationMetadata
        {
            get => _deprecated;
            private set => _deprecated = value;
        }

        public bool IsDeprecated => DeprecationMetadata != null;
        public string? AlternatePackageText
        {
            get
            {
                if (DeprecationMetadata.AlternatePackage == null)
                {
                    return null;
                }

                // pretty print
                string versionString = VersionRangeFormatter.Instance.Format("p", DeprecationMetadata.AlternatePackage.VersionRange, VersionRangeFormatter.Instance);

                if (StarAll.Equals(versionString, StringComparison.InvariantCultureIgnoreCase) || StarAllFloating.Equals(versionString, StringComparison.InvariantCultureIgnoreCase))
                {
                    return DeprecationMetadata.AlternatePackage.PackageId;
                }

                return $"{DeprecationMetadata.AlternatePackage.PackageId} {versionString}";
            }
        }

        public string PackageDeprecationReasons
        {
            get
            {
                if (DeprecationMetadata.Reasons == null || !DeprecationMetadata.Reasons.Any())
                {
                    return Resources.Label_DeprecationReasons_Unknown;
                }
                else if (DeprecationMetadata.Reasons.Contains("CriticalBugs", StringComparer.OrdinalIgnoreCase))
                {
                    if (DeprecationMetadata.Reasons.Contains("Legacy", StringComparer.OrdinalIgnoreCase))
                    {
                        return Resources.Label_DeprecationReasons_LegacyAndCriticalBugs;
                    }
                    else
                    {
                        return Resources.Label_DeprecationReasons_CriticalBugs;
                    }
                }
                else if (DeprecationMetadata.Reasons.Contains("Legacy", StringComparer.OrdinalIgnoreCase))
                {
                    return Resources.Label_DeprecationReasons_Legacy;
                }
                else
                {
                    return Resources.Label_DeprecationReasons_Unknown;
                }
            }
        }
    }
}
