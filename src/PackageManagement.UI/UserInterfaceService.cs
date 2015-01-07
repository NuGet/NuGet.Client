using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;

namespace NuGet.PackageManagement.UI
{
    [Export(typeof(IUserInterfaceService))]
    public class UserInterfaceService : IUserInterfaceService
    {
        private readonly Dispatcher _uiDispatcher;

        public UserInterfaceService()
        {
            _uiDispatcher = Dispatcher.CurrentDispatcher;
        }

        public bool PromptForLicenseAcceptance(IEnumerable<PackageLicenseInfo> packages)
        {
            if (_uiDispatcher.CheckAccess())
            {
                return PromptForLicenseAcceptanceImpl(packages);
            }
            else
            {
                // Use Invoke() here to block the worker thread
                object result = _uiDispatcher.Invoke(
                    new Func<IEnumerable<PackageLicenseInfo>, bool>(PromptForLicenseAcceptanceImpl),
                    packages);
                return (bool)result;
            }
        }

        private bool PromptForLicenseAcceptanceImpl(
            IEnumerable<PackageLicenseInfo> packages)
        {
            var licenseWindow = new LicenseAcceptanceWindow()
            {
                DataContext = packages
            };

            throw new NotImplementedException();
            //using (NuGetEventTrigger.Instance.TriggerEventBeginEnd(
            //    NuGetEvent.LicenseWindowBegin,
            //    NuGetEvent.LicenseWindowEnd))
            //{
            //    bool? dialogResult = licenseWindow.ShowModal();
            //    return dialogResult ?? false;
            //}
        }

        public void LaunchExternalLink(Uri url)
        {
            throw new NotImplementedException();
            //NuGet.VisualStudio.UriHelper.OpenExternalLink(url);
        }

        public void LaunchNuGetOptionsDialog()
        {
            throw new NotImplementedException();
            //var optionsPageActivator = ServiceLocator.GetInstance<IOptionsPageActivator>();
            //optionsPageActivator.ActivatePage(OptionsPage.General, null);
        }
    }
}
