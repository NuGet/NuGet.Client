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
        /// <summary>
        /// Folder under which the config file is present
        /// </summary>
        string Root { get; }

        /// <summary>
        /// The file name of the config file. Joining <see cref="Root"/> and
        /// <see cref="FileName"/> results in the full path to the config file.
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Enumerates the sequence of <see cref="ISettings"/> instances used to fetch settings
        /// values (e.g. with <see cref="GetValue"/>). This enumeration includes this instance
        /// itself.
        /// </summary>
        IEnumerable<ISettings> Priority { get; }

        /// <summary>
        /// Gets a value for the given key from the given section
        /// If isPath is true, then the value represents a path. If the path value is already rooted, it is simply
        /// returned
        /// Otherwise, path relative to ISettings.Root is returned
        /// </summary>
        string GetValue(string section, string key, bool isPath = false);

        /// <summary>
        /// Gets all subsection element names under section as a List of string.
        /// </summary>
        /// <param name="section">Name of the section.</param>
        /// <returns>List of string containing subsection element names.</returns>
        IReadOnlyList<string> GetAllSubsections(string section);

        /// <summary>
        /// Gets all the values under section
        /// </summary>
        IList<SettingValue> GetSettingValues(string section, bool isPath = false);

        /// <summary>
        /// Gets all the values under section as List of KeyValuePair
        /// </summary>
        IList<KeyValuePair<string, string>> GetNestedValues(string section, string subSection);

        /// <summary>
        /// Gets all the values under section as List of SettingValue
        /// </summary>
        IReadOnlyList<SettingValue> GetNestedSettingValues(string section, string subSection);

        /// <summary>
        /// Sets the value under the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="key">The key to set set.</param>
        /// <param name="value">The value to set.</param>
        void SetValue(string section, string key, string value);

        /// <summary>
        /// Sets the values under the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="values">The values to set.</param>
        void SetValues(string section, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Updates the <paramref name="values" /> across multiple <see cref="ISettings" /> instances in the hierarchy.
        /// Values are updated in the <see cref="ISettings" /> with the nearest priority.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="values">The values to set.</param>
        void UpdateSections(string section, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Updates nested <paramref name="values" /> across multiple <see cref="ISettings" /> instances in the hierarchy.
        /// Values are updated in the <see cref="ISettings" /> with the nearest priority.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The values to set.</param>
        void UpdateSubsections(string section, string subsection, IReadOnlyList<SettingValue> values);

        /// <summary>
        /// Sets the values under the specified <paramref name="section" /> and <paramref name="subsection" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The values to set.</param>
        void SetNestedValues(string section, string subsection, IList<KeyValuePair<string, string>> values);

        /// <summary>
        /// Sets the setting values under the specified <paramref name="section" /> and <paramref name="subsection" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="subsection">The name of the subsection.</param>
        /// <param name="values">The setting values to set.</param>
        void SetNestedSettingValues(string section, string subsection, IList<SettingValue> values);

        /// <summary>
        /// Deletes a key from the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <param name="key">The key to be delted.</param>
        /// <returns>bool indicating success.</returns>
        bool DeleteValue(string section, string key);

        /// <summary>
        /// Deletes the specified <paramref name="section" />.
        /// </summary>
        /// <param name="section">The name of the section.</param>
        /// <returns>bool indicating success.</returns>
        bool DeleteSection(string section);

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
