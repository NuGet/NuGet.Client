// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Dotnet.Integration.Test;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Represents a builder for dotnet core projects for integration tests.
    /// Relies on <c>MsbuildIntegrationTestFixture</c> to programmatically create the project
    /// </summary>
    /// <seealso cref="MsbuildIntegrationTestFixture"/>
    internal class ProjectFileBuilder
    {
        public string PackageIcon { get; private set; }
        public string PackageIconUrl { get; private set; }
        public string ProjectName { get; private set; }
        public List<ItemEntry> ItemGroupEntries { get; private set; }
        public string BaseDir { get; private set; }
        public string ProjectFilePath => Path.Combine(BaseDir, ProjectName, $"{ProjectName}.csproj");
        public string ProjectFolder => Path.Combine(BaseDir, ProjectName);
        public Dictionary<string, string> Properties { get; private set; }

        private ProjectFileBuilder()
        {
            ItemGroupEntries = new List<ItemEntry>();
            Properties = new Dictionary<string, string>();
        }

        /// <summary>
        /// Factory method
        /// </summary>
        /// <returns>An instance of <c>ProjectFileBuilder</c></returns>
        public static ProjectFileBuilder Create()
        {
            return new ProjectFileBuilder();
        }

        public void Build(MsbuildIntegrationTestFixture fixture, string path)
        {
            BaseDir = path;

            fixture.CreateDotnetNewProject(path, ProjectName, " classlib");

            ModifyProjectFile();
        }

        /// <summary>
        /// Adds and intem inside a &lt;ItemGroup/&gt; node in the following form:
        /// <c>&lt;{itemType} Include="{itemPath}" [PackagePath="{packagePath}"] /&gt;</c>
        /// </summary>
        public ProjectFileBuilder WithItem(string itemType, string itemPath, string packagePath)
        {
            ItemGroupEntries.Add(new ItemEntry(itemType, itemPath, packagePath));

            return this;
        }

        public ProjectFileBuilder WithPackageIcon(string packageIcon)
        {
            PackageIcon = packageIcon;

            return this;
        }

        public ProjectFileBuilder WithPackageIconUrl(string packageIconUrl)
        {
            PackageIconUrl = packageIconUrl;

            return this;
        }

        public ProjectFileBuilder WithProjectName(string projectName)
        {
            ProjectName = projectName;

            return this;
        }

        public ProjectFileBuilder WithProperty(string propertyName, string value)
        {
            Properties[propertyName] = value;

            return this;
        }

        private void ModifyProjectFile()
        {
            using (FileStream stream = new FileStream(ProjectFilePath, FileMode.Open, FileAccess.ReadWrite))
            {
                var xml = XDocument.Load(stream);

                if (PackageIconUrl != null)
                {
                    ProjectFileUtils.AddProperty(xml, "PackageIconUrl", PackageIconUrl);
                }

                if (PackageIcon != null)
                {
                    ProjectFileUtils.AddProperty(xml, "PackageIcon", PackageIcon);
                }

                ProjectFileUtils.AddProperties(xml, Properties);

                var attributes = new Dictionary<string, string>();
                var properties = new Dictionary<string, string>();
                attributes["Pack"] = "true";
                foreach (var tup in ItemGroupEntries)
                {
                    attributes.Remove("PackagePath");

                    if (tup.PackagePath != null)
                    {
                        attributes["PackagePath"] = tup.PackagePath;
                    }

                    ProjectFileUtils.AddItem(xml, tup.ItemType, tup.ItemPath, string.Empty, properties, attributes);
                }

                ProjectFileUtils.WriteXmlToFile(xml, stream);
            }
        }

        /// <summary>
        /// Represents an MSBuild project file Item entry created by <c>ProjectFileBuilder</c>.
        /// For testing purposes.
        /// </summary>
        internal class ItemEntry
        {
            public string ItemType { get; }
            public string ItemPath { get; }
            public string PackagePath { get; }

            public ItemEntry(string itemType, string itemPath, string packagePath)
            {
                ItemType = itemType;
                ItemPath = itemPath;
                PackagePath = packagePath;
            }
        }
    }
}
