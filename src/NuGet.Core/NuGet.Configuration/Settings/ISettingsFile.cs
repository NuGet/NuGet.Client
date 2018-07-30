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
        /// Saves the document to disk
        /// </summary>
        void Save();

        /// <summary>
        /// Root element for the settings abstraction
        /// </summary>
        NuGetConfiguration RootElement { get; }

        bool IsDirty { get; set; }

        /// <summary>
        /// Event raised when the setting have been changed.
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
