// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Markdown.Platform;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataReadMeControl.xaml
    /// </summary>
    public partial class PackageMetadataReadMeControl : UserControl, IDisposable
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private IMarkdownPreview _markdownPreview;
#pragma warning restore CS0618 // Type or member is obsolete
        private bool _disposed = false;

        private ReadMePreviewViewModel ReadMeViewModel { get => (ReadMePreviewViewModel)DataContext; }

        [Obsolete]
        public PackageMetadataReadMeControl()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _markdownPreview?.Dispose();
                var viewModel = (ReadMePreviewViewModel)DataContext;
                viewModel.PropertyChanged -= ViewModel_PropertyChangedAsync;
            }

            _disposed = true;
        }

        private void PackageMetadataReadMeControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var oldViewModel = (ReadMePreviewViewModel)e.OldValue;
            if (oldViewModel != null)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChangedAsync;
            }
            ReadMeViewModel.PropertyChanged += ViewModel_PropertyChangedAsync;
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void ViewModel_PropertyChangedAsync(object sender, System.ComponentModel.PropertyChangedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (ReadMeViewModel is not null && e.PropertyName == nameof(ReadMeViewModel.ReadMeMarkdown))
            {
                try
                {
                    await UpdateMarkdownAsync(ReadMeViewModel.ReadMeMarkdown);
                }
                catch (Exception ex)
                {
                    ReadMeViewModel.IsErrorWithReadMe = true;
                    await TelemetryUtility.PostFaultAsync(ex, nameof(ReadMePreviewViewModel));
                }
            }
        }

        private async Task UpdateMarkdownAsync(string markDown)
        {
            if (_markdownPreview is null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                _markdownPreview = new PreviewBuilder().Build();
                descriptionMarkdownPreview.Content = _markdownPreview.VisualElement;
#pragma warning restore CS0618 // Type or member is obsolete
            }
            if (DataContext is ReadMePreviewViewModel viewModel)
            {
                viewModel.IsErrorWithReadMe = false;
                if (!string.IsNullOrWhiteSpace(markDown))
                {
                    await _markdownPreview.UpdateContentAsync(markDown, ScrollHint.None);
                }
            }
        }
    }
}
