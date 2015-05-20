// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using Microsoft.VisualStudio.Shell;
using System.Globalization;

namespace NuGet.PackageManagement.UI
{
    public class NuGetUI : INuGetUI
    {
        private readonly INuGetUIContext _context;
        private const string LogEntrySource = "NuGet Package Manager";

        public NuGetUI(
            INuGetUIContext context,
            NuGetUIProjectContext projectContext)
        {
            _context = context;
            ProgressWindow = projectContext;
        }

        public bool PromptForLicenseAcceptance(IEnumerable<PackageLicenseInfo> packages)
        {
            var result = false;

            UIDispatcher.Invoke(() => { result = PromptForLicenseAcceptanceImpl(packages); });

            return result;
        }

        private static bool PromptForLicenseAcceptanceImpl(
            IEnumerable<PackageLicenseInfo> packages)
        {
            var licenseWindow = new LicenseAcceptanceWindow
                {
                    DataContext = packages
                };

            using (NuGetEventTrigger.TriggerEventBeginEnd(
                NuGetEvent.LicenseWindowBegin,
                NuGetEvent.LicenseWindowEnd))
            {
                var dialogResult = licenseWindow.ShowModal();
                return dialogResult ?? false;
            }
        }

        public void LaunchExternalLink(Uri url)
        {
            UIUtility.LaunchExternalLink(url);
        }

        public void LaunchNuGetOptionsDialog()
        {
            if (_context?.OptionsPageActivator != null)
            {
                UIDispatcher.Invoke(() => { _context.OptionsPageActivator.ActivatePage(OptionsPage.General, null); });
            }
            else
            {
                MessageBox.Show("Options dialog is not available in the standalone UI");
            }
        }

        public bool PromptForPreviewAcceptance(IEnumerable<PreviewResult> actions)
        {
            var result = false;

            UIDispatcher.Invoke(() =>
                {
                    var w = new PreviewWindow(_context);
                    w.DataContext = new PreviewWindowModel(actions);

                    if (StandaloneSwitch.IsRunningStandalone
                        && DetailControl != null)
                    {
                        var win = Window.GetWindow(DetailControl);
                        w.Owner = win;
                    }

                    result = w.ShowModal() == true;
                });

            return result;
        }

        // TODO: rename it to something like Start
        public void ShowProgressDialog(DependencyObject ownerWindow)
        {
            ProgressWindow.Start();
            ProgressWindow.FileConflictAction = FileConflictAction;
        }

        // TODO: rename it to something like End
        public void CloseProgressDialog()
        {
            ProgressWindow.End();
        }

        // TODO: rename it
        public NuGetUIProjectContext ProgressWindow { get; }

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
                var result = true;

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
                var result = FileConflictAction.PromptUser;

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

        public DependencyBehavior DependencyBehavior
        {
            get
            {
                var result = DependencyBehavior.Lowest;

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
                var result = false;
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
                var result = false;
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
                    UIDispatcher.Invoke(() => { result = DetailControl.GetUserAction(); });
                }

                return result;
            }
        }

        public PackageIdentity SelectedPackage { get; set; }

        public void RefreshPackageStatus()
        {
            if (PackageManagerControl != null)
            {
                UIDispatcher.Invoke(() => { PackageManagerControl.UpdatePackageStatus(); });
            }

            if (DetailControl != null)
            {
                UIDispatcher.Invoke(() => { DetailControl.Refresh(); });
            }
        }

        public SourceRepository ActiveSource
        {
            get
            {
                SourceRepository source = null;

                if (PackageManagerControl != null)
                {
                    UIDispatcher.Invoke(() => { source = PackageManagerControl.ActiveSource; });
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
            if (ex is NuGetResolverConstraintException ||
                ex is PackageAlreadyInstalledException ||
                ex is NuGetVersionNotSatisfiedException ||
                ex is FrameworkException ||
                ex is PackagingException || 
                ex is InvalidOperationException)
            {
                // for exceptions that are known to be normal error cases, just
                // display the message.
                ProgressWindow.Log(ProjectManagement.MessageLevel.Info, ex.Message);

                // write to activity log
                var message = string.Format(CultureInfo.CurrentCulture, ex.ToString());
                ActivityLog.LogError(LogEntrySource, message);
            }
            else
            {
                ProgressWindow.Log(ProjectManagement.MessageLevel.Error, ex.ToString());
            }

            ProgressWindow.ReportError(ex.Message);
        }
    }

    public static class UIUtility
    {
        public static void LaunchExternalLink(Uri url)
        {
            if (url == null
                || !url.IsAbsoluteUri)
            {
                return;
            }

            // mitigate security risk
            if (url.IsFile
                || url.IsLoopback
                || url.IsUnc)
            {
                return;
            }

            if (IsHttpUrl(url))
            {
                // REVIEW: Will this allow a package author to execute arbitrary program on user's machine?
                // We have limited the url to be HTTP only, but is it sufficient?
                Process.Start(url.AbsoluteUri);
                NuGetEventTrigger.Instance.TriggerEvent(NuGetEvent.LinkOpened);
            }
        }

        private static bool IsHttpUrl(Uri uri)
        {
            return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
