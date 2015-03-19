using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using NuGet.ProjectManagement;
using NuGet.Packaging.Core;
using System.Diagnostics;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    public class NuGetUI : INuGetUI
    {
        private readonly INuGetUIContext _context;
        private NuGetUIProjectContext _uiProjectContext;

        public NuGetUI(
            INuGetUIContext context,             
            NuGetUIProjectContext projectContext)
        {
            _context = context;
            _uiProjectContext = projectContext;
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
            if (_context != null && _context.OptionsPageActivator != null)
            {
                UIDispatcher.Invoke(() =>
                    {
                        _context.OptionsPageActivator.ActivatePage(OptionsPage.General, null);
                    });
            }
            else
            {
                MessageBox.Show("Options dialog is not available in the standalone UI");
            }
        }

        public bool PromptForPreviewAcceptance(IEnumerable<PreviewResult> actions)
        {
            bool result = false;

            UIDispatcher.Invoke(() =>
            {
                var w = new PreviewWindow();
                w.DataContext = new PreviewWindowModel(actions);

                if (StandaloneSwitch.IsRunningStandalone && DetailControl != null)
                {
                    Window win = Window.GetWindow(DetailControl);
                    w.Owner = win;
                }

                result = w.ShowModal() == true;
            });

            return result;
        }

        // TODO: rename it to something like Start
        public void ShowProgressDialog(DependencyObject ownerWindow)
        {
            _uiProjectContext.Start();
            _uiProjectContext.FileConflictAction = FileConflictAction;
        }

        // TODO: rename it to something like End
        public void CloseProgressDialog()
        {
            _uiProjectContext.End();
        }

        // TODO: rename it
        public NuGetUIProjectContext ProgressWindow
        {
            get
            {
                return _uiProjectContext;
            }
        }

        public IEnumerable<NuGetProject> Projects
        {
            get
            {
                var projects = Enumerable.Empty<NuGetProject>();
                if (DetailControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        var model = (DetailControlModel)DetailControl.DataContext;
                        projects = model.SelectedProjects;
                    });
                }

                return projects;
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
                        var model = (DetailControlModel)DetailControl.DataContext;
                        result = model.Options.ShowPreviewWindow;
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
                        var model = (DetailControlModel)DetailControl.DataContext;
                        result = model.Options.SelectedFileConflictAction.Action;
                    });
                }

                return result;
            }
        }

        public Resolver.DependencyBehavior DependencyBehavior
        {
            get
            {
                var result = Resolver.DependencyBehavior.Lowest;

                if (DetailControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        var model = (DetailControlModel)DetailControl.DataContext;
                        result = model.Options.SelectedDependencyBehavior.Behavior;
                    });
                }

                return result;
            }
        }

        public bool RemoveDependencies
        {
            get
            {
                bool result = false;
                if (DetailControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        var model = (DetailControlModel)DetailControl.DataContext;
                        result = model.Options.RemoveDependencies;
                    });
                }
                return result;
            }
        }

        public bool ForceRemove
        {
            get
            {
                bool result = false;
                if (DetailControl != null)
                {
                    UIDispatcher.Invoke(() =>
                    {
                        var model = (DetailControlModel)DetailControl.DataContext;
                        result = model.Options.ForceRemove;
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

        public void ShowError(Exception ex)
        {
            if (ex is NuGet.Resolver.NuGetResolverConstraintException ||
                ex is PackageAlreadyInstalledException ||
                ex is NuGetVersionNotSatisfiedException ||
                ex is NuGet.Frameworks.FrameworkException ||
                ex is NuGet.Packaging.Core.PackagingException)
            {
                // for exceptions that are known to be normal error cases, just
                // display the message.
                _uiProjectContext.Log(MessageLevel.Info, ex.Message);
            }
            else
            {
                _uiProjectContext.Log(MessageLevel.Error, ex.ToString());
            }

            _uiProjectContext.ReportError(ex.Message);
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
