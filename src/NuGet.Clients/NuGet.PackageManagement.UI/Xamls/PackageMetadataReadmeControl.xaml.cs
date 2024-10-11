// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Markdown.Platform;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataReadmeControl.xaml
    /// </summary>
    public partial class PackageMetadataReadmeControl : UserControl, IDisposable
    {
        internal CancellationTokenSource _loadCts;

        private bool _renderLocalReadme = false;

        public static readonly DependencyProperty PackageMetadataProperty =
            DependencyProperty.Register(
                nameof(PackageMetadata),
                typeof(DetailedPackageMetadata),
                typeof(PackageMetadataReadmeControl),
                new PropertyMetadata(OnPropertyChanged));

#pragma warning disable CS0618 // Type or member is obsolete
        private IMarkdownPreview _markdownPreview;
#pragma warning restore CS0618 // Type or member is obsolete
        private bool _disposed = false;

        private ReadmePreviewViewModel ReadmeViewModel { get => (ReadmePreviewViewModel)DataContext; }

        public PackageMetadataReadmeControl()
        {
            InitializeComponent();
            _loadCts = new CancellationTokenSource();
#pragma warning disable CS0618 // Type or member is obsolete
            _markdownPreview = new PreviewBuilder().Build();
#pragma warning restore CS0618 // Type or member is obsolete
            descriptionMarkdownPreview.Content = _markdownPreview.VisualElement;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public DetailedPackageMetadata PackageMetadata
        {
            get
            {
                return (DetailedPackageMetadata)GetValue(PackageMetadataProperty);
            }
            set
            {
                UpdateControl(PackageMetadata, value);
                SetValue(PackageMetadataProperty, value);
            }
        }

        public void OnTabFilterChange(ItemFilter filter)
        {
            _renderLocalReadme = filter != ItemFilter.All;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _loadCts.Cancel();
                _loadCts.Dispose();
                _markdownPreview?.Dispose();
            }

            _disposed = true;
        }

        private static void OnPropertyChanged(
           DependencyObject dependencyObject,
           DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == PackageMetadataProperty && dependencyObject is PackageMetadataReadmeControl control)
            {
                control?.UpdateControl((DetailedPackageMetadata)e.OldValue, (DetailedPackageMetadata)e.NewValue);
            }
        }

        private void UpdateControl(DetailedPackageMetadata oldValue, DetailedPackageMetadata newValue)
        {
            if (newValue is not null &&
                (oldValue?.Id != newValue.Id ||
                oldValue?.Version != newValue.Version))
            {
                var loadCts = new CancellationTokenSource();
                var oldCts = Interlocked.Exchange(ref _loadCts, loadCts);
                oldCts?.Cancel();
                oldCts?.Dispose();

                NuGetUIThreadHelper.JoinableTaskFactory
                .RunAsync(async () =>
                {
                    await ReadmeViewModel.LoadReadmeAsync(newValue, _renderLocalReadme, _loadCts.Token);
                    await UpdateMarkdownAsync();
                })
                .PostOnFailure(nameof(PackageMetadataReadmeControl));
            }
        }

        private async Task UpdateMarkdownAsync()
        {
            var markdown = string.Empty;
            if (ReadmeViewModel?.CanDetermineReadmeDefined == true)
            {
                markdown = string.IsNullOrWhiteSpace(ReadmeViewModel.ReadmeMarkdown)
                    ? UI.Resources.Text_NoReadme
                    : ReadmeViewModel.ReadmeMarkdown;
            }

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                await _markdownPreview.UpdateContentAsync(markdown, ScrollHint.None);
                descriptionMarkdownPreview.Visibility = string.IsNullOrEmpty(markdown) ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                ReadmeViewModel.ErrorLoadingReadme = true;
                descriptionMarkdownPreview.Visibility = Visibility.Collapsed;
                await TelemetryUtility.PostFaultAsync(ex, nameof(ReadmePreviewViewModel));
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
    }
}
