using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    public interface ISettings
    {
        string GetValue(string section, string key);
        string GetValue(string section, string key, bool isPath);
        IList<SettingValue> GetSettingValues(string section, bool isPath);
        IList<KeyValuePair<string, string>> GetNestedValues(string section, string key);
        void SetValue(string section, string key, string value);
        void SetValues(string section, IList<KeyValuePair<string, string>> values);
        void SetNestedValues(string section, string key, IList<KeyValuePair<string, string>> values);
        bool DeleteValue(string section, string key);
        bool DeleteSection(string section);
    }
}
