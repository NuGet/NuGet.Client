using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    /// <summary>
    /// Interface to expose NuGet Settings
    /// </summary>
    public interface ISettings
    {
        /// <summary>
        /// Folder under which the config file is present
        /// </summary>
        string Root { get; }
        /// <summary>
        /// Gets a value for the given key from the given section 
        /// If isPath is true, then the value represents a path. If the path value is already rooted, it is simply returned
        /// Otherwise, path relative to ISettings.Root is returned
        /// </summary>
        string GetValue(string section, string key, bool isPath = false);
        /// <summary>
        /// Gets all the values under section
        /// </summary>
        IList<SettingValue> GetSettingValues(string section, bool isPath = false);
        /// <summary>
        /// Gets all the values under section
        /// </summary>
        IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection);
        void SetValue(string section, string key, string value);

        void SetValues(string section, IList<KeyValuePair<string, string>> values);
        void SetNestedValues(string section, string subSection, IList<KeyValuePair<string, string>> values);
        bool DeleteValue(string section, string key);
        bool DeleteSection(string section);

    }
}
