// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Protocol;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class VulnerableCapability : IVulnerableCapable
    {
        private readonly AsyncLazy<IReadOnlyList<PackageVulnerabilityMetadataContextInfo>> _vulnerabilitiesLazy;

        public VulnerableCapability(Func<Task<IReadOnlyList<PackageVulnerabilityMetadataContextInfo>>> vulnerabilitiesFactory)
        {
            if (vulnerabilitiesFactory == null)
            {
                throw new ArgumentNullException(nameof(vulnerabilitiesFactory));
            }

            _vulnerabilitiesLazy = new AsyncLazy<IReadOnlyList<PackageVulnerabilityMetadataContextInfo>>(async () =>
            {
                var vulnerabilities = await vulnerabilitiesFactory();
                List<PackageVulnerabilityMetadataContextInfo> sortedList = [.. vulnerabilities];
                // Sort the list in descending order.
                sortedList.Sort((b, a) => a.Severity.CompareTo(b.Severity));
                return sortedList.AsReadOnly();
            }, NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public IReadOnlyList<PackageVulnerabilityMetadataContextInfo> Vulnerabilities
        {
            get => NuGetUIThreadHelper.JoinableTaskFactory.Run(_vulnerabilitiesLazy.GetValueAsync);
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
    }
}
