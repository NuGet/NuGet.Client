// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public static class SimpleTestPackageUtility
    {
        /// <summary>
        /// Creates a net45 package containing lib, build, native, tools, and contentFiles
        /// </summary>
        public static FileInfo CreateFullPackage(
           string repositoryDir,
           string id,
           string version)
        {
            return CreateFullPackage(repositoryDir, id, version, new PackageDependency[0]);
        }

        /// <summary>
        /// Creates a net45 package containing lib, build, native, tools, and contentFiles
        /// </summary>
        public static FileInfo CreateFullPackage(
           string repositoryDir,
           string id,
           string version,
           IEnumerable<PackageDependency> dependencies)
        {
            var package = new SimpleTestPackageContext()
            {
                Id = id,
                Version = version
            };

            package.Dependencies.AddRange(dependencies.Select(d => new SimpleTestPackageContext()
            {
                Id = d.Id,
                Version = d.VersionRange.MinVersion.ToString() ?? "1.0.0",
                Include = string.Join(",", d.Include),
                Exclude = string.Join(",", d.Include),
            }));

            return CreateFullPackage(repositoryDir, package);
        }

        /// <summary>
        /// Creates a net45 package containing lib, build, native, tools, and contentFiles
        /// </summary>
        public static FileInfo CreateFullPackage(
           string repositoryDir,
           SimpleTestPackageContext packageContext)
        {
            var id = packageContext.Id;
            var version = packageContext.Version;
            var runtimeJson = packageContext.RuntimeJson;

            var file = new FileInfo(Path.Combine(repositoryDir, $"{id}.{version}.nupkg"));

            file.Directory.Create();

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                if (packageContext.Files.Any())
                {
                    foreach (var entryFile in packageContext.Files)
                    {
                        zip.AddEntry(entryFile.Key, entryFile.Value);
                    }
                }
                else
                {
                    zip.AddEntry("contentFiles/any/any/config.xml", new byte[] { 0 });
                    zip.AddEntry("contentFiles/cs/net45/code.cs", new byte[] { 0 });
                    zip.AddEntry("lib/net45/a.dll", new byte[] { 0 });
                    zip.AddEntry("lib/netstandard1.0/a.dll", new byte[] { 0 });
                    zip.AddEntry($"build/net45/{id}.targets", @"<targets />", Encoding.UTF8);
                    zip.AddEntry("native/net45/a.dll", new byte[] { 0 });
                    zip.AddEntry("tools/a.exe", new byte[] { 0 });
                }

                if (!string.IsNullOrEmpty(runtimeJson))
                {
                    zip.AddEntry("runtime.json", runtimeJson, Encoding.UTF8);
                }

                var nuspecXml = packageContext.Nuspec?.ToString() ?? $@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                        <metadata>
                            <id>{id}</id>
                            <version>{version}</version>
                            <title />
                            <frameworkAssemblies>
                                <frameworkAssembly assemblyName=""System.Runtime"" />
                            </frameworkAssemblies>
                            <contentFiles>
                                <files include=""cs/net45/config/config.xml"" buildAction=""none"" />
                                <files include=""cs/net45/config/config.xml"" copyToOutput=""true"" flatten=""false"" />
                                <files include=""cs/net45/images/image.jpg"" buildAction=""embeddedresource"" />
                            </contentFiles>
                        </metadata>
                        </package>";

                var xml = XDocument.Parse(nuspecXml);

                // Add the min client version if it exists
                if (!string.IsNullOrEmpty(packageContext.MinClientVersion))
                {
                    xml.Root.Element(XName.Get("metadata"))
                        .Add(new XAttribute(XName.Get("minClientVersion"), packageContext.MinClientVersion));
                }

                var dependencies = packageContext.Dependencies.Select(e =>
                    new PackageDependency(
                        e.Id,
                        VersionRange.Parse(e.Version),
                        string.IsNullOrEmpty(e.Include)
                            ? new List<string>()
                            : e.Include.Split(',').ToList(),
                        string.IsNullOrEmpty(e.Exclude)
                            ? new List<string>()
                            : e.Exclude.Split(',').ToList()));

                if (dependencies.Any())
                {
                    var metadata = xml.Element(XName.Get("package")).Element(XName.Get("metadata"));

                    var dependenciesNode = new XElement(XName.Get("dependencies"));
                    var groupNode = new XElement(XName.Get("group"));
                    dependenciesNode.Add(groupNode);
                    metadata.Add(dependenciesNode);

                    foreach (var dependency in dependencies)
                    {
                        var node = new XElement(XName.Get("dependency"));
                        groupNode.Add(node);
                        node.Add(new XAttribute(XName.Get("id"), dependency.Id));
                        node.Add(new XAttribute(XName.Get("version"), dependency.VersionRange.ToNormalizedString()));

                        if (dependency.Include.Count > 0)
                        {
                            node.Add(new XAttribute(XName.Get("include"), string.Join(",", dependency.Include)));
                        }

                        if (dependency.Exclude.Count > 0)
                        {
                            node.Add(new XAttribute(XName.Get("exclude"), string.Join(",", dependency.Exclude)));
                        }
                    }
                }

                zip.AddEntry($"{id}.nuspec", xml.ToString(), Encoding.UTF8);
            }

            return file;
        }

        /// <summary>
        /// Create packages.
        /// </summary>
        public static void CreatePackages(string repositoryPath, params SimpleTestPackageContext[] package)
        {
            CreatePackages(package.ToList(), repositoryPath);
        }

        /// <summary>
        /// Create all packages in the list, including dependencies.
        /// </summary>
        public static void CreatePackages(List<SimpleTestPackageContext> packages, string repositoryPath)
        {
            var done = new HashSet<PackageIdentity>();
            var toCreate = new Stack<SimpleTestPackageContext>(packages);

            while (toCreate.Count > 0)
            {
                var package = toCreate.Pop();

                if (done.Add(package.Identity))
                {
                    CreateFullPackage(
                        repositoryPath,
                        package);

                    foreach (var dep in package.Dependencies)
                    {
                        toCreate.Push(dep);
                    }
                }
            }
        }

        /// <summary>
        /// Create packages with PackageBuilder, this includes OPC support.
        /// </summary>
        public static void CreateOPCPackage(SimpleTestPackageContext package, string repositoryPath)
        {
            CreateOPCPackages(new List<SimpleTestPackageContext>() { package }, repositoryPath);
        }

        /// <summary>
        /// Create packages with PackageBuilder, this includes OPC support.
        /// </summary>
        public static void CreateOPCPackages(List<SimpleTestPackageContext> packages, string repositoryPath)
        {
            foreach (var package in packages)
            {
                var builder = new Packaging.PackageBuilder()
                {
                    Id = package.Id,
                    Version = NuGetVersion.Parse(package.Version),
                    Description = "Description.",
                };

                builder.Authors.Add("testAuthor");

                foreach (var file in package.Files)
                {
                    builder.Files.Add(CreatePackageFile(file.Key));
                }

                using (var stream = File.OpenWrite(Path.Combine(repositoryPath, $"{package.Identity.Id}.{package.Identity.Version.ToString()}.nupkg")))
                {
                    builder.Save(stream);
                }
            }
        }

        private static IPackageFile CreatePackageFile(string name)
        {
            InMemoryFile file = new InMemoryFile();
            file.Path = name;
            file.Stream = new MemoryStream();

            string effectivePath;
            var fx = FrameworkNameUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.EffectivePath = effectivePath;
            file.TargetFramework = fx;

            return file;
        }

        private class InMemoryFile : IPackageFile
        {
            public string EffectivePath { get; set; }

            public string Path { get; set; }

            public FrameworkName TargetFramework { get; set; }

            public MemoryStream Stream { get; set; }

            public Stream GetStream()
            {
                return Stream;
            }
        }
    }
}
