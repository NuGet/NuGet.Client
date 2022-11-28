// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio.Utility.FileWatchers
{
    internal sealed class SolutionConfigFileWatcher : IFileWatcher
    {
        private bool _disposed;
        private FileSystemWatcher _solutionWatcher;
        private FileSystemWatcher? _dotNuGetWatcher;

        public SolutionConfigFileWatcher(string solutionDirectory)
        {
            string? current = solutionDirectory.EndsWith("\\", StringComparison.Ordinal)
                ? Path.GetDirectoryName(solutionDirectory)
                : solutionDirectory;

            _solutionWatcher = Create(solutionDirectory);

            var dotNuGetPath = Path.Combine(solutionDirectory, NuGetConstants.NuGetSolutionSettingsFolder);
            if (Directory.Exists(dotNuGetPath))
            {
                _dotNuGetWatcher = Create(dotNuGetPath);
            }

            FileSystemWatcher Create(string path)
            {
                var watcher = new FileSystemWatcher(path, Settings.DefaultSettingsFileName);
                watcher.Created += OnFileSystemEvent;
                watcher.Changed += OnFileSystemEvent;
                watcher.Deleted += OnFileSystemEvent;
                watcher.Renamed += OnFileSystemEvent;
                watcher.EnableRaisingEvents = true;

                return watcher;
            }
        }

        public event EventHandler? FileChanged;

        public void Dispose()
        {
            if (_disposed) { return; }

            _disposed = true;

            _solutionWatcher.Dispose();
            _dotNuGetWatcher?.Dispose();

            GC.SuppressFinalize(this);
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            FileChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
