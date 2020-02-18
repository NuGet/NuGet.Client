// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;

namespace NuGet.CommandLine.Test
{
    /// <summary>
    /// Helps ensuring only one unit-test can backup/restore the global nuget.config at a time.
    /// </summary>
    public sealed class DefaultConfigurationFilePreserver : IDisposable
    {
        private const string MutexName = "DefaultConfigurationFilePreserver";
        private readonly Mutex _mutex;
        private bool _disposed = false;

        public DefaultConfigurationFilePreserver()
        {
            _mutex = new Mutex(initiallyOwned: false, MutexName);
            var owner = _mutex.WaitOne(TimeSpan.FromMinutes(2));
            if (!owner)
            {
                throw new TimeoutException(string.Format("Timedout while waiting for mutex {0}", MutexName));
            }
            BackupAndDeleteDefaultConfigurationFile();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    RestoreDefaultConfigurationFile();
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                }

                _disposed = true;
            }
        }

        ~DefaultConfigurationFilePreserver()
        {
            Dispose(false);
        }

        private static void BackupAndDeleteDefaultConfigurationFile()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string defaultConfigurationFile = Path.Combine(appDataPath, "NuGet", "NuGet.Config");
            string backupFileName = defaultConfigurationFile + ".backup";

            if (File.Exists(defaultConfigurationFile))
            {
                File.Copy(defaultConfigurationFile, backupFileName, true);
                File.Delete(defaultConfigurationFile);
            }
        }

        private static void RestoreDefaultConfigurationFile()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string defaultConfigurationFile = Path.Combine(appDataPath, "NuGet", "NuGet.Config");
            string backupFileName = defaultConfigurationFile + ".backup";

            if (File.Exists(backupFileName))
            {
                File.Copy(backupFileName, defaultConfigurationFile, true);
                File.Delete(backupFileName);
            }
        }
    }
}
