// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class VulnerableCapability : IVulnerable
    {
        private IEnumerable<PackageVulnerabilityMetadataContextInfo> _vulnerabilities = [];

        public IEnumerable<PackageVulnerabilityMetadataContextInfo> Vulnerabilities
        {
            get => _vulnerabilities;
            private set => _vulnerabilities = value.OrderByDescending(v => v?.Severity ?? -1);
        }

        public bool IsVulnerable => Vulnerabilities.Any(v => v != null);

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity
        {
            get
            {
                // Vulnerabilities are ordered so the first element is always the highest severity
                int? severity = Vulnerabilities.FirstOrDefault()?.Severity;
                if (severity != null && Enum.IsDefined(typeof(PackageVulnerabilitySeverity), severity))
                {
                    return (PackageVulnerabilitySeverity)severity;
                }
                else
                {
                    return PackageVulnerabilitySeverity.Unknown;
                }
            }
        }

        public VulnerableCapability(IEnumerable<PackageVulnerabilityMetadataContextInfo> vulnerabilities)
        {
            Vulnerabilities = vulnerabilities ?? throw new ArgumentNullException(nameof(vulnerabilities));
        }
    }
}
