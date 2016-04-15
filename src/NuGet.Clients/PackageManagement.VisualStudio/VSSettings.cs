// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(Configuration.ISettings))]
    public class VSSettings : Configuration.ISettings
    {
        private Configuration.ISettings SolutionSettings { get; set; }
        private ISolutionManager SolutionManager { get; set; }
        private Configuration.IMachineWideSettings MachineWideSettings { get; set; }

        public event EventHandler SettingsChanged;

        public VSSettings(ISolutionManager solutionManager)
            : this(solutionManager, machineWideSettings: null)
        {
        }

        [ImportingConstructor]
        public VSSettings(ISolutionManager solutionManager, Configuration.IMachineWideSettings machineWideSettings)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException("solutionManager");
            }

            SolutionManager = solutionManager;
            MachineWideSettings = machineWideSettings;
            ResetSolutionSettings();
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
                root = Path.Combine(SolutionManager.SolutionDirectory, EnvDTEProjectUtility.NuGetSolutionSettingsFolder);
            }

            try
            {
                SolutionSettings = Configuration.Settings.LoadDefaultSettings(root, configFileName: null, machineWideSettings: MachineWideSettings);
            }
            catch (Configuration.NuGetConfigurationException ex)
            {
                MessageHelper.ShowErrorMessage(ExceptionUtilities.DisplayMessage(ex), Strings.ConfigErrorDialogBoxTitle);
            }

            if (SolutionSettings == null)
            {
                SolutionSettings = Configuration.NullSettings.Instance;
            }
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            ResetSolutionSettings();

            // raises event SettingsChanged
            if (SettingsChanged != null)
            {
                SettingsChanged(this, EventArgs.Empty);
            }
        }

        public bool DeleteSection(string section)
        {
            return CanChangeSettings && SolutionSettings.DeleteSection(section);
        }

        public bool DeleteValue(string section, string key)
        {
            return CanChangeSettings && SolutionSettings.DeleteValue(section, key);
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            return SolutionSettings.GetNestedValues(section, subSection);
        }

        public IList<Configuration.SettingValue> GetSettingValues(string section, bool isPath = false)
        {
            return SolutionSettings.GetSettingValues(section, isPath);
        }

        public string GetValue(string section, string key, bool isPath = false)
        {
            return SolutionSettings.GetValue(section, key, isPath);
        }

        public string Root
        {
            get { return SolutionSettings.Root; }
        }

        public string FileName
        {
            get { return SolutionSettings.FileName; }
        }

        public IEnumerable<Configuration.ISettings> Priority
        {
            get { return SolutionSettings.Priority; }
        }

        public void SetNestedValues(string section, string subSection, IList<KeyValuePair<string, string>> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetNestedValues(section, subSection, values);
            }
        }

        public void SetValue(string section, string key, string value)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetValue(section, key, value);
            }
        }

        public void SetValues(string section, IReadOnlyList<Configuration.SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetValues(section, values);
            }
        }

        public void UpdateSections(string section, IReadOnlyList<Configuration.SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.UpdateSections(section, values);
            }
        }

        private bool CanChangeSettings
        {
            get
            {
                // The value for SolutionSettings can't possibly be null, but it could be a read-only instance
                return !object.ReferenceEquals(SolutionSettings, Configuration.NullSettings.Instance);
            }
        }
    }
}
