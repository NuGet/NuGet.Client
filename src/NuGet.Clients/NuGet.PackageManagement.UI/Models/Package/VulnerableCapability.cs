// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public abstract class VulnerableCapability : IVulnerableCapable
    {
        protected IReadOnlyList<PackageVulnerabilityMetadataContextInfo>? _vulnerabilities;

        internal VulnerableCapability()
        {

        }

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo>? Vulnerabilities => _vulnerabilities;

        public bool IsVulnerable => Vulnerabilities?.Count > 0;

        public PackageVulnerabilitySeverity VulnerabilityMaxSeverity
        {
            get
            {
                if (Vulnerabilities is null)
                {
                    throw new InvalidOperationException("Vulnerabilities is null");

                }
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

        public abstract Task RefreshAsync(CancellationToken cancellationToken);
    }
}
