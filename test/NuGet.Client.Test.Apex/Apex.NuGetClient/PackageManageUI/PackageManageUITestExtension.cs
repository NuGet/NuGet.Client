using System;
using System.ComponentModel.Composition;
using Apex.NuGetClient.Host;
using Apex.NuGetClient.ObjectModel.TestExtensions;
using Apex.NuGetClient.ObjectModel;
using NuGetClientTestContracts;

namespace Apex.NuGetClient.PackageManageUI
{
    /// <summary>
    /// Test Extension for Package Manager UI 
    /// </summary>
    public class PackageManageUITestExtension : NuGetClientInProcessTestExtension<object, PackageManageUIVerifier>
    {
        private IPackageManageUITestContract packageManageUITestContract;
        public PackageManageUITestExtension(IPackageManageUITestContract testContract)
        {
            packageManageUITestContract = testContract;
        }

        private IPackageManageUITestContract PackageManageUITestContract { get { return this.LazyPackageManageUITestContract.Value; } }

        [Import(AllowDefault = true)]
        private Lazy<IPackageManageUITestContract> LazyPackageManageUITestContract { get; set; }

        [Import(AllowDefault = true)]
        private Lazy<IPackageManageUIService> LazyPackageManageUI { get; set; }

        private IPackageManageUIObjectModel ObjectModel
        {
            get
            {
                return this.PackageManageUIHost.ObjectModel;
            }
        } 
        
        private PackageManageUIHost PackageManageUIHost
        {
            get { return null; }
        }
    }
}
