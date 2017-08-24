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

        internal ApexTestUIProject(PackageManagerControl project)
        {
            _packageManagerControl = project;
        }

        public IEnumerable<PackageItemListViewModel> PackageItems
        {
            get
            {
                return UIInvoke(() => _packageManagerControl.PackageList.PackageItems);
            }
        }

        public PackageItemListViewModel SelectedPackage
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
            UIInvoke(() => _packageManagerControl.InstallPackage(packageId, NuGetVersion.Parse(version)));
        }

        public void UninstallPackage(string packageId)
        {
            UIInvoke(() => _packageManagerControl.UninstallPackage(packageId));
        }

        public void UpdatePackage(List<PackageIdentity> packages)
        {
            UIInvoke(() => _packageManagerControl.UpdatePackage(packages));
        }

        public bool IsPackageInstalled(PackageIdentity package)
        {
            var nugetProject = _packageManagerControl._detailModel.NuGetProjects.FirstOrDefault();

            if (nugetProject == null)
            {
                return false;
            }

            var installedPackage = NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                return await nugetProject.GetInstalledPackagesAsync(CancellationToken.None);
            });

            return installedPackage.Any(p => p.PackageIdentity.Equals(package));
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
