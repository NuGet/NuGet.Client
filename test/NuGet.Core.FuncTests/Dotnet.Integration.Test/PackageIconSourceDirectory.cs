// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Collections.Generic;
using NuGet.Test.Utility;
using System.Collections;
using System.Xml.Linq;

namespace Dotnet.Integration.Test
{
    public class PackageIconTestSourceDirectory : IDisposable
    {
        public string ProjectName { get; }
        public string PackageIconEntry { get; }
        public string PackageIconUrlEntry { get; }
        private TestDirectory TestDirectory { get; }
        public string BaseDir => TestDirectory.Path;
        public string ProjectFolder => Path.Combine(BaseDir, ProjectName);
        public string ProjectFile => Path.Combine(BaseDir, ProjectName, $"{ProjectName}.csproj");

        public PackageIconTestSourceDirectory(
                string projectName,
                string packageIconEntry,
                string packageIconUrlEntry,
                MsbuildIntegrationTestFixture fixture,
                IEnumerable<Tuple<string, int>> files,
                IEnumerable<Tuple<string, string, string>> fileEntries)
        {
            ProjectName = projectName;
            PackageIconEntry = packageIconEntry;
            PackageIconUrlEntry = packageIconUrlEntry;
            TestDirectory = TestDirectory.Create();

            fixture.CreateDotnetNewProject(TestDirectory.Path, projectName, " classlib");            

            CreateFiles(files);

            ModifyProjectFile(fileEntries);
        }

        private void ModifyProjectFile(IEnumerable<Tuple<string, string, string>> fileEntries)
        {
            using (var stream = new FileStream(ProjectFile, FileMode.Open, FileAccess.ReadWrite))
            {
                var xml = XDocument.Load(stream);

                if (PackageIconUrlEntry != null)
                {
                    ProjectFileUtils.AddProperty(xml, "PackageIconUrl", PackageIconEntry);
                }

                if (PackageIconEntry != null)
                {
                    ProjectFileUtils.AddProperty(xml, "PackageIcon", PackageIconEntry);
                }

                foreach (var tup in fileEntries)
                {
                    ProjectFileUtils.AddItem(xml, tup.Item1, tup.Item2, string.Empty, null, null);
                }

                ProjectFileUtils.WriteXmlToFile(xml, stream);
            }
        }

        private void CreateFiles(IEnumerable<Tuple<string, int>> files)
        {
            foreach (var f in files)
            {
                var filepath = Path.Combine(BaseDir, f.Item1);
                var dir = Path.GetDirectoryName(filepath);

                Directory.CreateDirectory(dir);
                using (var fileStream = File.OpenWrite(Path.Combine(BaseDir, f.Item1)))
                {
                    fileStream.SetLength(f.Item2);
                }
            }
        }

        public void Dispose()
        {
            TestDirectory.Dispose();
        }
    }

    public class PackageIconTestSourceDirectoryBuilder
    {
        private string ProjectName { get; set; }
        private string PackageIcon { get; set; }
        private string PackageIconUrl { get; set; }
        private List<Tuple<string, int>> Files { get; set; }
        private List<Tuple<string, string, string>> ContentEntries { get; set; }
        private PackageIconTestSourceDirectoryBuilder() { }


        public static PackageIconTestSourceDirectoryBuilder Create(string projectName)
        {
            return new PackageIconTestSourceDirectoryBuilder
            {
                ProjectName = projectName,
                Files = new List<Tuple<string, int>>(),
                ContentEntries = new List<Tuple<string, string, string>>()
            };
        }

        public PackageIconTestSourceDirectoryBuilder WithFile(string filePath, int fileSize)
        {
            Files.Add(Tuple.Create(filePath, fileSize));

            return this;
        }

        public PackageIconTestSourceDirectoryBuilder WithPackageIcon(string packageIcon)
        {
            PackageIcon = packageIcon;

            return this;
        }

        public PackageIconTestSourceDirectoryBuilder WithPackageIconUrl(string packageIconUrl)
        {
            PackageIconUrl = packageIconUrl;

            return this;
        }

        public PackageIconTestSourceDirectory Build(MsbuildIntegrationTestFixture fixture)
        {
            return new PackageIconTestSourceDirectory(
                ProjectName,
                PackageIcon,
                PackageIconUrl,
                fixture,
                Files,
                ContentEntries);
        }
    }
}
