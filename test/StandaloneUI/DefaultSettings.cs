using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandaloneUI
{
    [Export(typeof(ISettings))]
    public class DefaultSettings : ISettings
    {
        private NuGet.Configuration.ISettings Instance { get; set; }

        public DefaultSettings()
        {
            Instance = Settings.LoadDefaultSettings(null, null, null);
        }

        public bool DeleteSection(string section)
        {
            return Instance.DeleteSection(section);
        }

        public bool DeleteValue(string section, string key)
        {
            return Instance.DeleteValue(section, key);
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            return Instance.GetNestedValues(section, subSection);
        }

        public IList<SettingValue> GetSettingValues(string section)
        {
            return Instance.GetSettingValues(section);
        }

        public string GetValue(string section, string key, bool isPath = false)
        {
            return Instance.GetValue(section, key, isPath);
        }

        public string Root
        {
            get { return Instance.Root; }
        }

        public void SetNestedValues(string section, string subSection, IList<KeyValuePair<string, string>> values)
        {
            Instance.SetNestedValues(section, subSection, values);
        }

        public void SetValue(string section, string key, string value)
        {
            Instance.SetValue(section, key, value);
        }

        public void SetValues(string section, IList<KeyValuePair<string, string>> values)
        {
            Instance.SetValues(section, values);
        }
    }
}
