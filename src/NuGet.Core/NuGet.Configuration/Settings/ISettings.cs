// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    /// <summary>
    /// Interface to expose NuGet Settings
    /// </summary>
    public interface ISettings
    {
        //TODO: Delete all obsolete APIs.

        /// <summary>
        /// Gets a value for the given key from the given section
        /// If isPath is true, then the value represents a path. If the path value is already rooted, it is simply
        /// returned
        /// Otherwise, path relative to ISettings.Root is returned
        /// </summary>
        [Obsolete("GetValue(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        string GetValue(string section, string key, bool isPath = false);

        /// <summary>
        /// Gets all subsection element names under section as a List of string.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <returns>List of string containing subsection element names.</returns>
        [Obsolete("GetAllSubsections(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        IReadOnlyList<string> GetAllSubsections(string section);

        /// <summary>
        /// Gets all the values under section
        /// </summary>
        [Obsolete("GetSettingValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        IList<SettingValue> GetSettingValues(string section, bool isPath = false);

        /// <summary>
        /// Gets all the values under section as List of KeyValuePair
        /// </summary>
        [Obsolete("GetNestedValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection);

        /// <summary>
        /// Gets all the values under section as List of SettingValue
        /// </summary>
        [Obsolete("GetNestedSettingValues(...) is deprecated, please use GetSection(...) to interact with the setting values instead.")]
        IReadOnlyList<SettingValue> GetNestedSettingValues(string section, string subSection);

        /// <summary>
        /// Sets the value under the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="key">The key to set set.</param>
        /// <param name="value">The value to set.</param>
        [Obsolete("SetValue(...) is deprecated, please use SetItemInSection(...) to add an item to a section or interact directly with the SettingsElement you want.")]
        void SetValue(string section, string key, string value);

        /// <summary>
        /// Sets the values under the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("SetValues(...) is deprecated, please use SetItemInSection(...) to add an item to a section or interact directly with the SettingsElement you want.")]
        void SetValues(string section, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Updates the <paramref name="values" /> across multiple <see cref="ISettings" /> instances in the hierarchy.
        /// Values are updated in the <see cref="ISettings" /> with the nearest priority.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("UpdateSections(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        void UpdateSections(string section, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Updates nested <paramref name="values" /> across multiple <see cref="ISettings" /> instances in the hierarchy.
        /// Values are updated in the <see cref="ISettings" /> with the nearest priority.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("UpdateSubsections(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        void UpdateSubsections(string section, string subsection, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Sets the values under the specified <paramref name="section" /> and <paramref name="subsection" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("SetNestedValues(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        void SetNestedValues(string section, string subsection, IList<KeyValuePair<string, string>> values);

        /// <summary>
        /// Sets the setting values under the specified <paramref name="section" /> and <paramref name="subsection" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The setting values to set.</param>
        [Obsolete("SetNestedSettingValues(...) is deprecated, please use SetItemInSection(...) to update an item in a section or interact directly with the SettingsElement you want.")]
        void SetNestedSettingValues(string section, string subsection, IList<SettingValue> values);

        /// <summary>
        /// Deletes a key from the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="key">The key to be delted.</param>
        /// <returns>bool indicating success.</returns>
        [Obsolete("DeleteValue(...) is deprecated, please interact directly with the SettingsElement you want to delete.")]
        bool DeleteValue(string section, string key);

        /// <summary>
        /// Deletes the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <returns>bool indicating success.</returns>
        [Obsolete("DeleteSection(...) is deprecated, please interact directly with the SettingsElement you want to delete.")]
        bool DeleteSection(string section);

        /// <summary>
        /// Gets the section with a given name.
        /// </summary>
        /// <param name="sectionName">name to match sections</param>
        /// <returns>null if no section with the given name was found</returns>
        SettingsSection GetSection(string sectionName);

        /// <summary>
        /// Adds the given <paramref name="section" /> to the settings.
        /// </summary>
        /// <param name="section">The non-emtpy section element.</param>
        /// <param name="isBatchOperation">If this operation is part of a batch operation, the caller should be responsible for saving the settings</param>
        /// <returns>false if the given section is empty or already exists in settings</returns>
        bool CreateSection(SettingsSection section, bool isBatchOperation = false);

        /// <summary>
        /// Adds or updates the given <paramref name="item"/> to the settings.
        /// If the <paramref name="item"/> has to be added this method will add it
        /// in the user wide settings file, or walk down the hierarchy (starting from the user wide config)
        /// until it finds a config where the given section is not cleared.
        /// </summary>
        /// <param name="sectionName">section where the <paramref name="item"/> has to be added. If this section does not exist, one will be created.</param>
        /// <param name="item">item to be added to the settings.</param>
        /// <param name="isBatchOperation">If this operation is part of a batch operation, the caller should be responsible for saving the settings</param>
        /// <returns>true if the item was successfully updated or added in the settings</returns>
        bool SetItemInSection(string sectionName, SettingsItem item, bool isBatchOperation = false);

        /// <summary>
        /// Saves any SettingsFile that is dirty.
        /// </summary>
        void Save();

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
