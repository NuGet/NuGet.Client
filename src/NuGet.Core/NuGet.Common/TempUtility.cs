// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;

namespace NuGet.Common
{
    public class TempFile : IDisposable
    {
        private readonly string _filePath;

        /// <summary>
        /// Constructor. It creates an empty temp file under the temp directory / NuGet, with
        /// extension <paramref name="extension"/>.
        /// </summary>
        /// <param name="extension">The extension of the temp file.</param>
        public TempFile(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                throw new ArgumentNullException(nameof(extension));
            }

            var tempDirectory = Path.GetTempPath();
            var randomFolderName = Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.Combine(tempDirectory, randomFolderName));
            _filePath = Path.Combine(tempDirectory, Guid.NewGuid().ToString() + extension);

            if (!File.Exists(_filePath))
            {
                try
                {
                    File.Create(_filePath).Dispose();
                    // file is created successfully.
                    return;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedToCreateRandomFile, _filePath) + " : " +
                            ex.Message,
                            ex);
                }
            }
        }

        public TempFile()
        {
            var tempDirectory = Path.GetTempPath();
            var randomFolderName = Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.Combine(tempDirectory, randomFolderName));
            _filePath = Path.Combine(tempDirectory, Guid.NewGuid().ToString());

            if (!File.Exists(_filePath))
            {
                try
                {
                    File.Create(_filePath).Dispose();
                    // file is created successfully.
                    return;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedToCreateRandomFile, _filePath) + " : " +
                            ex.Message,
                            ex);
                }
            }
        }

        public static implicit operator string(TempFile f)
        {
            return f._filePath;
        }

        public void Dispose()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }
}