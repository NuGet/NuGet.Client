using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using NuGet.ProjectManagement;
using NuGet.PackagingCore;
using System.Diagnostics;
using NuGet.Client;

namespace NuGet.PackageManagement.UI
{
    public class NuGetUI : INuGetUI
    {
        private readonly INuGetUIContext _context;

        private ProgressDialog _progressDialog;
        private readonly NuGetProject[] _projects;

        public NuGetUI(INuGetUIContext context, IEnumerable<NuGetProject> projects)
        {
            _context = context;
            _projects = projects.ToArray();
        }

        public bool PromptForLicenseAcceptance(IEnumerable<PackageLicenseInfo> packages)
        {
            bool result = false;

            UIDispatcher.Invoke(() =>
                    {
                        result = PromptForLicenseAcceptanceImpl(packages);
                    });

            return result;
        }

        private bool PromptForLicenseAcceptanceImpl(
            IEnumerable<PackageLicenseInfo> packages)
        {
            var licenseWindow = new LicenseAcceptanceWindow()
            {
                DataContext = packages
            };

            using (NuGetEventTrigger.Instance.TriggerEventBeginEnd(
                NuGetEvent.LicenseWindowBegin,
                NuGetEvent.LicenseWindowEnd))
            {
                bool? dialogResult = licenseWindow.ShowModal();
                return dialogResult ?? false;
            }
        }

        public void LaunchExternalLink(Uri url)
        {
            UIUtility.LaunchExternalLink(url);
        }

        public void LaunchNuGetOptionsDialog()
        {
            UIDispatcher.Invoke(() =>
                {
                    _context.OptionsPageActivator.ActivatePage(OptionsPage.General, null);
                });
        }


        public bool PromptForPreviewAcceptance(IEnumerable<PreviewResult> actions)
        {
            bool result = false;

            UIDispatcher.Invoke(() =>
            {
                var w = new PreviewWindow();
                w.DataContext = new PreviewWindowModel(actions);
                result = w.ShowModal() == true;
            });

            return result;
        }

        public void ShowProgressDialog(DependencyObject ownerWindow)
        {
            Debug.Assert(_progressDialog == null, "Progress dialog is already up");

            if (_progressDialog == null)
            {
                UIDispatcher.Invoke(() =>
                    {
                        _progressDialog = new ProgressDialog();

                        _progressDialog.Owner = Window.GetWindow(ownerWindow);
                        _progressDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        _progressDialog.FileConflictAction = FileConflictAction;
                        _progressDialog.Show();
                    });
            }
        }

        public void CloseProgressDialog()
        {
            if (_progressDialog != null)
            {
                UIDispatcher.Invoke(() =>
                    {
                        _progressDialog.Close();
                        _progressDialog = null;
                    });
            }
        }

        public INuGetProjectContext ProgressWindow
        {
            get
            {
                return _progressDialog;
            }
        }

        public IEnumerable<NuGetProject> Projects
        {
            get
            {
                return _projects;
            }
        }

        public bool DisplayPreviewWindow
        {
            get
            {
                bool result = true;

                if (DetailControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        result = DetailControl.DisplayPreviewWindow;
                    });
                }

                return result;
            }
        }

        public FileConflictAction FileConflictAction
        {
            get
            {
                FileConflictAction result = FileConflictAction.PromptUser;

                if (DetailControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        result = DetailControl.FileConflictAction;
                    });
                }

                return result;
            }
        }

        public UserAction UserAction
        {
            get
            {
                UserAction result = null;

                if (DetailControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        result = DetailControl.GetUserAction();
                    });
                }

                return result;
            }
        }

        public PackageIdentity SelectedPackage
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void ShowError(string message, string detail)
        {
            UIDispatcher.Invoke(() =>
                    {
                        var errorDialog = new ErrorReportingDialog(
                                message,
                                detail);
                        errorDialog.ShowModal();
                    });
        }

        public void RefreshPackageStatus()
        {
            if (PackageManagerControl != null)
            {
                UIDispatcher.Invoke(() =>
                {
                    PackageManagerControl.UpdatePackageStatus();
                });
            }

            if (DetailControl != null)
            {
                UIDispatcher.Invoke(() =>
                {
                    DetailControl.Refresh();
                });
            }
        }

        public SourceRepository ActiveSource
        {
            get
            {
                SourceRepository source = null;

                if (PackageManagerControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        source = PackageManagerControl.ActiveSource;
                    });
                }

                return source;
            }
        }

        internal PackageManagerControl PackageManagerControl { get; set; }

        internal DetailControl DetailControl { get; set; }

        private Dispatcher UIDispatcher
        {
            get
            {
                if (DetailControl != null)
                {
                    return DetailControl.Dispatcher;
                }

                if (Application.Current != null)
                {
                    return Application.Current.Dispatcher;
                }

                // null for unit tests
                return null;
            }
        }
    }

    public static class UIUtility
    {
        public static void LaunchExternalLink(Uri url)
        {
            if (url == null || !url.IsAbsoluteUri)
            {
                return;
            }

            // mitigate security risk
            if (url.IsFile || url.IsLoopback || url.IsUnc)
            {
                return;
            }

            if (IsHttpUrl(url))
            {
                // REVIEW: Will this allow a package author to execute arbitrary program on user's machine?
                // We have limited the url to be HTTP only, but is it sufficient?
                System.Diagnostics.Process.Start(url.AbsoluteUri);
                NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.LinkOpened);
            }
        }

        private static bool IsHttpUrl(Uri uri)
        {
            return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }

}
