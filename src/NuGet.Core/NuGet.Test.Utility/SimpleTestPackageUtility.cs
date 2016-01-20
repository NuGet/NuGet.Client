// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public class SimpleTestPackageUtility
    {
        /// <summary>
        /// Creates a net45 package containing lib, build, native, tools, and contentFiles
        /// </summary>
        public static FileInfo CreateFullPackage(
           string repositoryDir,
           string id,
           string version)
        {
            return CreateFullPackage(repositoryDir, id, version, new Packaging.Core.PackageDependency[0]);
        }

        /// <summary>
        /// Creates a net45 package containing lib, build, native, tools, and contentFiles
        /// </summary>
        public static FileInfo CreateFullPackage(
           string repositoryDir,
           string id,
           string version,
           IEnumerable<Packaging.Core.PackageDependency> dependencies)
        {
            var file = new FileInfo(Path.Combine(repositoryDir, $"{id}.{version}.nupkg"));

            file.Directory.Create();

            using (var zip = new ZipArchive(File.Create(file.FullName), ZipArchiveMode.Create))
            {
                zip.AddEntry("contentFiles/any/any/config.xml", new byte[] { 0 });
                zip.AddEntry("contentFiles/cs/net45/code.cs", new byte[] { 0 });
                zip.AddEntry("lib/net45/a.dll", new byte[] { 0 });
                zip.AddEntry($"build/net45/{id}.targets", @"<targets />", Encoding.UTF8);
                zip.AddEntry("native/net45/a.dll", new byte[] { 0 });
                zip.AddEntry("tools/a.exe", new byte[] { 0 });

                var nuspecXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
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

        public static void CreatePackages(List<SimpleTestPackageContext> packages, string repositoryPath)
        {
            var done = new HashSet<PackageIdentity>();
            var toCreate = new Stack<SimpleTestPackageContext>(packages);

            while (toCreate.Count > 0)
            {
                var package = toCreate.Pop();

                if (done.Add(package.Identity))
                {
                    var dependencies = package.Dependencies.Select(e =>
                        new Packaging.Core.PackageDependency(
                            e.Id,
                            VersionRange.Parse(e.Version),
                            string.IsNullOrEmpty(e.Include)
                                ? new List<string>()
                                : e.Include.Split(',').ToList(),
                            string.IsNullOrEmpty(e.Exclude)
                                ? new List<string>()
                                : e.Exclude.Split(',').ToList()));

                    CreateFullPackage(
                        repositoryPath,
                        package.Id,
                        package.Version,
                        dependencies);

                    foreach (var dep in package.Dependencies)
                    {
                        toCreate.Push(dep);
                    }
                }
            }
        }
    }
}
