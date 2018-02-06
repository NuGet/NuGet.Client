using Apex.NuGetClient.ObjectModel.TestExtensions;

namespace Apex.NuGetClient.ObjectModel
{
    public interface IPackageManageUIObjectModel
    {
       //Package Manange Window
        //PackageManageUITestExtension PackageManangeWindow { get; }

        //Package Manager Top Panel
        PackageManagerTopPanelTestExtension TopPanel { get; }

        //InfiniteScrollList
        PackageManagerInfiniteScrollListTestExtension InfiniteScrollList { get; }

        //Detail Control
        PackageDetailTestExtension Detail { get; }
    }
}
