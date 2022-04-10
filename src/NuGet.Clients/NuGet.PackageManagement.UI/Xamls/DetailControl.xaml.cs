// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The DataContext of this control is <see cref="DetailControlModel" />, i.e. either
    /// <see cref="PackageSolutionDetailControlModel" /> or <see cref="PackageDetailControlModel"/>
    /// </summary>
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
                _solutionView.InstallButtonClicked += InstallButtonClicked;
                _solutionView.UninstallButtonClicked += UninstallButtonClicked;

                _projectView.InstallButtonClicked -= InstallButtonClicked;
                _projectView.UninstallButtonClicked -= UninstallButtonClicked;
            }
            else
            {
                _projectView.InstallButtonClicked += InstallButtonClicked;
                _projectView.UninstallButtonClicked += UninstallButtonClicked;

                _solutionView.InstallButtonClicked -= InstallButtonClicked;
                _solutionView.UninstallButtonClicked -= UninstallButtonClicked;
            }
        }

        /// <summary>
        /// Handles Hyperlink controls inside this DetailControl class associated with
        /// <see cref="PackageManagerControlCommands.OpenExternalLink" />
        /// </summary>
        /// <param name="sender">A Hyperlink control</param>
        /// <param name="e">Command arguments</param>
        private void ExecuteOpenExternalLink(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.OriginalSource is Hyperlink hyperlink && hyperlink.NavigateUri != null)
            {
                Control.Model.UIController.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;

                if (e.Parameter is not null and HyperlinkType hyperlinkType)
                {
                    var evt = new HyperlinkClickedTelemetryEvent(hyperlinkType, UIUtility.ToContractsItemFilter(Control.ActiveFilter), Control.Model.IsSolution);
                    TelemetryActivity.EmitTelemetryEvent(evt);
                }
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
                if (DataContext is DetailControlModel model)
                {
                    await model.RefreshAsync(CancellationToken.None);
                }
            }).PostOnFailure(nameof(DetailControl), nameof(Refresh));
        }

        private void UninstallButtonClicked(object sender, EventArgs e)
        {
            if (DataContext is DetailControlModel model)
            {
                var userAction = UserAction.CreateUnInstallAction(model.Id, Control.Model.IsSolution, UIUtility.ToContractsItemFilter(Control._topPanel.Filter));
                ExecuteUserAction(userAction, NuGetActionType.Uninstall);
            }
        }

        private void InstallButtonClicked(object sender, EventArgs e)
        {
            if (DataContext is DetailControlModel model && model.SelectedVersion != null)
            {
                var userAction = UserAction.CreateInstallAction(
                    model.Id,
                    model.SelectedVersion.Version,
                    Control.Model.IsSolution,
                    UIUtility.ToContractsItemFilter(Control._topPanel.Filter),
                    model.SelectedVersion.Range);

                ExecuteUserAction(userAction, NuGetActionType.Install);
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
                    nugetUi.TopLevelVulnerablePackagesCount = model.IsPackageVulnerable ? 1 : 0;
                    nugetUi.TopLevelVulnerablePackagesMaxSeverities = new List<int>() { model.PackageVulnerabilityMaxSeverity };
                });
        }
    }
}
