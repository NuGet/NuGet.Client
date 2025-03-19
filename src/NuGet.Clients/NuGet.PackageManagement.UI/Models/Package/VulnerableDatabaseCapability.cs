using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI
{
    public class VulnerableDatabaseCapability : VulnerableCapabilityBase
    {
        private IPackageVulnerabilityService _vulnerabilityService;
        private PackageIdentity _packageIdentity;

        public VulnerableDatabaseCapability(IPackageVulnerabilityService vulnerabilityService, PackageIdentity packageIdentity)
        {
            _vulnerabilityService = vulnerabilityService ?? throw new ArgumentNullException(nameof(vulnerabilityService));
            _packageIdentity = packageIdentity ?? throw new ArgumentNullException(nameof(packageIdentity));
        }

        public override async Task PopulateDataAsync(CancellationToken cancellationToken)
        {
            Vulnerabilities = await _vulnerabilityService.GetVulnerabilityInfoAsync(_packageIdentity, cancellationToken);
        }
    }
}
