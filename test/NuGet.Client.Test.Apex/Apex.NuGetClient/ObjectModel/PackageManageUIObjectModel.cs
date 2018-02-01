using System;
using System.ComponentModel.Composition;
using Apex.NuGetClient.ObjectModel.TestExtensions;
using Apex.NuGetClient.TestServices;


namespace Apex.NuGetClient.ObjectModel
{
    /// <summary>
    /// This is the NuGet Package Manage UI test extension
    /// </summary>
    [Export(typeof(PackageManageUIObjectModel))]
    public class PackageManageUIObjectModel : PackageManageUITestService, IPackageManageUIObjectModel
    {
        //public PackageManageUIWindowTestExtension PackageManangeWindow
        //{
        //    get { return this.LazyNuGetPackageManageWindow.Value.Current; }
        //}

        public PackageManagerTopPanelTestExtension TopPanel => throw new NotImplementedException();

        public PackageManagerInfiniteScrollListTestExtension InfiniteScrollList => throw new NotImplementedException();

        public PackageDetailTestExtension Detail => throw new NotImplementedException();

        //[Import(AllowDefault = true)]
        //private Lazy<PackageManageUIWindowService> LazyNuGetPackageManageWindow { get; set; }
    }
}
