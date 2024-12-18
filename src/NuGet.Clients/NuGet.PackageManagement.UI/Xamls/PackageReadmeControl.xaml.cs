// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Markdown.Platform;
using Microsoft.VisualStudio.Text.Editor;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for PackageReadmeControl.xaml
    /// </summary>
    public partial class PackageReadmeControl : UserControl
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private IMarkdownPreview _markdownPreview;
#pragma warning restore CS0618 // Type or member is obsolete

        public PackageReadmeControl()
        {
            InitializeComponent();
        }

        public ReadmePreviewViewModel ReadmeViewModel { get => (ReadmePreviewViewModel)DataContext; }

        public bool Initialize(IEditorOptionsFactoryService options)
        {
            if (options is null)
            {
                return false;
            }
            if (_markdownPreview is null)
            {
                // This class is marked as obsolete because the api hasn't been finalized, however we want to use IMarkdownPreview to maintain a centralized way of rendering markdown into html.
#pragma warning disable CS0618 // Type or member is obsolete
                var previewBuilder = new PreviewBuilder();
#pragma warning restore CS0618 // Type or member is obsolete
                previewBuilder.EditorOptions = options.GlobalOptions;
                _markdownPreview = previewBuilder.Build();
                descriptionMarkdownPreview.Content = _markdownPreview?.VisualElement;
            }
            return true;
        }

        private void ReadmeViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReadmePreviewViewModel.ReadmeMarkdown))
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(UpdateMarkdownAsync);
            }
        }

        private async Task UpdateMarkdownAsync()
        {
            try
            {
                if (_markdownPreview is not null && !string.IsNullOrWhiteSpace(ReadmeViewModel.ReadmeMarkdown))
                {
                    await _markdownPreview.UpdateContentAsync(ReadmeViewModel.ReadmeMarkdown, ScrollHint.None);
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                ReadmeViewModel.ErrorWithReadme = true;
                ReadmeViewModel.ReadmeMarkdown = string.Empty;
                await TelemetryUtility.PostFaultAsync(ex, nameof(ReadmePreviewViewModel));
            }
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ReadmePreviewViewModel oldMetadata)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await _markdownPreview?.UpdateContentAsync("", ScrollHint.None);
                });
                oldMetadata.PropertyChanged -= ReadmeViewModel_PropertyChanged;
            }
            if (ReadmeViewModel is not null)
            {
                ReadmeViewModel.PropertyChanged += ReadmeViewModel_PropertyChanged;
            }
        }

        private void PackageReadmeControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ReadmeViewModel is not null && _markdownPreview is not null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await _markdownPreview.UpdateContentAsync("", ScrollHint.None);
                });
            }
        }

        private void PackageReadmeControl_Loaded(object sender, RoutedEventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(UpdateMarkdownAsync);
        }
    }
}
