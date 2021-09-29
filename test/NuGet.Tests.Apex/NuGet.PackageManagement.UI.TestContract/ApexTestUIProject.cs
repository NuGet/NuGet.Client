using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI.TestContract
{
    public class ApexTestUIProject
    {
        private PackageManagerControl _packageManagerControl;

        internal ApexTestUIProject(PackageManagerControl packageManagerControl)
        {
            _packageManagerControl = packageManagerControl ?? throw new ArgumentNullException(nameof(packageManagerControl));
        }

        public IEnumerable<PackageItemViewModel> PackageItems
        {
            get
            {
                return UIInvoke(() => _packageManagerControl.PackageList.PackageItems);
            }
        }

        public PackageItemViewModel SelectedPackage
        {
            get
            {
                return UIInvoke(() => _packageManagerControl.PackageList.SelectedItem);
            }
            set
            {
                UIInvoke(() => _packageManagerControl.PackageList.SelectedItem = value);
            }
        }

        public ItemFilter ActiveFilter
        {
            get
            {
                return UIInvoke(() => _packageManagerControl.ActiveFilter);
            }
            set
            {
                UIInvoke(() => _packageManagerControl.ActiveFilter = value);
            }
        }

        public bool IsSolution { get => _packageManagerControl._detailModel.IsSolution; }

        public void Search(string searchText)
        {
            UIInvoke(() => _packageManagerControl.Search(searchText));
        }

        public void InstallPackage(string packageId, string version)
        {
            UIInvoke(() => _packageManagerControl.InstallPackage(packageId, NuGetVersion.Parse(version), null));
        }

        public void UninstallPackage(string packageId)
        {
            UIInvoke(() => _packageManagerControl.UninstallPackage(packageId, null));
        }

        public void UpdatePackage(List<PackageIdentity> packages)
        {
            UIInvoke(() => _packageManagerControl.UpdatePackage(packages, null));
        }

        public bool WaitForActionComplete(Action action, TimeSpan timeout)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            EventHandler eventHandler = (s, e) => taskCompletionSource.TrySetResult(true);

            try
            {
                _packageManagerControl._actionCompleted += eventHandler;

                action();

                if (!taskCompletionSource.Task.Wait(timeout))
                {
                    return false;
                }
                else
                {
                    return true;
                }

            }
            finally
            {
                _packageManagerControl._actionCompleted -= eventHandler;
            }
        }

        public bool WaitForSearchComplete(Action search, TimeSpan timeout)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            EventHandler eventHandler = (s, e) => taskCompletionSource.TrySetResult(true);

            try
            {
                _packageManagerControl.PackageList.LoadItemsCompleted += eventHandler;

                search();

                if (!taskCompletionSource.Task.Wait(timeout))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            finally
            {
                _packageManagerControl.PackageList.LoadItemsCompleted -= eventHandler;
            }
        }

        /// <summary>
        /// Used for package source mapping Apex tests which require All option in package sources.
        /// </summary>
        public void SetPackageSourceOptionToAll() => UIInvoke(() => {
            // First one is always 'All' option
            _packageManagerControl.SelectedSource = _packageManagerControl.PackageSources.First();
        });

        private void UIInvoke(Action action)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                action();
            });
        }

        private T UIInvoke<T>(Func<T> function)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return function();
            });
        }
    }
}
