// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISettings))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VSSettings : ISettings
    {
        private const string NuGetSolutionSettingsFolder = ".nuget";

        // to initialize SolutionSettings first time outside MEF constructor
        private ISettings _solutionSettings;

        private ISettings SolutionSettings
        {
            get
            {
                if (_solutionSettings == null)
                {
                    // first time set _solutionSettings via ResetSolutionSettings API call.
                    ResetSolutionSettings();
                }

                return _solutionSettings;
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

        private void ResetSolutionSettings()
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
                _solutionSettings = Settings.LoadDefaultSettings(root, configFileName: null, machineWideSettings: MachineWideSettings);
            }
            catch (NuGetConfigurationException ex)
            {
                MessageHelper.ShowErrorMessage(ExceptionUtilities.DisplayMessage(ex), Strings.ConfigErrorDialogBoxTitle);
            }

            if (_solutionSettings == null)
            {
                _solutionSettings = NullSettings.Instance;
            }
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            ResetSolutionSettings();

            // raises event SettingsChanged
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

        // The value for SolutionSettings can't possibly be null, but it could be a read-only instance
        private bool CanChangeSettings => !ReferenceEquals(SolutionSettings, NullSettings.Instance);

        //TODO: Remove deprecated methods https://github.com/NuGet/Home/issues/7294

#pragma warning disable CS0618 // Type or member is obsolete

        [Obsolete("GetValue(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        public string GetValue(string section, string key, bool isPath = false) => SolutionSettings.GetValue(section, key, isPath);

        [Obsolete("GetAllSubsections(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        public IReadOnlyList<string> GetAllSubsections(string section) => SolutionSettings.GetAllSubsections(section);

        [Obsolete("GetSettingValues(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        public IList<SettingValue> GetSettingValues(string section, bool isPath = false) => SolutionSettings.GetSettingValues(section, isPath);

        [Obsolete("GetNestedValues(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection) => SolutionSettings.GetNestedValues(section, subSection);

        [Obsolete("GetNestedSettingValues(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        public IReadOnlyList<SettingValue> GetNestedSettingValues(string section, string subSection) => SolutionSettings.GetNestedSettingValues(section, subSection);

        [Obsolete("SetValue(...) is deprecated. Please use SetItemInSection(...) to add an item to a section or interact directly with the SettingsElement you want.")]
        public void SetValue(string section, string key, string value)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetValue(section, key, value);
            }
        }

        [Obsolete("SetValues(...) is deprecated. Please use SetItemInSection(...) to add an item to a section or interact directly with the SettingsElement you want.")]
        public void SetValues(string section, IReadOnlyList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetValues(section, values);
            }
        }

        [Obsolete("UpdateSections(...) is deprecated. Please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void UpdateSections(string section, IReadOnlyList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.UpdateSections(section, values);
            }
        }

        [Obsolete("UpdateSubsections(...) is deprecated. Please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void UpdateSubsections(string section, string subsection, IReadOnlyList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.UpdateSubsections(section, subsection, values);
            }
        }

        [Obsolete("SetNestedValues(...) is deprecated. Please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void SetNestedValues(string section, string subsection, IList<KeyValuePair<string, string>> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetNestedValues(section, subsection, values);
            }
        }

        [Obsolete("SetNestedSettingValues(...) is deprecated. Please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        public void SetNestedSettingValues(string section, string subsection, IList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetNestedSettingValues(section, subsection, values);
            }
        }

        [Obsolete("DeleteValue(...) is deprecated. Please interact directly with the SettingsElement you want to delete.")]
        public bool DeleteValue(string section, string key) => CanChangeSettings && SolutionSettings.DeleteValue(section, key);

        [Obsolete("DeleteSection(...) is deprecated. Please interact directly with the SettingsElement you want to delete.")]
        public bool DeleteSection(string section) => CanChangeSettings && SolutionSettings.DeleteSection(section);

#pragma warning restore CS0618 // Type or member is obsolete

    }
}
