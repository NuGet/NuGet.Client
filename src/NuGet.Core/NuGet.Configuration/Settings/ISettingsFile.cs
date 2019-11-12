// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Configuration
{
    internal interface ISettingsFile
    {
        /// <summary>
        /// Full path to the settings file
        /// </summary>
        string ConfigFilePath { get; }

        /// <summary>
        /// Folder under which the settings file is present
        /// </summary>
        string DirectoryPath { get; }

        /// <summary>
        /// Filename of the settings file
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Defines if the configuration settings have been changed but have not been saved to disk
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// Defines if the settings file is considered a machine wide settings file
        /// </summary>
        /// <remarks>Machine wide settings files cannot be eddited.</remarks>
        bool IsMachineWide { get; }

        SettingSection GetSection(string sectionName);

        /// <summary>
        /// Adds or updates the given <paramref name="item"/> to the settings.
        /// </summary>
        /// <param name="sectionName">section where the <paramref name="item"/> has to be added. If this section does not exist, one will be created.</param>
        /// <param name="item">item to be added to the settings.</param>
        /// <returns>true if the item was successfully updated or added in the settings</returns>
        void AddOrUpdate(string sectionName, SettingItem item);

        /// <summary>
        /// Removes the given <paramref name="item"/> from the settings.
        /// If the <paramref name="item"/> is the last item in the section, the section will also be removed.
        /// </summary>
        /// <param name="sectionName">Section where the <paramref name="item"/> is stored. If this section does not exist, the method will throw</param>
        /// <param name="item">item to be removed from the settings</param>
        /// <remarks> If the SettingsFile is a machine wide config this method will throw</remarks>
        void Remove(string sectionName, SettingItem item);

        /// <summary>
        /// Flushes any in-memory updates in the SettingsFile to disk.
        /// </summary>
        void SaveToDisk();

        bool IsEmpty();

        /// <remarks>
        /// This method gives you a reference to the actual abstraction instead of a clone of it.
        /// It should be used only when intended. For most purposes you should be able to use
        /// GetSection(...) instead.
        /// </remarks>
        bool TryGetSection(string sectionName, out SettingSection section);

        void MergeSectionsInto(Dictionary<string, VirtualSettingSection> sectionsContainer);
    }
}
