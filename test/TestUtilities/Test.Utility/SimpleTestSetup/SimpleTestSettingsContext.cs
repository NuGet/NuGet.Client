// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Common;
using Xunit;

namespace NuGet.Test.Utility
{
    public class SimpleTestSettingsContext
    {
        /// <summary>
        /// NuGet.Config path on disk
        /// </summary>
        public string ConfigPath { get; }

        /// <summary>
        /// XML
        /// </summary>
        public XDocument XML { get; }


        public SimpleTestSettingsContext(string nugetConfigPath, string userPackagesFolder, string packagesV2, string fallbackFolder, string packageSource)
            : this(nugetConfigPath, GetDefault(userPackagesFolder, packagesV2, fallbackFolder, packageSource))
        {
        }

        public SimpleTestSettingsContext(string path, XDocument xml)
        {
            XML = xml ?? throw new ArgumentNullException(nameof(xml));
            ConfigPath = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <summary>
        /// Save to disk
        /// </summary>
        public void Save()
        {
            FileUtility.Replace((outputPath) =>
            {
                using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    XML.Save(output);
                }
            },
            ConfigPath);
        }

        /// <summary>
        /// Disable automatic package restore and save the file.
        /// </summary>
        public void DisableAutoRestore()
        {
            var section = GetOrAddSection(XML, "packageRestore");

            AddEntry(section, "enabled", "false");
            AddEntry(section, "automatic", "false");

            Save();
        }

        private static XDocument GetDefault(string userPackagesFolder, string packagesV2, string fallbackFolder, string packageSource)
        {
            var doc = GetEmptyConfig();

            var packageSources = GetOrAddSection(doc, "packageSources");
            AddEntry(packageSources, "source", packageSource);

            var fallbackFolders = GetOrAddSection(doc, "fallbackPackageFolders");
            AddEntry(fallbackFolders, "shared", fallbackFolder);

            AddSetting(doc, "globalPackagesFolder", userPackagesFolder);
            AddSetting(doc, "repositoryPath", packagesV2);

            return doc;
        }

        private static XDocument GetEmptyConfig()
        {
            var doc = new XDocument();
            var configuration = new XElement(XName.Get("configuration"));
            doc.Add(configuration);

            var config = GetOrAddSection(doc, "config");
            var packageSources = GetOrAddSection(doc, "packageSources");
            var disabledSources = GetOrAddSection(doc, "disabledPackageSources");
            var fallbackFolders = GetOrAddSection(doc, "fallbackPackageFolders");

            packageSources.Add(new XElement(XName.Get("clear")));
            disabledSources.Add(new XElement(XName.Get("clear")));

            return doc;
        }

        public static XElement GetOrAddSection(XDocument doc, string name)
        {
            var root = doc.Element(XName.Get("configuration"));

            var node = root.Element(XName.Get(name));

            if (node == null)
            {
                node = new XElement(XName.Get(name));
                root.Add(node);
            }

            return node;
        }

        public static void AddPackageSourceCredentialsSection(XDocument doc, string sourceName, string userName, string password, bool clearTextPassword)
        {
            var root = doc.Element(XName.Get("configuration"));

            var sourceNode = new XElement(XName.Get(sourceName));
            AddEntry(sourceNode, "Username", userName);
            if (clearTextPassword)
            {
                AddEntry(sourceNode, "ClearTextPassword", password);
            }
            else
            {
                AddEntry(sourceNode, "Password", password);
            }

            var packageSourceCredentialsNode = new XElement(XName.Get("packageSourceCredentials"));
            packageSourceCredentialsNode.Add(sourceNode);

            root.Add(packageSourceCredentialsNode);
        }

        public static void AddEntry(XElement section, string key, string value)
        {
            var setting = new XElement(XName.Get("add"));
            setting.Add(new XAttribute(XName.Get("key"), key));
            setting.Add(new XAttribute(XName.Get("value"), value));
            section.Add(setting);
        }

        public static void AddEntry(XElement section, string key, string value, string additionalAtrributeName, string additionalAttributeValue)
        {
            var setting = new XElement(XName.Get("add"));
            setting.Add(new XAttribute(XName.Get("key"), key));
            setting.Add(new XAttribute(XName.Get("value"), value));
            setting.Add(new XAttribute(XName.Get(additionalAtrributeName), additionalAttributeValue));
            section.Add(setting);
        }

        public static void AddSetting(XDocument doc, string key, string value)
        {
            RemoveSetting(doc, key);
            var config = GetOrAddSection(doc, "config");

            var setting = new XElement(XName.Get("add"));
            setting.Add(new XAttribute(XName.Get("key"), key));
            setting.Add(new XAttribute(XName.Get("value"), value));
            config.Add(setting);
        }

        public static void RemoveSetting(XDocument doc, string key)
        {
            var config = GetOrAddSection(doc, "config");

            foreach (var item in config.Elements(XName.Get("add")).Where(e => e.Name.LocalName.Equals(key, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                item.Remove();
            }
        }

        public static void RemoveSource(XDocument doc, string key)
        {
            var packageSources = GetOrAddSection(doc, "packageSources");

            foreach (var item in packageSources.Elements(XName.Get("add")).Where(e => e.FirstAttribute.Value.Equals(key, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                item.Remove();
            }
        }

        // Add nuget.org as package source when NetStandard.Library and NetCorePlatforms packages are needed and save the file.
        public void AddNetStandardFeeds()
        {
            const string nuget = "https://api.nuget.org/v3/index.json";

            var section = GetOrAddSection(XML, "packageSources");
            AddEntry(section, "nuget", nuget);

            Save();
        }
    }
}
