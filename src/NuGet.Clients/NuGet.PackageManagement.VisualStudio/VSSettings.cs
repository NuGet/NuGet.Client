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

        public SettingsSection GetSection(string sectionName)
        {
            return SolutionSettings.GetSection(sectionName);
        }

        public IEnumerable<string> GetConfigFilePaths()
        {
            return SolutionSettings.GetConfigFilePaths();
        }

        public IEnumerable<string> GetConfigRoots()
        {
            return SolutionSettings.GetConfigRoots();
        }

        public bool CreateSection(SettingsSection section, bool isBatchOperation = false)
        {
            return CanChangeSettings && CreateSection(section, isBatchOperation);
        }

        public bool SetItemInSection(string sectionName, SettingsItem item, bool isBatchOperation = false)
        {
            return CanChangeSettings && SolutionSettings.SetItemInSection(sectionName, item, isBatchOperation);
        }

        public void Save()
        {
            if (CanChangeSettings)
            {
                SolutionSettings.Save();
            }
        }

        // The value for SolutionSettings can't possibly be null, but it could be a read-only instance
        private bool CanChangeSettings => !ReferenceEquals(SolutionSettings, NullSettings.Instance);
    }
}
