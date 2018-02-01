using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using Apex.NuGetClient.Host;
using Microsoft.Test.Apex;
using Microsoft.Test.Apex.Hosts;
using Microsoft.Test.Apex.Services;
using Microsoft.Test.Apex.Services.Remoting;

namespace Apex.NuGetClient.PackageManageUI
{
    [ProvidesHostExtension(typeof(ExternalUIHost))]
    [Export(typeof(IRemotableService))]
    [Export(typeof(IPackageManageUIService))]
    public class PackageManageUIService : MarshalByRefObject, IPackageManageUIService, IRemotableService
    {
        /// <summary>
        /// Collection of Test Contract assemblies for the NuGetClient
        /// </summary>
        private IList<string> nugetClientTestContracts = new List<string>
        {
            "NuGetClientTestContracts.dll"
        };

        public static string NuGetClientTestBinariesPath
        {
            get
            {
                return Path.Combine(Environment.GetEnvironmentVariable("TEMP",EnvironmentVariableTarget.User), "NuGetTest", "NuGetClientTestBinariesPath");
            }
        }
        /// <summary>
        /// Gets the contract (interface) type to register the service for remoting as. 
        /// </summary>
        Type IRemotableService.RemoteContract
        {
            get
            {
                return typeof(IPackageManageUIService);
            }
        }

        /// <summary>
        /// Gets the synchronization service.
        /// </summary>
        public ISynchronizationService Synchronization
        {
            get
            {
                return this.LazySynchronizationService.Value;
            }
        }


        /// <summary>
        /// Gets or sets the synchronization service (lazy import).
        /// </summary>
        [Import(AllowDefault = true)]
        private Lazy<ISynchronizationService> LazySynchronizationService
        {
            get;
            set;
        }

        public override object InitializeLifetimeService()
        {
            // infinite lifetime
            return null;
        }

        /// <summary>
        /// Get NuGet Package manager host for given project
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="packageManageUIHostProcess"></param>
        /// <returns></returns>
        public PackageManageUIHost GetPackageManageUIHost(string projectName, Process packageManageUIHostProcess)
        {
            Require.NotEmpty(projectName, "projectName");

            PackageManageUIHostConfiguration config = new PackageManageUIHostConfiguration()
            {
                LocalDumpStorageDirectory = Environment.GetEnvironmentVariable("WOR") ?? Environment.GetEnvironmentVariable("TEMP"),
                PackageManageUIHostProcessId = packageManageUIHostProcess.Id //devenv
            };

            PackageManageUIHost newHost = Microsoft.Test.Apex.Operations.Current.CreateAndStartHost<PackageManageUIHost>();

            return newHost;
        }
    }
}
