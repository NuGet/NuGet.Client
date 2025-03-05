// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using NuGet.Protocol;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class VulnerableCapability : IVulnerable
    {
        private IReadOnlyList<PackageVulnerabilityMetadataContextInfo> _vulnerabilities = [];
        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo> Vulnerabilities
        {
            get => _vulnerabilities;
            private set
            {
                List<PackageVulnerabilityMetadataContextInfo> sortedList = [.. value];
                // Sort the list in descending order.
                sortedList.Sort((b, a) => a.Severity.CompareTo(b.Severity));
                _vulnerabilities = sortedList;
            }
        }

        public bool IsVulnerable => Vulnerabilities.Count > 0;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity
        {
            get
            {
                if (!IsVulnerable)
                {
                    throw new InvalidOperationException("Vulnerabilities is empty");
                }

                // Vulnerabilities are ordered on set so the first element is always the highest severity
                int severity = Vulnerabilities[0].Severity;
                if (Enum.IsDefined(typeof(PackageVulnerabilitySeverity), severity))
                {
                    return (PackageVulnerabilitySeverity)severity;
                }
                else
                {
                    return PackageVulnerabilitySeverity.Unknown;
                }
            }
        }

        public VulnerableCapability(List<PackageVulnerabilityMetadataContextInfo> vulnerabilities)
        {
            if (vulnerabilities == null)
            {
                throw new ArgumentNullException(nameof(vulnerabilities));
            }

            Vulnerabilities = vulnerabilities;
        }
    }
}
