using NuGetClient.Test.Foundation.Utility.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetClient.Test.Foundation.Utility.IO
{
    internal class FileChangeWatcherService : IFileChangeWatcherService
    {
        public event EventHandler<FileSystemEventArgs> FileChanged;

        private Dictionary<string, WatchedDirectory> pathToWatchedDirectories =
            new Dictionary<string, WatchedDirectory>(StringComparer.OrdinalIgnoreCase);
        private object syncLock = new object();

        internal FileChangeWatcherService() { }

        public IFileChangeWatcher CreateFileChangeWatcher()
        {
            return new FileChangeWatcher(this);
        }

        public void WatchFile(string filePath)
        {
            bool watchRecursively = false;
            string directory = PathHelper.GetDirectory(filePath);

            if (!FileChangeWatcherService.DirectoryIsAccessible(directory))
            {
                watchRecursively = true;
                directory = FileChangeWatcherService.GetExistingParentDirectory(directory);
            }

            if (String.IsNullOrEmpty(directory))
            {
                // Not a lot we can do
                return;
            }

            if (PathHelper.IsPathRelative(directory))
            {
                Debug.Fail("We cannot watch a relative path directory!");
                return;
            }

            WatchedDirectory watchedDirectory;

            lock (this.syncLock)
            {
                if (!this.pathToWatchedDirectories.TryGetValue(directory, out watchedDirectory))
                {
                    // Don't already have one for this directory
                    watchedDirectory = new WatchedDirectory(this, directory);
                    this.pathToWatchedDirectories.Add(directory, watchedDirectory);
                }
            }

            // If the existing watcher says watch recursively we want to leave it that way
            if (watchRecursively) { watchedDirectory.IncludeSubdirectories = watchRecursively; }
            watchedDirectory.WatchFile(filePath);
        }

        private void MoveToParentDirectory(WatchedDirectory watchedDirectory)
        {
            string oldDirectory = watchedDirectory.Path;
            if (!this.pathToWatchedDirectories.Remove(oldDirectory))
            {
                Debug.Fail("Didn't find the existing directory's key??");
            }

            string parentDirectory = FileChangeWatcherService.GetExistingParentDirectory(watchedDirectory.Path);
            if (String.IsNullOrEmpty(parentDirectory))
            {
                // Something really unexpected happened here. We'll hope that the watched directory
                // won't be deleted and just press on. (The other option is to stop watching, which
                // seems like the greater of two evils.)
                Debug.Fail("Shouldn't hit this issue when reparenting a watched directory");
                return;
            }

            if (PathHelper.IsPathRelative(parentDirectory))
            {
                Debug.Fail("We cannot watch a relative path directory!");
                return;
            }

            string[] oldFiles = watchedDirectory.FilePaths.ToArray();
            Debug.Assert(oldFiles.Length > 0);

            if (!this.pathToWatchedDirectories.TryGetValue(parentDirectory, out watchedDirectory))
            {
                // Don't already have one for the parent, create
                watchedDirectory = new WatchedDirectory(this, parentDirectory);
                this.pathToWatchedDirectories.Add(parentDirectory, watchedDirectory);
            }

            watchedDirectory.IncludeSubdirectories = true;
            foreach (string file in oldFiles)
            {
                watchedDirectory.WatchFile(file);
            }
        }

        private static bool DirectoryIsAccessible(string directory)
        {
            try
            {
                return PathHelper.DirectoryExists(directory);
            }
            catch (UnauthorizedAccessException)
            {
                // This happens during unit tests on some test machines
                Debug.WriteLine("Unauthorized access attempting to find directory '{0}'", args: new string[] { directory });
                return false;
            }
        }

        private static string GetExistingParentDirectory(string directory)
        {
            // Walk up until we find a directory that exists
            do
            {
                string parentDirectory = PathHelper.GetParentDirectory(directory);
                if (String.Equals(parentDirectory, directory, StringComparison.OrdinalIgnoreCase))
                {
                    // No luck in finding an existing directory
                    Debug.WriteLine("Failed to find an existing parent directory for filePath");
                    return null;
                }

                directory = parentDirectory;
            } while (!FileChangeWatcherService.DirectoryIsAccessible(directory));

            return directory;
        }

        public void StopWatchingFile(string filePath)
        {
            string directory = FileChangeWatcherService.GetExistingParentDirectory(filePath);
            if (String.IsNullOrEmpty(directory))
            {
                // Something really unexpected happened here. We'll hope that the watched directory
                // won't be deleted and just press on. (The other option is to stop watching, which
                // seems like the greater of two evils.)
                Debug.Fail("Shouldn't hit this issue when reparenting a watched directory");
                return;
            }

            WatchedDirectory watchedDirectory;

            lock (this.syncLock)
            {
                if (this.pathToWatchedDirectories.TryGetValue(directory, out watchedDirectory))
                {
                    if (watchedDirectory.StopWatchingFile(filePath))
                    {
                        // Still watching
                        return;
                    }
                    else
                    {
                        // No longer watching any files. (Don't want to leave open file handles any longer than we need to.)
                        this.pathToWatchedDirectories.Remove(directory);
                    }
                }
            }

            if (watchedDirectory != null) { watchedDirectory.Dispose(); }
        }

        public void StopWatchingDirectory(string directoryFullPath)
        {
            directoryFullPath = PathHelper.EnsurePathEndsInDirectorySeparator(directoryFullPath);
            List<WatchedDirectory> directoriesToDispose = new List<WatchedDirectory>();

            lock (this.syncLock)
            {
                // We were watching a sub directory but parent directory is getting deleted
                // ensure we stop watching all sub directories
                string[] watchedDirectories = this.pathToWatchedDirectories.Keys.ToArray();
                foreach (string watchedDirectoryPath in watchedDirectories)
                {
                    if (PathHelper.IsPathWithin(watchedDirectoryPath, directoryFullPath))
                    {
                        directoriesToDispose.Add(this.pathToWatchedDirectories[watchedDirectoryPath]);
                        this.pathToWatchedDirectories.Remove(watchedDirectoryPath);
                    }
                }
            }

            foreach (WatchedDirectory watchedDirectory in directoriesToDispose)
            {
                watchedDirectory.Dispose();
            }
        }

        public void StopWatchingAllDirectories()
        {
            lock (this.syncLock)
            {
                foreach (WatchedDirectory watchedDirectory in this.pathToWatchedDirectories.Values)
                {
                    watchedDirectory.Dispose();
                }
                this.pathToWatchedDirectories.Clear();
            }
        }
        private void OnFileChanged(WatchedDirectory sender, FileSystemEventArgs eventArgs, bool raiseEvent)
        {
            if (raiseEvent && this.FileChanged != null)
            {
                this.FileChanged(this, eventArgs);
            }

            // Don't bother if we have another change in progress
            if (!Monitor.TryEnter(this.syncLock)) { return; }

            try
            {
                if (!this.CanWatchChildren(sender))
                {
                    this.MoveToParentDirectory(sender);
                    sender.Dispose();
                }
            }
            finally
            {
                Monitor.Exit(this.syncLock);
            }
        }

        private bool CanWatchChildren(WatchedDirectory watchedDirectory)
        {
            bool canWatchChildren = true;
            ErrorHandling.HandleBasicExceptions(
                action: () =>
                {
                    if (!PathHelper.DirectoryExists(watchedDirectory.Path)
                        || !Directory.EnumerateFileSystemEntries(watchedDirectory.Path).Any())
                    {
                        // If the directory is gone we won't get any more events  *also*
                        // if the contents of the directory are completely gone we need to watch
                        // the parent directory as the current directory could be deleted and we
                        // will have no way of knowing.
                        canWatchChildren = false;
                    }
                },
                handledExceptionAction: (Exception exception) =>
                {
                    Debug.WriteLine("Threw exception trying to check on children of '{0}': {1}", watchedDirectory, exception.Message);
                    canWatchChildren = false;
                },
                exceptionHandlers: ErrorHandling.BasicIOExceptionHandler);

            return canWatchChildren;
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
                lock (this.syncLock)
                {
                    foreach (WatchedDirectory watchedDirectory in this.pathToWatchedDirectories.Values)
                    {
                        watchedDirectory.Dispose();
                    }

                    this.pathToWatchedDirectories.Clear();
                }
            }
        }

#if DEBUG
        ~FileChangeWatcherService()
        {
            DebugHelper.FinalizerAssert(this.pathToWatchedDirectories.Keys.Count == 0, "Failed to dispose FileChangeWatcherService");
            this.Dispose(false);
        }
#endif

        private class WatchedDirectory : FileSystemWatcher
        {
            private Dictionary<string, int> filePaths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private FileChangeWatcherService watcherService;

            internal WatchedDirectory(FileChangeWatcherService watcher, string directory)
                : base(directory)
            {
                this.watcherService = watcher;
                this.IncludeSubdirectories = false;

                // FileName filter is needed to get creation and deletion events. When a
                // file is created TWO events are fired- "Created" then "Changed". When
                // a file is deleted, ONLY "Deleted" is fired.
                this.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

                this.Changed += this.OnFileChanged;
                this.Deleted += this.OnFileChanged;
                this.Renamed += this.OnFileChanged;
            }

            public IEnumerable<string> FilePaths { get { return this.filePaths.Keys; } }

            internal void WatchFile(string filePath)
            {
                if (this.filePaths.ContainsKey(filePath))
                {
                    this.filePaths[filePath]++;
                }
                else
                {
                    filePaths.Add(filePath, 1);
                    this.EnableRaisingEvents = true;
                }
            }

            /// <returns>'true' if still watching any files</returns>
            internal bool StopWatchingFile(string filePath)
            {
                if (this.filePaths.ContainsKey(filePath))
                {
                    this.filePaths[filePath]--;
                    if (this.filePaths[filePath] == 0)
                    {
                        this.filePaths.Remove(filePath);
                    }
                }

                if (filePaths.Keys.Count == 0)
                {
                    this.EnableRaisingEvents = false;
                    return false;
                }

                return true;
            }

            private void OnFileChanged(object sender, FileSystemEventArgs eventArgs)
            {
                string trackedFileName = (eventArgs.ChangeType == WatcherChangeTypes.Renamed)
                    ? ((RenamedEventArgs)eventArgs).OldFullPath
                    : eventArgs.FullPath;
                try
                {
                    this.watcherService.OnFileChanged(this, eventArgs, this.filePaths.ContainsKey(trackedFileName));
                }
                catch (AccessViolationException)
                {
                    // We may not be able to actually resolve paths if we don't have access rights
                    Debug.WriteLine("Unauthorized access attempting to resolve path '{0}'", args: new string[] { trackedFileName });
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    this.EnableRaisingEvents = false;
                    this.Changed -= this.OnFileChanged;
                    this.Deleted -= this.OnFileChanged;
                    this.Renamed -= this.OnFileChanged;
                }

                base.Dispose(disposing);
            }
        }
    }
}
