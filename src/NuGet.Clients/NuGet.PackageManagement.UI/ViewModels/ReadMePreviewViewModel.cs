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
            PathString = "Hello";
            MarkdownPreviewControl = _markdownPreview.VisualElement;
        }

        private string _path;

        public string PathString
        {
            get => _path;
            private set
            {
                _path = value;
                RaisePropertyChanged(nameof(PathString));
            }
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
            //var markDown = string.Empty;
            var markDown = "# About xUnit.net\r\n\r\n[<img align=\"right\" width=\"100px\" src=\"https://raw.githubusercontent.com/xunit/media/main/dotnet-foundation.svg\" />](https://dotnetfoundation.org/projects/project-detail/xunit)\r\n\r\nxUnit.net is a free, open source, community-focused unit testing tool for the .NET Framework. Written by the original inventor of NUnit v2, xUnit.net is the latest technology for unit testing C# and F# (other .NET languages may work as well, but are unsupported). xUnit.net works with Visual Studio, Visual Studio Code, ReSharper, CodeRush, and TestDriven.NET. It is part of the [.NET Foundation](https://www.dotnetfoundation.org/), and operates under their [code of conduct](https://www.dotnetfoundation.org/code-of-conduct). It is licensed under [Apache 2](https://opensource.org/licenses/Apache-2.0) (an OSI approved license)";
            //if (!string.IsNullOrEmpty(packagePath))
            //{
            //    var fileInfo = new FileInfo(packagePath);
            //    if (fileInfo.Exists)
            //    {
            //        using var pfr = new PackageArchiveReader(fileInfo.OpenRead());
            //        var files = await pfr.GetFilesAsync(CancellationToken.None);
            //        var readmeFile = files.FirstOrDefault(file => file.IndexOf("readme.md", System.StringComparison.OrdinalIgnoreCase) >= 0);
            //        if (!string.IsNullOrEmpty(readmeFile))
            //        {
            //            using var stream = new StreamReader(await pfr.GetStreamAsync(readmeFile, CancellationToken.None));
            //            markDown = await stream.ReadToEndAsync();
            //        }
            //    }
            //}
            await _markdownPreview.UpdateContentAsync(markDown, ScrollHint.None);
        }

        public void Dispose()
        {
            _markdownPreview?.Dispose();
            _markdownPreview = null;
        }
    }
}
