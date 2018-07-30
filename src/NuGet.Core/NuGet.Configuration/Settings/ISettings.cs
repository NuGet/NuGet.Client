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
        SettingsSection GetSection(string sectionName);

        IEnumerable<string> GetConfigFilePaths();

        IEnumerable<string> GetConfigRoots();

        /// <summary>
        /// Adds the given <paramref name="section" /> to the settings.
        /// </summary>
        /// <param name="section">The non-emtpy section element.</param>
        /// <param name="isBatchOperation">If this operation is part of a batch operation, the caller should be responsible for saving the settings</param>
        bool CreateSection(SettingsSection section, bool isBatchOperation = false);

        bool SetItemInSection(string sectionName, SettingsItem item, bool isBatchOperation = false);

        void Save();

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
