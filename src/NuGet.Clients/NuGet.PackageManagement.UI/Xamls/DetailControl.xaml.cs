// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    // The DataContext of this control is DetailControlModel, i.e. either
    // PackageSolutionDetailControlModel or PackageDetailControlModel.
    public partial class DetailControl : UserControl
    {
        public PackageManagerControl Control { get; set; }

        public DetailControl()
        {
            InitializeComponent();
            DataContextChanged += PackageSolutionDetailControl_DataContextChanged;
        }

        private void PackageSolutionDetailControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var dataContext = DataContext as DetailControlModel;

            if (dataContext == null)
            {
                _root.Visibility = Visibility.Collapsed;
                return;
            }

            _root.Visibility = Visibility.Visible;

            if (dataContext.IsSolution)
            {
                _solutionView.InstallButtonClicked += SolutionInstallButtonClicked;
                _solutionView.UninstallButtonClicked += SolutionUninstallButtonClicked;

                _projectView.InstallButtonClicked -= ProjectInstallButtonClicked;
                _projectView.UninstallButtonClicked -= ProjectUninstallButtonClicked;
            }
            else
            {
                _projectView.InstallButtonClicked += ProjectInstallButtonClicked;
                _projectView.UninstallButtonClicked += ProjectUninstallButtonClicked;

                _solutionView.InstallButtonClicked -= SolutionInstallButtonClicked;
                _solutionView.UninstallButtonClicked -= SolutionUninstallButtonClicked;
            }
        }

        private void ExecuteOpenLicenseLink(object sender, ExecutedRoutedEventArgs e)
        {
            var hyperlink = e.OriginalSource as Hyperlink;
            if (hyperlink != null
                && hyperlink.NavigateUri != null)
            {
                Control.Model.UIController.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }

        public void ScrollToHome()
        {
            _root.ScrollToHome();
        }

        public void Refresh()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // because the code is async, it's possible that the DataContext has been changed
                // once execution reaches here and thus 'model' could be null.
                var model = DataContext as DetailControlModel;

                if (model != null)
                {
                    await model.RefreshAsync(CancellationToken.None);
                }
            });
        }

        private void ProjectInstallButtonClicked(object sender, EventArgs e)
        {
            var model = (PackageDetailControlModel)DataContext;

            if (model != null && model.SelectedVersion != null)
            {
                var userAction = UserAction.CreateInstallAction(
                    model.Id,
                    model.SelectedVersion.Version);

                ExecuteUserAction(userAction, NuGetActionType.Install);
            }
        }

        private void ProjectUninstallButtonClicked(object sender, EventArgs e)
        {
            var model = (PackageDetailControlModel)DataContext;

            if (model != null)
            {
                var userAction = UserAction.CreateUnInstallAction(model.Id);
                ExecuteUserAction(userAction, NuGetActionType.Uninstall);
            }
        }

        private void SolutionInstallButtonClicked(object sender, EventArgs e)
        {
            var model = (PackageSolutionDetailControlModel)DataContext;

            if (model != null && model.SelectedVersion != null)
            {
                var userAction = UserAction.CreateInstallAction(
                    model.Id,
                    model.SelectedVersion.Version);

                ExecuteUserAction(userAction, NuGetActionType.Install);
            }
        }

        private void SolutionUninstallButtonClicked(object sender, EventArgs e)
        {
            var model = (PackageSolutionDetailControlModel)DataContext;

            if (model != null)
            {
                var userAction = UserAction.CreateUnInstallAction(model.Id);
                ExecuteUserAction(userAction, NuGetActionType.Uninstall);
            }
        }

        private void ExecuteUserAction(UserAction action, NuGetActionType actionType)
        {
            Control.ExecuteAction(
                () =>
                {
                    return Control.Model.Context.UIActionEngine.PerformInstallOrUninstallAsync(
                        Control.Model.UIController,
                        action,
                        CancellationToken.None);
                },
                nugetUi =>
                {
                    var model = (DetailControlModel)DataContext;

                    // Set the properties by reading the current options on the UI
                    nugetUi.FileConflictAction = model.Options.SelectedFileConflictAction.Action;
                    nugetUi.DependencyBehavior = model.Options.SelectedDependencyBehavior.Behavior;
                    nugetUi.RemoveDependencies = model.Options.RemoveDependencies;
                    nugetUi.ForceRemove = model.Options.ForceRemove;
                    nugetUi.Projects = model.GetSelectedProjects(action);
                    nugetUi.DisplayPreviewWindow = model.Options.ShowPreviewWindow;
                    nugetUi.DisplayDeprecatedFrameworkWindow = model.Options.ShowDeprecatedFrameworkWindow;
                    nugetUi.ProjectContext.ActionType = actionType;
                    nugetUi.SelectedIndex = model.SelectedIndex;
                    nugetUi.RecommendedCount = model.RecommendedCount;
                    nugetUi.RecommendPackages = model.RecommendPackages;
                    nugetUi.RecommenderVersion = model.RecommenderVersion;
                });
        }
    }
}
