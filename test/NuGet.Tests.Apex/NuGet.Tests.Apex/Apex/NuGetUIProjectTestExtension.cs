using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.UI.TestContract;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Tests.Apex
{
    public class NuGetUIProjectTestExtension : NuGetBaseTestExtension<object, NuGetUIProjectTestExtensionVerifier>
    {
        private ApexTestUIProject _uiproject;
        private TimeSpan _timeout = TimeSpan.FromSeconds(5);

        public bool IsSolution { get => _uiproject.IsSolution; }

        public NuGetUIProjectTestExtension(ApexTestUIProject project)
        {
            _uiproject = project;
        }

        public bool SeachPackgeFromUI(string searchText)
        {
            return _uiproject.WaitForSearchComplete(() => _uiproject.Search(searchText), _timeout);
        }

        public bool InstallPackageFromUI(string packageId, string version)
        {
            return _uiproject.WaitForActionComplete(() => _uiproject.InstallPackage(packageId, version), _timeout);
        }

        public bool UninstallPackageFromUI(string packageId)
        {
            return _uiproject.WaitForActionComplete(() => _uiproject.UninstallPackage(packageId), _timeout);
        }

        public bool UpdatePackageFromUI(string packageId, string version)
        {
            return _uiproject.WaitForActionComplete(
                () => _uiproject.UpdatePackage(
                    new List<PackageIdentity>() { new PackageIdentity(packageId, NuGetVersion.Parse(version)) }),
                _timeout);
        }

        public void SwitchTabToBrowse()
        {
            _uiproject.ActiveFilter = ItemFilter.All;
        }

        public void SwitchTabToInstalled()
        {
            _uiproject.ActiveFilter = ItemFilter.Installed;
        }

        public void SwitchTabToUpdate()
        {
            _uiproject.ActiveFilter = ItemFilter.UpdatesAvailable;
        }

        public bool IsPackageInstalled(string packageId, string version)
        {
            return _uiproject.IsPackageInstalled(new PackageIdentity(packageId, NuGetVersion.Parse(version)));
        }

    }
}
