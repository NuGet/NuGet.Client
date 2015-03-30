using System;
using System.Collections.Generic;
using System.Globalization;

namespace NuGet.Configuration
{
    public class NullSettings : ISettings
    {
        private static readonly NullSettings _settings = new NullSettings();

        public static NullSettings Instance
        {
            get { return _settings; }
        }

        public string Root
        {
            get { return String.Empty; }
        }

        public string GetValue(string section, string key, bool isPath = false)
        {
            return String.Empty;
        }

        public IList<SettingValue> GetSettingValues(string section, bool isPath)
        {
            return new List<SettingValue>().AsReadOnly();
        }

        public IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection)
        {
            return new List<KeyValuePair<string, string>>().AsReadOnly();
        }

        public void SetValue(string section, string key, string value)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, "SetValue"));
        }

        public void SetValues(string section, IList<KeyValuePair<string, string>> values)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, "SetValues"));
        }

        public void SetNestedValues(string section, string key, IList<KeyValuePair<string, string>> values)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, "SetNestedValues"));
        }

        public bool DeleteValue(string section, string key)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, "DeleteValue"));
        }

        public bool DeleteSection(string section)
        {
            throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.InvalidNullSettingsOperation, "DeleteSection"));
        }
    }

}
