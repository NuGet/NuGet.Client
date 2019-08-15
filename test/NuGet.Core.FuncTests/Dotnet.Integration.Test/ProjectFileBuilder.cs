// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Dotnet.Integration.Test;
using NuGet.Commands;

namespace NuGet.Test.Utility
{
    public class ProjectFileBuilder
    {
        public string PackageIcon { get; private set; }
        public string PackageIconUrl { get; private set; }
        public string ProjectName { get; private set; }
        public List<Tuple<string, string, string>> ItemGroupEntries { get; private set; }
        public string BaseDir { get; private set; }
        public string ProjectFilepath => Path.Combine(BaseDir, ProjectName, $"{ProjectName}.csproj");
        public string ProjectFolder => Path.Combine(BaseDir, ProjectName);

        private ProjectFileBuilder()
        {
            ItemGroupEntries = new List<Tuple<string, string, string>>();
        }

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

        public ProjectFileBuilder WithItem(string type, string path, string packagePath)
        {
            ItemGroupEntries.Add(Tuple.Create(type, path, packagePath));

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

        private void ModifyProjectFile()
        {
            using (FileStream stream = new FileStream(ProjectFilepath, FileMode.Open, FileAccess.ReadWrite))
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

                var attributes = new Dictionary<string, string>();
                var properties = new Dictionary<string, string>();
                attributes["Pack"] = "true";
                foreach (var tup in ItemGroupEntries)
                {
                    attributes.Remove("PackagePath");

                    if (tup.Item3 != null)
                    {
                        attributes["PackagePath"] = tup.Item3;
                    }

                    ProjectFileUtils.AddItem(xml, tup.Item1, tup.Item2, string.Empty, properties, attributes);
                }

                ProjectFileUtils.WriteXmlToFile(xml, stream);
            }
        }
    }
}
