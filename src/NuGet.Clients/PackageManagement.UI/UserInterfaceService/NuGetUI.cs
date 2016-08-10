// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;

namespace NuGet.PackageManagement.UI
{
    public class NuGetUI : INuGetUI
    {
        private readonly INuGetUIContext _context;
        public const string LogEntrySource = "NuGet Package Manager";

        public NuGetUI(
            INuGetUIContext context,
            NuGetUIProjectContext projectContext)
        {
            _context = context;
            ProgressWindow = projectContext;

            // set default values of properties
            FileConflictAction = FileConflictAction.PromptUser;
            DependencyBehavior = DependencyBehavior.Lowest;
            RemoveDependencies = false;
            ForceRemove = false;
            Projects = Enumerable.Empty<NuGetProject>();
            DisplayPreviewWindow = true;
            DisplayDeprecatedFrameworkWindow = true;
        }

        public bool WarnAboutDotnetDeprecation(IEnumerable<NuGetProject> projects)
        {
            var result = false;

            UIDispatcher.Invoke(() => { result = WarnAboutDotnetDeprecationImpl(projects); });

            return result;
        }

        private bool WarnAboutDotnetDeprecationImpl(IEnumerable<NuGetProject> projects)
        {
            var window = new DeprecatedFrameworkWindow(_context)
            {
                DataContext = DotnetDeprecatedPrompt.GetDeprecatedFrameworkModel(projects)
            };

            var dialogResult = window.ShowModal();
            return dialogResult ?? false;
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

        public void LaunchNuGetOptionsDialog(OptionsPage optionsPageToOpen)
        {
            if (_context?.OptionsPageActivator != null)
            {
                UIDispatcher.Invoke(() => { _context.OptionsPageActivator.ActivatePage(optionsPageToOpen, null); });
            }
            else
            {
                MessageBox.Show("Options dialog is not available in the standalone UI");
            }
        }

        public bool PromptForPreviewAcceptance(IEnumerable<PreviewResult> actions)
        {
            var result = false;

            if (actions.Any())
            {
                UIDispatcher.Invoke(() =>
                {
                    var w = new PreviewWindow(_context);
                    w.DataContext = new PreviewWindowModel(actions);

                    if (StandaloneSwitch.IsRunningStandalone
                        && _detailControl != null)
                    {
                        var win = Window.GetWindow(_detailControl);
                        w.Owner = win;
                    }

                    result = w.ShowModal() == true;
                });
            }
            else
            {
                return true;
            }

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
            set;
            get;
        }

        public bool DisplayPreviewWindow
        {
            set;
            get;
        }

        public bool DisplayDeprecatedFrameworkWindow
        {
            set;
            get;
        }

        public FileConflictAction FileConflictAction
        {
            set;
            get;
        }

        public DependencyBehavior DependencyBehavior
        {
            set;
            get;
        }

        public bool RemoveDependencies
        {
            set;
            get;
        }

        public bool ForceRemove
        {
            set;
            get;
        }

        public PackageIdentity SelectedPackage { get; set; }

        public void OnActionsExecuted(IEnumerable<ResolvedAction> actions)
        {
            this._context.SolutionManager.OnActionsExecuted(actions);
        }

        public IEnumerable<SourceRepository> ActiveSources
        {
            get
            {
                IEnumerable<SourceRepository> sources = null;

                if (PackageManagerControl != null)
                {
                    UIDispatcher.Invoke(() => { sources = PackageManagerControl.ActiveSources; });
                }

                return sources;
            }
        }

        internal PackageManagerControl PackageManagerControl { get; set; }

        private DetailControl _detailControl;

        internal DetailControl DetailControl
        {
            set
            {
                _detailControl = value;
            }
        }

        private Dispatcher UIDispatcher
        {
            get
            {
                if (_detailControl != null)
                {
                    return _detailControl.Dispatcher;
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
                ex is MinClientVersionException ||
                ex is FrameworkException ||
                ex is NuGetProtocolException ||
                ex is PackagingException ||
                ex is InvalidOperationException)
            {
                // for exceptions that are known to be normal error cases, just
                // display the message.
                ProgressWindow.Log(MessageLevel.Info, ExceptionUtilities.DisplayMessage(ex, indent: true));

                // write to activity log
                var activityLogMessage = string.Format(CultureInfo.CurrentCulture, ex.ToString());
                ActivityLog.LogError(LogEntrySource, activityLogMessage);
            }
            else
            {
                ProgressWindow.Log(MessageLevel.Error, ex.ToString());
            }

            ProgressWindow.ReportError(ExceptionUtilities.DisplayMessage(ex, indent: false));
        }
    }
}