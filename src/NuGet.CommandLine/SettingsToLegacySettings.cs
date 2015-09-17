using System.Collections.Generic;
using System.Linq;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Adapter class to convert NuGet.Configuration.ISettings into legacy ISettings.
    /// </summary>
    internal class SettingsToLegacySettings : ISettings
    {
        private NuGet.Configuration.ISettings _settings;

        public SettingsToLegacySettings(NuGet.Configuration.ISettings settings)
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

        public IList<SettingValue> GetNestedValues(string section, string subsection)
        {
            return _settings.GetNestedValues(section, subsection)
                .Select(v => new SettingValue(v.Key, v.Value, isMachineWide: false))
                .ToList();
        }

        public string GetValue(string section, string key, bool isPath)
        {
            return _settings.GetValue(section, key, isPath);
        }

        public IList<SettingValue> GetValues(string section, bool isPath)
        {
            return _settings.GetSettingValues(section, isPath)
                .Select(v => new SettingValue(v.Key, v.Value, v.IsMachineWide, v.Priority))
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

        public void SetValues(string section, IList<SettingValue> values)
        {
            _settings.SetValues(section, values.Select(Convert).ToList());
        }

        public void UpdateSections(string section, IList<SettingValue> values)
        {
            _settings.UpdateSections(section, values.Select(Convert).ToList());
        }

        private static NuGet.Configuration.SettingValue Convert(SettingValue settingValue)
        {
            var converted = new NuGet.Configuration.SettingValue(settingValue.Value, settingValue.Key, isMachineWide: false);
            foreach (var additionalData in settingValue.AdditionalData)
            {
                converted.AdditionalData.Add(additionalData);
            }

            return converted;
        }
    }
}