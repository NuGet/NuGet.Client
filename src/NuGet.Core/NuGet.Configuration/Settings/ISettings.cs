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
        IList<SettingValue> GetNestedSettingValues(string section, string subSection);

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

        void SetNestedValues(string section, string subSection, IList<KeyValuePair<string, string>> values);

        bool DeleteValue(string section, string key);

        bool DeleteSection(string section);

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
