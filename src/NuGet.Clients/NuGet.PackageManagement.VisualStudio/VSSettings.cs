// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio.Utility.FileWatchers;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISettings))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VSSettings : ISettings, IDisposable
    {
        private const string NuGetSolutionSettingsFolder = ".nuget";

        // to initialize SolutionSettings first time outside MEF constructor
        private Tuple<string?, AsyncLazy<ISettings>>? _solutionSettings;

        private ISettings SolutionSettings
        {
            get
            {
                if (_solutionSettings == null)
                {
                    // first time set _solutionSettings via ResetSolutionSettings API call.
                    ResetSolutionSettingsIfNeeded();
                }

                return NuGetUIThreadHelper.JoinableTaskFactory.Run(_solutionSettings!.Item2.GetValueAsync);
            }
        }

        private ISolutionManager SolutionManager { get; }

        private IMachineWideSettings MachineWideSettings { get; }

        private IFileWatcherFactory _fileWatcherFactory;
        private IFileWatcher _userConfigFileWatcher;
        private IFileWatcher? _solutionConfigFileWatcher;

        public event EventHandler? SettingsChanged;

        [ImportingConstructor]
        public VSSettings(ISolutionManager solutionManager, IMachineWideSettings machineWideSettings)
            : this(solutionManager, machineWideSettings, new FileWatcherFactory())
        {
        }

        public VSSettings(ISolutionManager solutionManager, IMachineWideSettings machineWideSettings, IFileWatcherFactory fileWatcherFactory)
        {
            SolutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            MachineWideSettings = machineWideSettings ?? throw new ArgumentNullException(nameof(machineWideSettings));
            SolutionManager.SolutionOpening += OnSolutionOpenedOrClosed;
            SolutionManager.SolutionClosed += OnSolutionOpenedOrClosed;

            _fileWatcherFactory = fileWatcherFactory ?? throw new ArgumentNullException(nameof(fileWatcherFactory));
            _userConfigFileWatcher = _fileWatcherFactory.CreateUserConfigFileWatcher();
            _userConfigFileWatcher.FileChanged += OnConfigFileChanged;
        }

        private bool ResetSolutionSettingsIfNeeded()
        {
            string? root, solutionDirectory;
            if (SolutionManager == null
                || !SolutionManager.IsSolutionOpen
                || string.IsNullOrEmpty(solutionDirectory = SolutionManager.SolutionDirectory))
            {
                root = null;
            }
            else
            {
                root = Path.Combine(solutionDirectory, NuGetSolutionSettingsFolder);
            }

            // This is a performance optimization.
            // The solution load/unload events are called in the UI thread and are used to reset the settings.
            // In some cases there's a synchronous dependency between the invocation of the Solution event and the settings being reset.
            // In the open PM UI scenario (no restore run), there is an asynchronous invocation of this code path. This changes ensures that
            // the synchronous calls that come after the asynchrnous calls don't do duplicate work.
            // That however is not the case for solution close and  same session close -> open events. Those will be on the UI thread.
            if (_solutionSettings == null || !string.Equals(root, _solutionSettings.Item1, Common.PathUtility.GetStringComparisonBasedOnOS()))
            {
                IFileWatcher? oldSolutionConfigFileWatcher = _solutionConfigFileWatcher;
                IFileWatcher? newSolutionConfigFileWatcher = root == null ? null : _fileWatcherFactory.CreateSolutionConfigFileWatcher(root);

                // Just in case multiple threads run this in parallel, use Interlocked.CompareExchange to ensure only
                // one of those threads actually do the work to dispose the old FileWatchers and create new ones.
                // This helps minimize risk that there are multiple FileWatchers watching the same directory which
                // leads to multiple notifications for a single file change in the future.
                IFileWatcher? exchanged = Interlocked.CompareExchange(ref _solutionConfigFileWatcher, newSolutionConfigFileWatcher, oldSolutionConfigFileWatcher);
                if (ReferenceEquals(exchanged, oldSolutionConfigFileWatcher))
                {
                    if (newSolutionConfigFileWatcher != null)
                    {
                        newSolutionConfigFileWatcher.FileChanged += OnConfigFileChanged;
                    }

                    if (oldSolutionConfigFileWatcher != null)
                    {
                        oldSolutionConfigFileWatcher.FileChanged -= OnConfigFileChanged;
                        oldSolutionConfigFileWatcher.Dispose();
                    }
                }
                else
                {
                    newSolutionConfigFileWatcher?.Dispose();
                }

                ResetSolutionSettings(root);
                return true;
            }

            return false;
        }

        private void ResetSolutionSettings(string? solutionDirectory)
        {
            _solutionSettings = new Tuple<string?, AsyncLazy<ISettings>>(
                item1: solutionDirectory,
                item2: new AsyncLazy<ISettings>(async () =>
                {
                    ISettings settings;
                    try
                    {
                        settings = Settings.LoadDefaultSettings(solutionDirectory, configFileName: null, machineWideSettings: MachineWideSettings);
                    }
                    catch (NuGetConfigurationException ex)
                    {
                        settings = NullSettings.Instance;
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        MessageHelper.ShowErrorMessage(Common.ExceptionUtilities.DisplayMessage(ex), Strings.ConfigErrorDialogBoxTitle);
                    }

                    return settings;

                }, NuGetUIThreadHelper.JoinableTaskFactory));
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            var hasChanged = ResetSolutionSettingsIfNeeded();

            if (hasChanged)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnConfigFileChanged(object sender, EventArgs e)
        {
            ResetSolutionSettings(SolutionManager.SolutionDirectory);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public SettingSection GetSection(string sectionName)
        {
            return SolutionSettings.GetSection(sectionName);
        }

        public void AddOrUpdate(string sectionName, SettingItem item)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.AddOrUpdate(sectionName, item);
            }
        }

        public void Remove(string sectionName, SettingItem item)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.Remove(sectionName, item);
            }
        }

        public void SaveToDisk()
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SaveToDisk();
            }
        }

        public IList<string> GetConfigFilePaths() => SolutionSettings.GetConfigFilePaths();

        public IList<string> GetConfigRoots() => SolutionSettings.GetConfigRoots();

        public void Dispose()
        {
            SolutionManager.SolutionOpening -= OnSolutionOpenedOrClosed;
            SolutionManager.SolutionClosed -= OnSolutionOpenedOrClosed;

            _userConfigFileWatcher.FileChanged -= OnConfigFileChanged;
            _userConfigFileWatcher.Dispose();

            if (_solutionConfigFileWatcher != null)
            {
                _solutionConfigFileWatcher.FileChanged -= OnConfigFileChanged;
                _solutionConfigFileWatcher.Dispose();
            }
        }

        // The value for SolutionSettings can't possibly be null, but it could be a read-only instance
        private bool CanChangeSettings => !ReferenceEquals(SolutionSettings, NullSettings.Instance);
    }
}
