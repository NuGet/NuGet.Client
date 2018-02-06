using System.ComponentModel.Composition;
using Microsoft.Test.Apex.Hosts;

namespace Apex.NuGetClient.TestServices
{
    /// <summary>
    /// Base Class for test services
    /// </summary>
    [InheritedExport(typeof(NuGetClientTestService))]
    public class NuGetClientTestService : MarshallableApexObject
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
