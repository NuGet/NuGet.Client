// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
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
            var isErrorWithReadMe = false;
            try
            {
                if (!string.IsNullOrEmpty(packagePath))
                {
                    var packageDirectory = Path.GetDirectoryName(packagePath);
                    var nuspecPath = Path.Combine(packageDirectory, $"{packageId}{PackagingCoreConstants.NuspecExtension}");
                    var nuspectFileInfo = new FileInfo(nuspecPath);
                    if (nuspectFileInfo.Exists)
                    {
                        var nuspecReader = new NuspecReader(nuspecPath);
                        var readMePath = nuspecReader.GetReadme();
                        if (!string.IsNullOrEmpty(readMePath))
                        {
                            var readMeFullPath = Path.Combine(packageDirectory, readMePath);
                            var readMeFileInfo = new FileInfo(readMeFullPath);
                            if (readMeFileInfo.Exists)
                            {
                                using var readMeStreamReader = readMeFileInfo.OpenText();
                                var readMeContents = await readMeStreamReader.ReadToEndAsync();
                                newReadMeValue = readMeContents;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                isErrorWithReadMe = true;
                await TelemetryUtility.PostFaultAsync(ex, nameof(ReadMePreviewViewModel));
            }
            finally
            {
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
