extern alias Legacy;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGetVSExtension
{
    /// <summary>
    /// Adapter class to convert NuGet.Configuration.ISettings into legacy ISettings.
    /// </summary>
    internal class SettingsToLegacySettings : Legacy.NuGet.ISettings
    {
        private ISettings _settings;

        public SettingsToLegacySettings(ISettings settings)
        {
            _settings = settings;
        }

        public bool DeleteSection(string section)
        {
            return _settings.DeleteSection(section);
        }

        public bool DeleteValue(string section, string key)
        {
            return _settings.DeleteValue(section, key);
        }

        public IList<Legacy.NuGet.SettingValue> GetNestedValues(string section, string subsection)
        {
            return _settings.GetNestedValues(section, subsection)
                .Select(v => new Legacy.NuGet.SettingValue(v.Key, v.Value, isMachineWide: false))
                .ToList();
        }

        public string GetValue(string section, string key, bool isPath)
        {
            return _settings.GetValue(section, key, isPath);
        }

        public IList<Legacy.NuGet.SettingValue> GetValues(string section, bool isPath)
        {
            return _settings.GetSettingValues(section, isPath)
                .Select(v => new Legacy.NuGet.SettingValue(v.Key, v.Value, v.IsMachineWide, v.Priority))
                .ToList();
        }

        public void SetNestedValues(string section, string key, IList<KeyValuePair<string, string>> values)
        {
            _settings.SetNestedValues(section, key, values);
        }

        public void SetValue(string section, string key, string value)
        {
            _settings.SetValue(section, key, value);
        }

        public void SetValues(string section, IList<KeyValuePair<string, string>> values)
        {
            _settings.SetValues(section,
                values.Select(v => new SettingValue(v.Key, v.Value, isMachineWide: false))
                .ToList());
        }
    }
}
