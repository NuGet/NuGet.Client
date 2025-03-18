using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class VulnerableDatabaseCapability : VulnerableCapability
    {

        public VulnerableDatabaseCapability(IPackageVulnerabilityService vulnerabilityService, PackageIdentity packageIdentity)
            : base(GetVulnerabilitiesFactory(vulnerabilityService, packageIdentity))
        {
            if (vulnerabilityService == null)
            {
                throw new ArgumentNullException(nameof(vulnerabilityService));
            }

            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }
        }

        private static Func<Task<IReadOnlyList<PackageVulnerabilityMetadataContextInfo>>> GetVulnerabilitiesFactory(IPackageVulnerabilityService vulnerabilityService, PackageIdentity packageIdentity)
        {
            return async () =>
            {
                var vulnerabilities = await vulnerabilityService.GetVulnerabilityInfoAsync(packageIdentity, CancellationToken.None);
                return vulnerabilities;
            };
        }
    }
}
