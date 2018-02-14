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

        public IReadOnlyList<SettingValue> GetNestedSettingValues(string section, string subSection)
        {
            return SolutionSettings.GetNestedSettingValues(section, subSection);
        }

        public IList<SettingValue> GetSettingValues(string section, bool isPath = false)
        {
            return SolutionSettings.GetSettingValues(section, isPath);
        }

        public string GetValue(string section, string key, bool isPath = false)
        {
            return SolutionSettings.GetValue(section, key, isPath);
        }

        public IReadOnlyList<string> GetAllSubsections(string section)
        {
            return SolutionSettings.GetAllSubsections(section);
        }

        public string Root => SolutionSettings.Root;

        public string FileName => SolutionSettings.FileName;

        public IEnumerable<ISettings> Priority => SolutionSettings.Priority;

        public void SetNestedValues(string section, string subsection, IList<KeyValuePair<string, string>> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetNestedValues(section, subsection, values);
            }
        }

        public void SetNestedSettingValues(string section, string subsection, IList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetNestedSettingValues(section, subsection, values);
            }
        }

        public void SetValue(string section, string key, string value)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetValue(section, key, value);
            }
        }

        public void SetValues(string section, IReadOnlyList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.SetValues(section, values);
            }
        }

        public void UpdateSections(string section, IReadOnlyList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.UpdateSections(section, values);
            }
        }

        public void UpdateSubsections(string section, string subsection, IReadOnlyList<SettingValue> values)
        {
            if (CanChangeSettings)
            {
                SolutionSettings.UpdateSubsections(section, subsection, values);
            }
        }

        // The value for SolutionSettings can't possibly be null, but it could be a read-only instance
        private bool CanChangeSettings => !ReferenceEquals(SolutionSettings, NullSettings.Instance);
    }
}
