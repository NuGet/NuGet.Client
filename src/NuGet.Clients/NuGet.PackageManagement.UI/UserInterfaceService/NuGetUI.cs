// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public sealed class NuGetUI : INuGetUI
    {
        public const string LogEntrySource = "NuGet Package Manager";

        private readonly NuGetUIProjectContext _projectContext;

        public NuGetUI(
            ICommonOperations commonOperations,
            NuGetUIProjectContext projectContext,
            INuGetUIContext context,
            INuGetUILogger logger)
        {
            CommonOperations = commonOperations;
            _projectContext = projectContext;
            UIContext = context;
            UILogger = logger;

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

            InvokeOnUIThread(() => { result = WarnAboutDotnetDeprecationImpl(projects); });

            return result;
        }

        public bool ShowNuGetUpgradeWindow(NuGetProjectUpgradeWindowModel nuGetProjectUpgradeWindowModel)
        {
            var result = false;

            InvokeOnUIThread(() =>
            {
                var upgradeInformationWindow = new NuGetProjectUpgradeWindow(nuGetProjectUpgradeWindowModel);

                result = upgradeInformationWindow.ShowModal() == true;
            });

            return result;
        }

        private bool WarnAboutDotnetDeprecationImpl(IEnumerable<NuGetProject> projects)
        {
            var window = new DeprecatedFrameworkWindow(UIContext)
            {
                DataContext = DotnetDeprecatedPrompt.GetDeprecatedFrameworkModel(projects)
            };

            var dialogResult = window.ShowModal();
            return dialogResult ?? false;
        }

        public bool PromptForLicenseAcceptance(IEnumerable<PackageLicenseInfo> packages)
        {
            var result = false;

            InvokeOnUIThread(() => { result = PromptForLicenseAcceptanceImpl(packages); });

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

        public bool PromptForPackageManagementFormat(PackageManagementFormat selectedFormat)
        {
            var result = false;

            InvokeOnUIThread(() => { result = PromptForPackageManagementFormatImpl(selectedFormat); });

            return result;
        }

        private bool PromptForPackageManagementFormatImpl(PackageManagementFormat selectedFormat)
        {
            var packageFormatWindow = new PackageManagementFormatWindow(UIContext);
            packageFormatWindow.DataContext = selectedFormat;
            var dialogResult = packageFormatWindow.ShowModal();
            return dialogResult ?? false;
        }

        public async System.Threading.Tasks.Task UpdateNuGetProjectToPackageRef(IEnumerable<NuGetProject> msBuildProjects)
        {
            var projects = Projects.ToList();

            foreach (var project in msBuildProjects)
            {
                var newProject = await UIContext.SolutionManager.UpgradeProjectToPackageReferenceAsync(project);

                if (newProject != null)
                {
                    projects.Remove(project);
                    projects.Add(newProject);
                }
            }

            Projects = projects;
        }

        public void LaunchExternalLink(Uri url)
        {
            UIUtility.LaunchExternalLink(url);
        }

        public void LaunchNuGetOptionsDialog(OptionsPage optionsPageToOpen)
        {
            if (UIContext?.OptionsPageActivator != null)
            {
                InvokeOnUIThread(() => { UIContext.OptionsPageActivator.ActivatePage(optionsPageToOpen, null); });
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
                InvokeOnUIThread(() =>
                {
                    var w = new PreviewWindow(UIContext);
                    w.DataContext = new PreviewWindowModel(actions);

                    result = w.ShowModal() == true;
                });
            }
            else
            {
                return true;
            }

            return result;
        }

        public void BeginOperation()
        {
            _projectContext.FileConflictAction = FileConflictAction;
            UILogger.Start();
        }

        public void EndOperation()
        {
            UILogger.End();
        }

        public ICommonOperations CommonOperations { get; }

        public INuGetUIContext UIContext { get; }

        public INuGetUILogger UILogger { get; }

        public INuGetProjectContext ProjectContext => _projectContext;

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
            UIContext.SolutionManager.OnActionsExecuted(actions);
        }

        public IEnumerable<SourceRepository> ActiveSources
        {
            get
            {
                IEnumerable<SourceRepository> sources = null;

                if (PackageManagerControl != null)
                {
                    InvokeOnUIThread(() => { sources = PackageManagerControl.ActiveSources; });
                }

                return sources;
            }
        }

        public Configuration.ISettings Settings
        {
            get
            {
                Configuration.ISettings settings = null;

                if (PackageManagerControl != null)
                {
                    InvokeOnUIThread(() => { settings = PackageManagerControl.Settings; });
                }

                return settings;
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

        private void InvokeOnUIThread(Action action)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                action();
            });
        }

        public void ShowError(Exception ex)
        {
            var signException = ex as SignatureException;

            if (signException != null)
            {
                ProcessSignatureIssues(signException);
            }
            else
            {
                if (ex is NuGetResolverConstraintException ||
                    ex is PackageAlreadyInstalledException ||
                    ex is MinClientVersionException ||
                    ex is FrameworkException ||
                    ex is NuGetProtocolException ||
                    ex is PackagingException ||
                    ex is InvalidOperationException ||
                    ex is PackageReferenceRollbackException)
                {
                    // for exceptions that are known to be normal error cases, just
                    // display the message.
                    ProjectContext.Log(MessageLevel.Info, ExceptionUtilities.DisplayMessage(ex, indent: true));

                    // write to activity log
                    var activityLogMessage = string.Format(CultureInfo.CurrentCulture, ex.ToString());
                    ActivityLog.LogError(LogEntrySource, activityLogMessage);

                    // Log additional messages to the error list to provide context on why the rollback failed
                    var rollbackException = ex as PackageReferenceRollbackException;
                    if (rollbackException != null)
                    {
                        foreach (var message in rollbackException.LogMessages)
                        {
                            if (message.Level == LogLevel.Error)
                            {
                                UILogger.ReportError(message);
                            }
                        }
                    }
                }
                else
                {
                    ProjectContext.Log(MessageLevel.Error, ex.ToString());
                }

                UILogger.ReportError(ExceptionUtilities.DisplayMessage(ex, indent: false));
            }
        }

        private void ProcessSignatureIssues(SignatureException ex)
        {
            if (!string.IsNullOrEmpty(ex.Message))
            {
                UILogger.ReportError(ex.AsLogMessage().FormatWithCode());
                ProjectContext.Log(MessageLevel.Error, ex.AsLogMessage().FormatWithCode());
            }

            foreach (var result in ex.Results)
            {
                var errorList = result.GetErrorIssues().ToList();
                var warningList = result.GetWarningIssues().ToList();

                errorList.ForEach(p => UILogger.ReportError(p.FormatWithCode()));

                errorList.ForEach(p => ProjectContext.Log(MessageLevel.Error, p.FormatWithCode()));
                warningList.ForEach(p => ProjectContext.Log(MessageLevel.Warning, p.FormatWithCode()));
            }            
        }
    }
}