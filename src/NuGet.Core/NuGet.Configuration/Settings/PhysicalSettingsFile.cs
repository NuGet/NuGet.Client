// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;

namespace NuGet.Configuration
{
    internal sealed class PhysicalSettingsFile : ISettingsFile
    {
        public string ConfigFilePath { get; }

        public string DirectoryPath { get; }

        public string FileName { get; }

        public bool IsDirty { get; set; }

        public bool IsMachineWide { get; }

        /// <summary>
        /// XML element for settings file
        /// </summary>
        private readonly XDocument _xDocument;

        /// <summary>
        /// Root element of configuration file.
        /// By definition of a nuget.config, the root element has to be a 'configuration' element
        /// </summary>
        private readonly NuGetConfiguration _rootElement;

        /// <summary>
        /// Creates an instance of a PhysicalSettingsFile
        /// </summary>
        /// <remarks>It will parse the specified document,
        /// if it doesn't exist it will create one with the default configuration.</remarks>
        /// <param name="directoryPath">path to the directory where the file is</param>
        /// <param name="fileName">name of config file</param>
        /// <param name="isMachineWide">specifies if the SettingsFile is machine wide</param>
        public PhysicalSettingsFile(string directoryPath, string fileName, bool isMachineWide)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(directoryPath));
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(fileName));
            }

            if (!FileSystemUtility.IsPathAFile(fileName))
            {
                throw new ArgumentException(Resources.Settings_FileName_Cannot_Be_A_Path, nameof(fileName));
            }

            DirectoryPath = directoryPath;
            FileName = fileName;
            IsMachineWide = isMachineWide;
            ConfigFilePath = Path.GetFullPath(Path.Combine(DirectoryPath, FileName));

            XDocument config = null;
            ExecuteSynchronized(() =>
            {
                config = XmlUtility.GetOrCreateDocument(CreateDefaultConfig(), ConfigFilePath);
            });

            _xDocument = config;

            _rootElement = new NuGetConfiguration(_xDocument.Root, origin: this);
        }

        public SettingSection GetSection(string sectionName)
        {
            return _rootElement.GetSection(sectionName);
        }

        public void AddOrUpdate(string sectionName, SettingItem item)
        {
            _rootElement.AddOrUpdate(sectionName, item);
        }

        public void Remove(string sectionName, SettingItem item)
        {
            _rootElement.Remove(sectionName, item);
        }

        public void SaveToDisk()
        {
            if (IsDirty)
            {
                ExecuteSynchronized(() =>
                {
                    FileSystemUtility.AddFile(ConfigFilePath, _xDocument.Save);
                });

                IsDirty = false;
            }
        }

        public bool IsEmpty() => _rootElement == null || _rootElement.IsEmpty();

        public bool TryGetSection(string sectionName, out SettingSection section)
        {
            return _rootElement.Sections.TryGetValue(sectionName, out section);
        }

        public void MergeSectionsInto(Dictionary<string, VirtualSettingSection> sectionsContainer)
        {
            _rootElement.MergeSectionsInto(sectionsContainer);
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
