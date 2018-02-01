using System.ComponentModel.Composition;
using Apex.NuGetClient.Host;
using Microsoft.Test.Apex.Hosts;
using Microsoft.Test.Apex.Services;

namespace Apex.NuGetClient.ObjectModel
{
    /// <summary>
    /// Factory Service
    /// </summary>
    [Export(typeof(PackageManageUIObjectModelFactoryService))]
    [ProvidesHostExtension(typeof(PackageManageUIHost))]
    internal class PackageManageUIObjectModelFactoryService : FactoryService<PackageManageUIHost, PackageManageUIObjectModel>
    {
        /// <summary>
        /// Gets the display name of the product.
        /// </summary>
        /// <value>The display name of the product.</value>
        protected override string ProductDisplayName
        {
            get
            {
                return "PackageManageUI Object Model";
            }
        }

        /// <summary>
        /// Gets a value indicating the type of composition to invoke on created products. 
        /// </summary>
        protected override FactoryServiceProductComposition ProductComposition
        {
            get
            {
                return FactoryServiceProductComposition.IgnoreImports;
            }
        }
    }
}
