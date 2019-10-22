// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using NuGet.Common;

namespace NuGet.Configuration
{
    public sealed class SettingsLoadingContext : IDisposable
    {
        private readonly IList<Lazy<SettingsFile>> _settingsFiles = new List<Lazy<SettingsFile>>();
        private readonly SemaphoreSlim _semaphore;
        private bool _isDisposed;

        public SettingsLoadingContext()
        {
            _semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }

        internal SettingsFile GetOrCreateSettingsFile(string filePath)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SettingsLoadingContext));
            }

            if (filePath == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(filePath)));
            }

            _semaphore.Wait();
            Lazy<SettingsFile> settingsFile = null;

            try
            {
                for (int i = 0; i < _settingsFiles.Count; i++)
                {
                    if (PathUtility.GetStringComparerBasedOnOS().Equals(_settingsFiles[i].Value.ConfigFilePath, filePath))
                    {
                        return _settingsFiles[i].Value;
                    }
                }

                var file = new FileInfo(filePath);
                settingsFile = new Lazy<SettingsFile>(() => new SettingsFile(file.DirectoryName, file.Name));
                _settingsFiles.Add(settingsFile);
            }
            finally
            {
                _semaphore.Release();
            }

            return settingsFile?.Value;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _semaphore.Dispose();
            _settingsFiles.Clear();

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }
    }
}
