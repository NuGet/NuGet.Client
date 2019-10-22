using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;

namespace NuGet.Configuration
{
    public class SettingsLoadingContext : IDisposable
    {
        private IList<SettingsFile> _settingsFiles = new List<SettingsFile>();
        private readonly SemaphoreSlim _semaphore;
        private bool _isDisposed;

        public SettingsLoadingContext()
        {
            _semaphore = new SemaphoreSlim(1, 1);
        }

        internal SettingsFile GetOrCreateSettingsFile(string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(string.Format(CultureInfo.CurrentCulture, Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(filePath)));
            }
            _semaphore.Wait();
            try
            {
                for (int i = 0; i < _settingsFiles.Count; i++)
                {
                    if (_settingsFiles[i].ConfigFilePath.Equals(filePath))
                    {
                        return _settingsFiles[i];
                    }
                }
                var file = new FileInfo(filePath);
                var settingsFile = new SettingsFile(file.DirectoryName, file.Name);
                _settingsFiles.Add(settingsFile);
                return settingsFile;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _semaphore.Dispose();

            GC.SuppressFinalize(this);

            _isDisposed = true;
        }
    }
}
