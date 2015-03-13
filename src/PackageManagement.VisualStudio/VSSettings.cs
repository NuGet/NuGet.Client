using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using NuGet.Configuration;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ISettings))]
    public class VSSettings : ISettings
    {
        private ISettings SolutionSettings { get; set; }
        private ISolutionManager SolutionManager { get; set; }
        private IMachineWideSettings MachineWideSettings { get; set; }

        public VSSettings(ISolutionManager solutionManager)
            : this(solutionManager, machineWideSettings: null)
        {

        }

        [ImportingConstructor]
        public VSSettings(ISolutionManager solutionManager, IMachineWideSettings machineWideSettings)
        {
            if(solutionManager == null)
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
            if(SolutionManager == null || !SolutionManager.IsSolutionOpen)
            {
                root = null;
            }
            else
            {
                root = Path.Combine(SolutionManager.SolutionDirectory, EnvDTEProjectUtility.NuGetSolutionSettingsFolder);
            }
            SolutionSettings = Settings.LoadDefaultSettings(root, configFileName: null, machineWideSettings: MachineWideSettings);
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            ResetSolutionSettings();
        }

        public bool DeleteSection(string section)
        {
            return SolutionSettings.DeleteSection(section);
        }

        public bool DeleteValue(string section, string key)
        {
            return SolutionSettings.DeleteValue(section, key);
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            return SolutionSettings.GetNestedValues(section, subSection);
        }

        public IList<SettingValue> GetSettingValues(string section)
        {
            return SolutionSettings.GetSettingValues(section);
        }

        public string GetValue(string section, string key, bool isPath = false)
        {
            return SolutionSettings.GetValue(section, key, isPath);
        }

        public string Root
        {
            get { return SolutionSettings.Root; }
        }

        public void SetNestedValues(string section, string subSection, IList<KeyValuePair<string, string>> values)
        {
            SolutionSettings.SetNestedValues(section, subSection, values);
        }

        public void SetValue(string section, string key, string value)
        {
            SolutionSettings.SetValue(section, key, value);
        }

        public void SetValues(string section, IList<System.Collections.Generic.KeyValuePair<string, string>> values)
        {
            SolutionSettings.SetValues(section, values);
        }


        public string GetDecryptedValue(string section, string key, bool isPath = false)
        {
            throw new NotImplementedException();
        }

        public void SetEncryptedValue(string section, string key, string value)
        {
            throw new NotImplementedException();
        }
    }
}
