using System;
using System.Collections.Generic;
using NuGet.PackageManagement.UI;
using NuGet.Packaging.Core;

namespace NuGetClientTestContracts
{
    public interface IPackageManageUITestContract : INuGetClientTestContract
    {

        IEnumerable<PackageItemListViewModel> PackageItems { get; }

        PackageItemListViewModel SeletedPackage { get; }

        ItemFilter ActiveFilter { get; set; }

        bool IsSolution { get; }

        void Search();

        void InstallPackage(string packageId, string version);

        void UninstallPackage(string packageId);

        void UpdatePackage(List<PackageIdentity> packages);

        bool IsPackageInstalled(PackageIdentity package);

        bool WaitForActionComplete(Action action, TimeSpan timeout);

        bool WaitForSearchComplete(Action search, TimeSpan timeout);

        void UIInvoke(Action action);

        T UIInvoke<T>(Func<T> function);

        PackageManagerControl GetProjectPackageManagerControl(string projectUniqueName);
    }
}
