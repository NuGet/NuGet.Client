// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Configuration
{
    internal sealed class SettingsFile : ISettingsFile
    {
        private PhysicalSettingsFile _physicalSettingsFile;
        
        public string ConfigFilePath => _physicalSettingsFile.ConfigFilePath;

        public string DirectoryPath => _physicalSettingsFile.DirectoryPath;

        public string FileName => _physicalSettingsFile.FileName;

        public bool IsDirty
        {
            get
            {
                return _physicalSettingsFile.IsDirty;
            }
            set
            {
                _physicalSettingsFile.IsDirty = value;
            }
        }

        public bool IsMachineWide => _physicalSettingsFile.IsMachineWide;


        /// <summary>
        /// Next config file to read in the hierarchy
        /// </summary>
        internal SettingsFile Next { get; private set; }

        /// <summary>
        /// Order in which the files will be read.
        /// A larger number means closer to the user.
        /// </summary>
        internal int Priority { get; private set; }

        /// <summary>
        /// Creates an instance of a non machine wide SettingsFile with the default filename.
        /// </summary>
        /// <param name="directoryPath">path to the directory where the file is</param>
        public SettingsFile(string directoryPath)
            : this(directoryPath, Settings.DefaultSettingsFileName, isMachineWide: false)
        {
        }

        /// <summary>
        /// Creates an instance of a non machine wide SettingsFile.
        /// </summary>
        /// <param name="directoryPath">path to the directory where the file is</param>
        /// <param name="fileName">name of config file</param>
        public SettingsFile(string directoryPath, string fileName)
            : this(directoryPath, fileName, isMachineWide: false)
        {
        }

        /// <summary>
        /// Creates an instance of a SettingsFile
        /// </summary>
        /// <remarks>It will parse the specified document,
        /// if it doesn't exist it will create one with the default configuration.</remarks>
        /// <param name="directoryPath">path to the directory where the file is</param>
        /// <param name="fileName">name of config file</param>
        /// <param name="isMachineWide">specifies if the SettingsFile is machine wide</param>
        public SettingsFile(string directoryPath, string fileName, bool isMachineWide)
        {
            _physicalSettingsFile = new PhysicalSettingsFile(directoryPath, fileName, isMachineWide);
             Priority = 0;
        }

        public SettingSection GetSection(string sectionName) => _physicalSettingsFile.GetSection(sectionName);

        public void AddOrUpdate(string sectionName, SettingItem item) => _physicalSettingsFile.AddOrUpdate(sectionName, item);

        public void Remove(string sectionName, SettingItem item) => _physicalSettingsFile.Remove(sectionName, item);

        public void SaveToDisk() => _physicalSettingsFile.SaveToDisk();

        public bool IsEmpty() => _physicalSettingsFile.IsEmpty();

        public bool TryGetSection(string sectionName, out SettingSection section) => _physicalSettingsFile.TryGetSection(sectionName, out section);

        public void MergeSectionsInto(Dictionary<string, VirtualSettingSection> sectionsContainer) => _physicalSettingsFile.MergeSectionsInto(sectionsContainer);


        internal static void ConnectSettingsFilesLinkedList(IList<SettingsFile> settingFiles)
        {
            if (settingFiles == null && !settingFiles.Any())
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(settingFiles));
            }

            settingFiles.First().Priority = settingFiles.Count;

            if (settingFiles.Count > 1)
            {
                // if multiple setting files were loaded, chain them in a linked list
                for (var i = 1; i < settingFiles.Count; ++i)
                {
                    settingFiles[i].SetNextFile(settingFiles[i - 1]);
                }
            }
        }

        internal void SetNextFile(SettingsFile settingsFile)
        {
            Next = settingsFile;
            Priority = settingsFile.Priority - 1;
        }


    }
}
