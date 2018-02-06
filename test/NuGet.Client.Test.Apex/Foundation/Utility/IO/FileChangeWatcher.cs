using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NuGetClient.Test.Foundation.Utility.Diagnostics;

namespace NuGetClient.Test.Foundation.Utility.IO
{
    internal class FileChangeWatcher : IFileChangeWatcher
    {
#if DEBUG
        private string originatingCallStack = new StackTrace().ToString();
#endif

        private FileChangeWatcherService fileChangeWatcherService = null;

        private object syncLock = new object();
        private HashSet<string> watchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<FileSystemEventArgs> FileChanged;

        internal FileChangeWatcher(FileChangeWatcherService fileChangeWatcherService)
        {
            this.fileChangeWatcherService = fileChangeWatcherService;
            this.fileChangeWatcherService.FileChanged += this.FileChangeWatcherService_FileChanged;
        }

        private void FileChangeWatcherService_FileChanged(object sender, FileSystemEventArgs e)
        {
            bool raiseEvent = false;
            lock (this.syncLock)
            {
                string trackedFileName = (e.ChangeType == WatcherChangeTypes.Renamed)
                    ? ((RenamedEventArgs)e).OldFullPath
                    : e.FullPath;

                raiseEvent = this.watchedFiles.Contains(trackedFileName);
            }

            if (raiseEvent)
            {
                if (this.FileChanged != null)
                {
                    this.FileChanged(this, e);
                }
            }
        }

        public void WatchFile(string filePath)
        {
            this.fileChangeWatcherService.WatchFile(filePath);
            lock (this.syncLock)
            {
                this.watchedFiles.Add(filePath);
            }
        }

        public void StopWatchingFile(string filePath)
        {
            this.fileChangeWatcherService.StopWatchingFile(filePath);
            lock (this.syncLock)
            {
                this.watchedFiles.Remove(filePath);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.fileChangeWatcherService.FileChanged -= this.FileChangeWatcherService_FileChanged;
                lock (this.syncLock)
                {
                    foreach (string filePath in this.watchedFiles)
                    {
                        fileChangeWatcherService.StopWatchingFile(filePath);
                    }
                    this.watchedFiles.Clear();
                }
            }
        }

#if DEBUG
        ~FileChangeWatcher()
        {
            DebugHelper.FinalizerFail("Failed to dispose FileChangeWatcher", this.originatingCallStack);
            this.Dispose(false);
        }
#endif

    }
}
