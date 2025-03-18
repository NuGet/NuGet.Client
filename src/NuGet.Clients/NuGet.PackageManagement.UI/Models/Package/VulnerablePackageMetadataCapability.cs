// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class VulnerablePackageMetadataCapability : VulnerableCapability
    {
        public VulnerablePackageMetadataCapability(INuGetSearchService nuGetSearchService,
            PackageIdentity packageIdentity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease) :
            base(GetVulnerabilitiesFactory(nuGetSearchService, packageIdentity, packageSources, includePrerelease))
        {
            if (nuGetSearchService == null)
            {
                throw new ArgumentNullException(nameof(nuGetSearchService));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }
        }

        private static Func<Task<IReadOnlyList<PackageVulnerabilityMetadataContextInfo>>> GetVulnerabilitiesFactory(INuGetSearchService nuGetSearchService,
            PackageIdentity packageIdentity,
            IReadOnlyCollection<PackageSourceContextInfo> packageSources,
            bool includePrerelease)
        {
            return new Func<Task<IReadOnlyList<PackageVulnerabilityMetadataContextInfo>>>(async () =>
            {
                var vulnerabilities = await nuGetSearchService.GetPackageMetadataAsync(packageIdentity, packageSources, includePrerelease, CancellationToken.None);
                return vulnerabilities.Item1.Vulnerabilities.ToList();
            });
        }
    }
}
