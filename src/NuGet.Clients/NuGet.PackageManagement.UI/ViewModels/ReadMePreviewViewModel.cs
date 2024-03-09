// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
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
            _ = UpdateMarkdownAsync("", "");
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

        public async Task UpdateMarkdownAsync(string packagePath, string packageName)
        {
            _markdownPreview.VisualElement.Visibility = Visibility.Collapsed;
            await _markdownPreview.UpdateContentAsync("", ScrollHint.None);
            if (!string.IsNullOrEmpty(packagePath))
            {
                var fileInfo = new FileInfo(packagePath);
                if (fileInfo.Exists)
                {
                    using var stream = fileInfo.OpenRead();
                    var markDown = await GetReadMeMD(stream, packageName);
                    if (!string.IsNullOrWhiteSpace(markDown))
                    {
                        await _markdownPreview.UpdateContentAsync(markDown, ScrollHint.None);
                        _markdownPreview.VisualElement.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        public async Task<string> GetReadMeMD(Stream stream, string packageName)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var nuspecZipArchiveEntry = archive.Entries.FirstOrDefault(zipEntry => string.Equals(UnescapePath(zipEntry.Name), $"{packageName}.nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspecZipArchiveEntry is not null)
            {
                using var nuspecFile = nuspecZipArchiveEntry.Open();
                var nuspecReader = new NuspecReader(nuspecFile);
                var readMePath = nuspecReader.GetReadme();
                if (!string.IsNullOrEmpty(readMePath))
                {
                    var readmeZipArchiveEntry = archive.Entries.FirstOrDefault(zipEntry => string.Equals(UnescapePath(zipEntry.FullName), readMePath, StringComparison.OrdinalIgnoreCase));
                    if (readmeZipArchiveEntry is not null)
                    {
                        using var readMeFile = readmeZipArchiveEntry.Open();
                        using var readMeStreamReader = new StreamReader(readMeFile);
                        var readMeContents = await readMeStreamReader.ReadToEndAsync();
                        return readMeContents;
                    }
                }
            }
            return string.Empty;
        }
        private static string UnescapePath(string path)
        {
            if (path != null && path.IndexOf("%", StringComparison.Ordinal) > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        public void Dispose()
        {
            _markdownPreview?.Dispose();
            _markdownPreview = null;
        }
    }
}
