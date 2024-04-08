// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadMePreviewViewModel : ViewModelBase
    {
        public ReadMePreviewViewModel()
        {
            _isReadMeAvailable = false;
            _isErrorWithReadMe = false;
            _rawReadMe = string.Empty;
        }

        private bool _isErrorWithReadMe;

        public bool IsErrorWithReadMe
        {
            get => _isErrorWithReadMe;
            set
            {
                _isErrorWithReadMe = value;
                RaisePropertyChanged(nameof(IsErrorWithReadMe));
            }
        }

        private bool _isReadMeAvailable;

        public bool IsReadMeAvailable
        {
            get => _isReadMeAvailable;
            set
            {
                _isReadMeAvailable = value;
                RaisePropertyChanged(nameof(IsReadMeAvailable));
            }
        }

        private string _rawReadMe;

        public string ReadMeMarkdown
        {
            get => _rawReadMe;
            set
            {
                _rawReadMe = value;
                RaisePropertyChanged(nameof(ReadMeMarkdown));
            }
        }

        public async Task LoadReadme(string packagePath, string packageId)
        {
            var newReadMeValue = string.Empty;
            var isReadMeAvailable = false;
            var isErrorWithReadMe = false;
            try
            {
                if (!string.IsNullOrEmpty(packagePath))
                {
                    var fileInfo = new FileInfo(packagePath);
                    if (fileInfo.Exists)
                    {
                        using var stream = fileInfo.OpenRead();
                        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                        var nuspecZipArchiveEntry = archive.Entries.FirstOrDefault(zipEntry => string.Equals(UnescapePath(zipEntry.Name), $"{packageId}{PackagingCoreConstants.NuspecExtension}", StringComparison.OrdinalIgnoreCase));
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
                                    newReadMeValue = readMeContents;
                                    isReadMeAvailable = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                isErrorWithReadMe = true;
                isReadMeAvailable = true;
                await TelemetryUtility.PostFaultAsync(ex, nameof(ReadMePreviewViewModel));
            }
            finally
            {
                IsReadMeAvailable = isReadMeAvailable;
                IsErrorWithReadMe = isErrorWithReadMe;
                ReadMeMarkdown = newReadMeValue;
            }
        }

        private static string UnescapePath(string path)
        {
            if (path != null && path.IndexOf("%", StringComparison.Ordinal) > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }
    }
}
