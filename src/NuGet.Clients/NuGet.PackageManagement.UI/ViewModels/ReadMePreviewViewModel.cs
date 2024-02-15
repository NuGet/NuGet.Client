// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Markdown.Platform;
using NuGet.Packaging;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadMePreviewViewModel : ViewModelBase, IDisposable
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private IMarkdownPreview _markdownPreview;
#pragma warning restore CS0618 // Type or member is obsolete

        public ReadMePreviewViewModel()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            _markdownPreview = new PreviewBuilder().Build();
#pragma warning restore CS0618 // Type or member is obsolete
            _ = UpdateMarkdownAsync("");
            MarkdownPreviewControl = _markdownPreview.VisualElement;
        }

        private FrameworkElement _markdownPreviewControl;
        /// <summary>
        /// The markdown preview control if it's available
        /// </summary>
        public FrameworkElement MarkdownPreviewControl
        {
            get => _markdownPreviewControl;
            private set
            {
                _markdownPreviewControl = value;
                RaisePropertyChanged(nameof(MarkdownPreviewControl));
            }
        }

        public async Task UpdateMarkdownAsync(string packagePath)
        {
            var markDown = string.Empty;
            _markdownPreview.VisualElement.Visibility = Visibility.Collapsed;
            if (!string.IsNullOrEmpty(packagePath))
            {
                var fileInfo = new FileInfo(packagePath);
                if (fileInfo.Exists)
                {
                    using var pfr = new PackageArchiveReader(fileInfo.OpenRead());
                    var files = await pfr.GetFilesAsync(CancellationToken.None);
                    var readmeFile = files.FirstOrDefault(file => file.IndexOf("readme.md", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!string.IsNullOrEmpty(readmeFile))
                    {
                        using var stream = new StreamReader(await pfr.GetStreamAsync(readmeFile, CancellationToken.None));
                        markDown = await stream.ReadToEndAsync();
                        _markdownPreview.VisualElement.Visibility = Visibility.Visible;
                    }
                }
            }
            await _markdownPreview.UpdateContentAsync(markDown, ScrollHint.None);
        }

        public void Dispose()
        {
            _markdownPreview?.Dispose();
            _markdownPreview = null;
        }
    }
}
