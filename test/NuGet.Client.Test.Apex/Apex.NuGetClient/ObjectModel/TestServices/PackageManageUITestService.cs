using System.ComponentModel.Composition;
using Apex.NuGetClient.Host;
using Microsoft.Test.Apex.Hosts;

namespace Apex.NuGetClient.ObjectModel.TestServices
{
    /// <summary>
    /// Base class for singleton Package Manage UI test service
    /// </summary>
    [CreateInProcess(typeof(PackageManageUIHost))]
    [InheritedExport(typeof(PackageManageUITestService))]
    public class PackageManageUITestService : MarshallableApexObject
    {
        /// <summary>
        /// Gets the lease type used to configure lease times for remoting of this instance.
        /// </summary>
        protected override MarshallableApexObjectLeaseType LeaseType
        {
            get
            {
                return MarshallableApexObjectLeaseType.Singleton;
            }
        }

        /// <summary>
        /// Initialization method called before Initialize.
        /// </summary>
        protected override void PreInitialize()
        {
            // Singletons are initialized with null.
            this.Initialize(null);
        }
    }
}
