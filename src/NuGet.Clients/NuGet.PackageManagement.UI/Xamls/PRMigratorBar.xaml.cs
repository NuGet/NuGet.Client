// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public partial class PRMigratorBar : UserControl, INuGetProjectContext
    {
        private readonly PackageManagerModel _model;

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ProjectManagement.ExecutionContext ExecutionContext { get; }

        public XDocument OriginalPackagesConfig { get; set; }

        public NuGetActionType ActionType { get; set; }

        public Guid OperationId { get; set; }

        public PRMigratorBar(PackageManagerModel model)
        {
            InitializeComponent();
            _model = model;

            UpgradeMessage.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.InfoTextKey);
            MigratorBar.SetResourceReference(Border.BackgroundProperty, VsBrushes.InfoBackgroundKey);
            MigratorBar.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ActiveBorderKey);
        }

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            if (args.Length > 0)
            {
                message = string.Format(CultureInfo.CurrentCulture, message, args);
            }

            ShowMessage(message);
        }

        public void ReportError(string message)
        {
            ShowMessage(message);
        }

        private void ShowMessage(string message)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                UpgradeMessage.Text = message;
            });
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (await ShouldShowUpgradeProjectAsync())
                {
                    ShowMigratorBar();
                }
                else
                {
                    HideMigratorBar();
                }
            });
        }

        private async Task<bool> ShouldShowUpgradeProjectAsync()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // If user has turned it off, don't show
            if (RegistrySettingUtility.GetBooleanSetting(Constants.SuppressUpgradePackagesConfigName))
            {
                return false;
            }

            // We don't currently support converting an entire solution
            if (_model.IsSolution)
            {
                return false;
            }

            // We only support a single project
            var projects = _model.Context.Projects.ToList();
            return (projects.Count == 1) && await _model.Context.IsNuGetProjectUpgradeable(projects[0]);
        }

        private void HideMigratorBar()
        {
            MigratorBar.Visibility = Visibility.Collapsed;
        }

        private void ShowMigratorBar()
        {
            MigratorBar.Visibility = Visibility.Visible;
        }

        private void OnMigrationLinkClick(object sender, RoutedEventArgs e)
        {
            var project = _model.Context.Projects.FirstOrDefault();
            Debug.Assert(project != null);

            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await _model.Context.UIActionEngine.UpgradeNuGetProjectAsync(_model.UIController, project);
            });
        }

        private void OnDoNotShowAgainClick(object sender, RoutedEventArgs e)
        {
            RegistrySettingUtility.SetBooleanSetting(Constants.SuppressUpgradePackagesConfigName, true);
            MigratorBar.Visibility = Visibility.Collapsed;
        }

        private void OnMigrationHelpUrlNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var hyperlink = (Hyperlink)sender;
            if (hyperlink != null
                && hyperlink.NavigateUri != null)
            {
                UIUtility.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }

        private void OnDeclineMigrationLinkClick(object sender, RoutedEventArgs e)
        {
            HideMigratorBar();
        }
    }
}