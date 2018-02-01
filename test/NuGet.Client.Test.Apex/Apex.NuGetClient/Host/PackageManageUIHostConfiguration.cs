using System;
using System.Collections.Generic;
using Microsoft.Test.Apex;

namespace Apex.NuGetClient.Host
{
    /// <summary>
    /// Host configuration for PackageManange UI
    /// </summary>
    public class PackageManageUIHostConfiguration : AppContainerUIHostConfiguration
    {
        protected static readonly TimeSpan packageManageDefaultRemoteObjectLeaseTime = TimeSpan.FromSeconds(2);
        protected static readonly TimeSpan packageManageRemoteSingletonObjectLeaseTime = TimeSpan.Zero;

        public PackageManageUIHostConfiguration()
            : base()
        {
            this.RemoteObjectLeaseTime = PackageManageUIHostConfiguration.packageManageDefaultRemoteObjectLeaseTime;
            this.RemoteSingletonObjectLeaseTime = PackageManageUIHostConfiguration.packageManageRemoteSingletonObjectLeaseTime;

            this.InProcessHostConstraints = new List<ITypeConstraint>
            {
                new NuGetClientInProcessTypeConstraint(typeof(PackageManageUIHost))
            };
        }


        /// <summary>
        /// Host PID(e.g. devenv, blend)
        /// </summary>
        public int PackageManageUIHostProcessId { get; set; }

        public string LocalDumpStorageDirectory { get; set; }

        /// <summary>
        /// Gets the host type that this configuration targets.
        /// </summary>
        protected override Type DefaultHostType
        {
            get
            {
                return typeof(PackageManageUIHost);
            }
        }
    }
}
