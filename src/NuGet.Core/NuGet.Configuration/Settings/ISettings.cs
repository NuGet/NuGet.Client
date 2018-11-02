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
        //TODO: Delete all obsolete APIs. https://github.com/NuGet/Home/issues/7294

        /// <summary>
        /// Gets a value for the given key from the given section
        /// If isPath is true, then the value represents a path. If the path value is already rooted, it is simply
        /// returned
        /// Otherwise, path relative to ISettings.Root is returned
        /// </summary>
        [Obsolete("GetValue(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        string GetValue(string section, string key, bool isPath = false);

        /// <summary>
        /// Gets all subsection element names under section as a List of string.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <returns>List of string containing subsection element names.</returns>
        [Obsolete("GetAllSubsections(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        IReadOnlyList<string> GetAllSubsections(string section);

        /// <summary>
        /// Gets all the values under section
        /// </summary>
        [Obsolete("GetSettingValues(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        IList<SettingValue> GetSettingValues(string section, bool isPath = false);

        /// <summary>
        /// Gets all the values under section as List of KeyValuePair
        /// </summary>
        [Obsolete("GetNestedValues(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection);

        /// <summary>
        /// Gets all the values under section as List of SettingValue
        /// </summary>
        [Obsolete("GetNestedSettingValues(...) is deprecated. Please use GetSection(...) to interact with the setting values instead.")]
        IReadOnlyList<SettingValue> GetNestedSettingValues(string section, string subSection);

        /// <summary>
        /// Sets the value under the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="key">The key to set set.</param>
        /// <param name="value">The value to set.</param>
        [Obsolete("SetValue(...) is deprecated. Please use AddOrUpdate(...) to add an item to a section or interact directly with the SettingItem you want.")]
        void SetValue(string section, string key, string value);

        /// <summary>
        /// Sets the values under the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("SetValues(...) is deprecated. Please use AddOrUpdate(...) to add an item to a section or interact directly with the SettingItem you want.")]
        void SetValues(string section, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Updates the <paramref name="values" /> across multiple <see cref="ISettings" /> instances in the hierarchy.
        /// Values are updated in the <see cref="ISettings" /> with the nearest priority.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("UpdateSections(...) is deprecated. Please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        void UpdateSections(string section, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Updates nested <paramref name="values" /> across multiple <see cref="ISettings" /> instances in the hierarchy.
        /// Values are updated in the <see cref="ISettings" /> with the nearest priority.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("UpdateSubsections(...) is deprecated. Please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        void UpdateSubsections(string section, string subsection, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Sets the values under the specified <paramref name="section" /> and <paramref name="subsection" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The values to set.</param>
        [Obsolete("SetNestedValues(...) is deprecated. Please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        void SetNestedValues(string section, string subsection, IList<KeyValuePair<string, string>> values);

        /// <summary>
        /// Sets the setting values under the specified <paramref name="section" /> and <paramref name="subsection" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The setting values to set.</param>
        [Obsolete("SetNestedSettingValues(...) is deprecated. Please use AddOrUpdate(...) to update an item in a section or interact directly with the SettingItem you want.")]
        void SetNestedSettingValues(string section, string subsection, IList<SettingValue> values);

        /// <summary>
        /// Deletes a key from the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="key">The key to be delted.</param>
        /// <returns>bool indicating success.</returns>
        [Obsolete("DeleteValue(...) is deprecated. Please use Remove(...) with the item you want to remove from the setttings.")]
        bool DeleteValue(string section, string key);

        /// <summary>
        /// Deletes the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <returns>bool indicating success.</returns>
        [Obsolete("DeleteSection(...) is deprecated,. Please use Remove(...) with all the items in the section you want to remove from the setttings.")]
        bool DeleteSection(string section);

        /// <summary>
        /// Gets the section with a given name.
        /// </summary>
        /// <param name="sectionName">name to match sections</param>
        /// <returns>null if no section with the given name was found</returns>
        SettingSection GetSection(string sectionName);

        /// <summary>
        /// Adds or updates the given <paramref name="item"/> to the settings.
        /// If the <paramref name="item"/> has to be added this method will add it
        /// in the user wide settings file, or walk down the hierarchy (starting from the user wide config)
        /// until it finds a config where the given section is not cleared.
        /// </summary>
        /// <param name="sectionName">section where the <paramref name="item"/> has to be added. If this section does not exist, one will be created.</param>
        /// <param name="item">item to be added to the settings.</param>
        /// <returns>true if the item was successfully updated or added in the settings</returns>
        void AddOrUpdate(string sectionName, SettingItem item);

        /// <summary>
        /// Removes the given <paramref name="item"/> from the settings.
        /// If the <paramref name="item"/> is the last item in the section, the section will also be removed.
        /// If the item is overriding any items from other configs it will delete all the merged items that are
        /// not in a machine wide config.
        /// </summary>
        /// <param name="sectionName">Section where the <paramref name="item"/> is stored. If this section does not exist, the method will throw</param>
        /// <param name="item">item to be removed from the settings</param>
        /// <remarks> If the <paramref name="item"/> is in a machine wide config this method will throw</remarks>
        void Remove(string sectionName, SettingItem item);

        /// <summary>
        /// Flushes any update that has been done in memory through the ISettings API to the settings file in disk.
        /// </summary>
        void SaveToDisk();

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;

        /// <summary>
        /// Get a list of all the paths of the settings files used as part of this settings object
        /// </summary>
        IList<string> GetConfigFilePaths();

        /// <summary>
        /// Get a list of all the roots of the settings files used as part of this settings object
        /// </summary>
        IList<string> GetConfigRoots();
    }
}
