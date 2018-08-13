// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;
using static NuGet.Configuration.Settings;

namespace NuGet.Configuration
{
    internal class SettingsFile : ISettingsFile
    {
        /// <summary>
        /// Name of the root element of settings file
        /// </summary>
        private static string _rootElementName => ConfigurationConstants.Configuration;

        /// <summary>
        /// XML element for settings file
        /// </summary>
        private XDocument _xDocument { get; }

        /// <summary>
        /// Root element of configuration file. By definition of a nuget.config, the root element has to be a `configuration` element
        /// </summary>
        private NuGetConfiguration RootElement { get; set; }

        /// <summary>
        /// Next config file to read in the hierarchy
        /// </summary>
        internal SettingsFile Next { get; private set; }

        /// <summary>
        /// Folder under which the settings file is present
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// Filename of the settings file
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Full path to the settings file
        /// </summary>
        public string ConfigFilePath => Path.GetFullPath(Path.Combine(Root, FileName));

        /// <summary>
        /// Defines if the settings file is considered a machine wide settings file
        /// </summary>
        /// <remarks>Machine wide settings files cannot be eddited.</remarks>
        public bool IsMachineWide { get; }

        /// <summary>
        /// Defines if the configuration settings have been changed but have not been saved to disk
        /// </summary>
        public bool IsDirty { get; set; }
        
        /// <summary>
        /// Event handler to be called when this setting file has changed.
        /// </summary>
        public event EventHandler SettingsChanged = delegate { };

        public SettingsFile(string root)
            : this(root, DefaultSettingsFileName, false)
        {
        }

        internal SettingsFile(string root, string fileName)
            : this(root, fileName, false)
        {
        }

        public SettingsFile(string root, string fileName, bool isMachineWide)
        {
            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentNullException(nameof(root), Resources.Argument_Cannot_Be_Null_Or_Empty);
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName), Resources.Argument_Cannot_Be_Null_Or_Empty);
            }

            if (!FileSystemUtility.IsPathAFile(fileName))
            {
                throw new ArgumentException(Resources.Settings_FileName_Cannot_Be_A_Path, nameof(fileName));
            }

            Root = root;
            FileName = fileName;
            IsMachineWide = isMachineWide;

            XDocument config = null;
            var self = this;
            ExecuteSynchronized(() =>
            {
                config = XmlUtility.GetOrCreateDocument(self.CreateDefaultConfig(), ConfigFilePath);
            });

            _xDocument = config;

            ParseNuGetConfiguration();
        }

        public bool IsEmpty() => RootElement == null || RootElement.IsEmpty();

        internal static void ConnectSettingsFilesLinkedList(IList<SettingsFile> settingFiles)
        {
            // if multiple setting files were loaded, chain them in a linked list
            for (var i = 1; i < settingFiles.Count; ++i)
            {
                settingFiles[i].Next = settingFiles[i - 1];
            }
        }

        internal void MergeSectionsInto(Dictionary<string, SettingsSection> sectionsContainer)
        {
            RootElement.MergeSectionsInto(sectionsContainer);
        }

        public SettingsSection GetSection(string sectionName)
        {
            return RootElement.GetSection(sectionName);
        }

        public bool CreateSection(SettingsSection section, bool isBatchOperation = false)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            if (section.IsEmpty() || GetSection(section.Name) != null)
            {
                return false;
            }

            return RootElement.AddChild(section, isBatchOperation);
        }

        public bool SetItemInSection(string sectionName, SettingsItem item, bool isBatchOperation = false)
        {
            if (string.IsNullOrEmpty(sectionName))
            {
                throw new ArgumentNullException(nameof(sectionName));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            // Check if set is an update
            var section = GetSection(sectionName);
            if (section != null)
            {
                if (section.TryUpdateChildItem(item, isBatchOperation))
                {
                    return true;
                }
            }
            
            // Set is an add
            return RootElement.AddItemInSection(sectionName, item.Copy(), isBatchOperation);
        }

        public void Save()
        {
            if (IsDirty)
            {
                ExecuteSynchronized(() =>
                {
                    FileSystemUtility.AddFile(ConfigFilePath, _xDocument.Save);
                });

                IsDirty = false;
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ParseNuGetConfiguration()
        {
            if (_xDocument.Root.Name != _rootElementName)
            {
                throw new NuGetConfigurationException(
                         string.Format(Resources.ShowError_ConfigRootInvalid, ConfigFilePath));
            }

            RootElement = new NuGetConfiguration(_xDocument.Root, origin: this);
        }

        private XDocument CreateDefaultConfig()
        {
            var configurationElement = new NuGetConfiguration(this);
            return new XDocument(configurationElement.AsXNode());
        }

        private void ExecuteSynchronized(Action ioOperation)
        {
            ConcurrencyUtilities.ExecuteWithFileLocked(filePath: ConfigFilePath, action: () =>
            {
                try
                {
                    ioOperation();
                }
                catch (InvalidOperationException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_ConfigInvalidOperation, ConfigFilePath, e.Message), e);
                }

                catch (UnauthorizedAccessException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_ConfigUnauthorizedAccess, ConfigFilePath, e.Message), e);
                }

                catch (XmlException e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.ShowError_ConfigInvalidXml, ConfigFilePath, e.Message), e);
                }

                catch (Exception e)
                {
                    throw new NuGetConfigurationException(
                        string.Format(CultureInfo.CurrentCulture, Resources.Unknown_Config_Exception, ConfigFilePath, e.Message), e);
                }
            });
        }
    }
}
