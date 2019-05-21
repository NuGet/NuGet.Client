// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Shared;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISettings))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VSSettings : ISettings
    {
        private const string NuGetSolutionSettingsFolder = ".nuget";

        // to initialize SolutionSettings first time outside MEF constructor
        private Tuple<string, ISettings> _solutionSettings;

        private ISettings SolutionSettings
        {
            get
            {
                if (_solutionSettings == null)
                {
                    // first time set _solutionSettings via ResetSolutionSettings API call.
                    ResetSolutionSettingsIfNeeded();
                }

                return _solutionSettings.Item2;
            }
        }

        private ISolutionManager SolutionManager { get; set; }

        private IMachineWideSettings MachineWideSettings { get; set; }

        public event EventHandler SettingsChanged;

        public VSSettings(ISolutionManager solutionManager)
            : this(solutionManager, machineWideSettings: null)
        {
        }

        [ImportingConstructor]
        public VSSettings(ISolutionManager solutionManager, IMachineWideSettings machineWideSettings)
        {
            SolutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            MachineWideSettings = machineWideSettings;
            SolutionManager.SolutionOpening += OnSolutionOpenedOrClosed;
            SolutionManager.SolutionClosed += OnSolutionOpenedOrClosed;
        }

        private bool ResetSolutionSettingsIfNeeded()
        {
            string root;
            if (SolutionManager == null
                || !SolutionManager.IsSolutionOpen
                || string.IsNullOrEmpty(SolutionManager.SolutionDirectory))
            {
                root = null;
            }
            else
            {
                root = Path.Combine(SolutionManager.SolutionDirectory, NuGetSolutionSettingsFolder);
            }

            try
            {
                // This is a performance optimization.
                // The solution load/unload events are called in the UI thread and are used to reset the settings.
                // In some cases there's a synchronous dependency between the invocation of the Solution event and the settings being reset.
                // In the open PM UI scenario (no restore run), there is an asynchronous invocation of this code path. This changes ensures that
                // the synchronus calls that come after the asynchrnous calls don't do duplicate work.
                if (!EqualityUtility.EqualsWithNullCheck(root, _solutionSettings?.Item1))
                {
                    _solutionSettings = new Tuple<string, ISettings>(
                        item1: root,
                        item2: Settings.LoadDefaultSettings(root, configFileName: null, machineWideSettings: MachineWideSettings)
                        );
                    return true;
                }
            }
            catch (NuGetConfigurationException ex)
            {
                MessageHelper.ShowErrorMessage(ExceptionUtilities.DisplayMessage(ex), Strings.ConfigErrorDialogBoxTitle);
            }

            if (_solutionSettings == null)
            {
                _solutionSettings = new Tuple<string, ISettings>(null, NullSettings.Instance);
                return true;
            }

            return false;
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e) // Should we refresh asynchronously
        {
            var changed = ResetSolutionSettingsIfNeeded();

            // raises event SettingsChanged
            if (changed)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
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

        // The value for SolutionSettings can't possibly be null, but it could be a read-only instance
        private bool CanChangeSettings => !ReferenceEquals(SolutionSettings, NullSettings.Instance);
    }
}
