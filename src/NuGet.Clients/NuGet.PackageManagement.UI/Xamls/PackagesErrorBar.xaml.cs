// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI
{
    [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD010", Justification = "NuGet/Home#4833 Baseline")]
    public partial class PackagesErrorBar : UserControl, INuGetProjectContext
    {
        private readonly ISolutionManager _solutionManager;

        public event EventHandler RefreshUI;

        public event EventHandler InitializationCompleted;

        public PackageExtractionV2Context PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ProjectManagement.ExecutionContext ExecutionContext { get; }

        public XDocument OriginalPackagesConfig { get; set; }

        public NuGetActionType ActionType { get; set; }

        public TelemetryServiceHelper TelemetryService { get; set; }

        public PackagesErrorBar(ISolutionManager solutionManager)
        {
            InitializeComponent();

            _solutionManager = solutionManager;

            // Start a task to check for all the project's packages file and accordingly update error bar.
            // this would also be initlize VSSolutionManager first time, if it hasn't been initialized yet.
            Task.Run(UpdateErrorBarAsync);

            ErrorMessage.SetResourceReference(TextBlock.ForegroundProperty, VsBrushes.InfoTextKey);
            ErrorBar.SetResourceReference(Border.BackgroundProperty, VsBrushes.InfoBackgroundKey);
            ErrorBar.SetResourceReference(Border.BorderBrushProperty, VsBrushes.ActiveBorderKey);
        }

        private async Task UpdateErrorBarAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var projects = await _solutionManager.GetNuGetProjectsAsync();
                foreach (var project in projects)
                {
                    await project.GetInstalledPackagesAsync(CancellationToken.None);
                }

                ErrorBar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = ex.Message;
                ErrorBar.Visibility = Visibility.Visible;
            }
            finally
            {
                InitializationCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ExecuteRefresh(object sender, EventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(UpdateErrorBarAsync);

            // raise RefreshUI event to refresh the manager ui
            RefreshUI?.Invoke(this, EventArgs.Empty);
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            if (args.Length > 0)
            {
                message = string.Format(CultureInfo.CurrentCulture, message, args);
            }

            ShowMessage(message);
        }

        public void CleanUp()
        {
            // for now, there is nothing to be cleaned but we might need it in future so it's already
            // been called from main component.
        }

        public void ReportError(string message)
        {
            ShowMessage(message);
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        private void ShowMessage(string message)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ErrorMessage.Text = message;
            });
        }
    }
}