// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Configuration
{
    /// <summary>
    /// Interface to expose a NuGet settings file
    /// </summary>
    internal interface ISettingsFile
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
        /// Full path to the settings file
        /// </summary>
        string ConfigFilePath { get; }

        /// <summary>
        /// Defines if the settings file is considered a machine wide settings file
        /// </summary>
        /// <remarks>Machine wide settings files cannot be eddited.</remarks>
        bool IsMachineWide { get; }

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
        /// </summary>
        /// <param name="sectionName">section where the <paramref name="item"/> has to be added. If this section does not exist, one will be created.</param>
        /// <param name="item">item to be added to the settings.</param>
        /// <param name="isBatchOperation">If this operation is part of a batch operation, the caller should be responsible for saving the settings</param>
        /// <returns>true if the item was successfully updated or added in the settings</returns>
        bool SetItemInSection(string sectionName, SettingsItem item, bool isBatchOperation = false);

        /// <summary>
        /// Describes if the current abstraction for the ISettingsFile has been
        /// modified but not saved to disk.
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// Saves the document to disk
        /// </summary>
        /// <remarks>This method will only save the document if it has been modified.</remarks>
        void Save();

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
