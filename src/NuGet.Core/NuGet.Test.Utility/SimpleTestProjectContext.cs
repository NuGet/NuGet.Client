// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public class SimpleTestProjectContext
    {
        public SimpleTestProjectContext()
        {
        }

        public string Version { get; set; } = "1.0.0";

        public Guid ProjectGuid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// MSBuild project name
        /// </summary>
        public string ProjectName { get; set; } = "projectA";

        /// <summary>
        /// Project file full path.
        /// </summary>
        public string ProjectPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), $"projectA.csproj");

        /// <summary>
        /// Base intermediate directory path
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Target frameworks containing dependencies.
        /// </summary>
        public List<SimpleTestProjectFrameworkContext> Frameworks { get; set; } = new List<SimpleTestProjectFrameworkContext>();

        /// <summary>
        /// Project type
        /// </summary>
        public RestoreOutputType Type { get; set; } = RestoreOutputType.NETCore;

        /// <summary>
        /// Include attributes for the parent project referencing this one.
        /// </summary>
        public string IncludeAssets { get; set; } = string.Empty;

        /// <summary>
        /// Include attributes for the parent project referencing this one.
        /// </summary>
        public string ExcludeAssets { get; set; } = string.Empty;

        /// <summary>
        /// Include attributes for the parent project referencing this one.
        /// </summary>
        public string PrivateAssets { get; set; } = string.Empty;

        public void Save()
        {
            Save(ProjectPath);
        }

        public void Save(string path)
        {
            var xml = GetXML();
            xml.Save(path);
        }

        public static SimpleTestProjectContext CreateForFrameworks(params NuGetFramework[] frameworks)
        {
            var context = new SimpleTestProjectContext();
            context.Frameworks.AddRange(frameworks.Select(e => new SimpleTestProjectFrameworkContext(e)));
            return context;
        }

        public XDocument GetXML()
        {
            var s = ResourceTestUtility.GetResource("NuGet.Test.Utility.compiler.resources.project1.csproj", typeof(SimpleTestProjectContext));
            var xml = XDocument.Parse(s);

            AddProperties(xml, new Dictionary<string, string>()
            {
                { "ProjectGuid", "{" + ProjectGuid.ToString() + "}" },
            });

            if (Type == RestoreOutputType.NETCore)
            {
                AddProperties(xml, new Dictionary<string, string>()
                {
                    { "VersionPrefix", Version },
                    { "DebugType", "portable" },
                    { "TargetFrameworks", string.Join(";", Frameworks.Select(f => f.Framework.GetShortFolderName())) },
                });

                foreach (var frameworkInfo in Frameworks)
                {
                    foreach (var package in frameworkInfo.PackageReferences)
                    {
                        var referenceFramework = frameworkInfo.Framework;

                        // Drop the conditional if it is not needed
                        if (Frameworks.All(f => f.PackageReferences.Contains(package)))
                        {
                            referenceFramework = NuGetFramework.AnyFramework;
                        }

                        var props = new Dictionary<string, string>();

                        if (!string.IsNullOrEmpty(package.Include))
                        {
                            props.Add("IncludeAssets", package.Include);
                        }

                        if (!string.IsNullOrEmpty(package.Exclude))
                        {
                            props.Add("ExcludeAssets", package.Exclude);
                        }

                        if (!string.IsNullOrEmpty(package.PrivateAssets))
                        {
                            props.Add("PrivateAssets", package.PrivateAssets);
                        }

                        AddItem(
                            xml,
                            "PackageReference",
                            $"{package.Id}/{package.Version.ToString()}",
                            referenceFramework,
                            props);
                    }

                    foreach (var project in frameworkInfo.ProjectReferences)
                    {
                        var referenceFramework = frameworkInfo.Framework;

                        // Drop the conditional if it is not needed
                        if (Frameworks.All(f => f.ProjectReferences.Contains(project)))
                        {
                            referenceFramework = NuGetFramework.AnyFramework;
                        }

                        var props = new Dictionary<string, string>();
                        props.Add("Name", project.ProjectName);
                        props.Add("Project", project.ProjectGuid.ToString());

                        if (!string.IsNullOrEmpty(project.ExcludeAssets))
                        {
                            props.Add("IncludeAssets", project.ExcludeAssets);
                        }

                        if (!string.IsNullOrEmpty(project.ExcludeAssets))
                        {
                            props.Add("ExcludeAssets", project.ExcludeAssets);
                        }

                        if (!string.IsNullOrEmpty(project.PrivateAssets))
                        {
                            props.Add("PrivateAssets", project.PrivateAssets);
                        }

                        AddItem(
                            xml,
                            "ProjectReference",
                            $"{project.ProjectPath}",
                            referenceFramework,
                            props);
                    }
                }
            }
            else
            {
                // Add all project references directly
                foreach (var project in Frameworks.SelectMany(f => f.ProjectReferences).Distinct())
                {
                    var props = new Dictionary<string, string>();
                    props.Add("Name", project.ProjectName);
                    props.Add("Project", project.ProjectGuid.ToString());

                    if (!string.IsNullOrEmpty(project.ExcludeAssets))
                    {
                        props.Add("IncludeAssets", project.ExcludeAssets);
                    }

                    if (!string.IsNullOrEmpty(project.ExcludeAssets))
                    {
                        props.Add("ExcludeAssets", project.ExcludeAssets);
                    }

                    if (!string.IsNullOrEmpty(project.PrivateAssets))
                    {
                        props.Add("PrivateAssets", project.PrivateAssets);
                    }

                    AddItem(
                        xml,
                        "ProjectReference",
                        $"{project.ProjectPath}",
                        NuGetFramework.AnyFramework,
                        props);
                }
            }

            return xml;
        }

        private static void AddProperties(XDocument doc, Dictionary<string, string> properties)
        {
            var ns = doc.Root.GetDefaultNamespace();

            var propertyGroup = new XElement(XName.Get("PropertyGroup", ns.NamespaceName));
            foreach (var pair in properties)
            {
                var subItem = new XElement(XName.Get(pair.Key, ns.NamespaceName), pair.Value);
                propertyGroup.Add(subItem);
            }

            doc.Add(propertyGroup);
        }

        private static void AddItem(XDocument doc,
            string name,
            string identity,
            NuGetFramework framework,
            Dictionary<string, string> properties)
        {
            var ns = doc.Root.GetDefaultNamespace();

            var propertyGroup = new XElement(XName.Get("ItemGroup", ns.NamespaceName));
            var entry = new XElement(XName.Get(name, ns.NamespaceName));
            entry.Add(new XAttribute(XName.Get("Include"), identity));

            if (framework?.IsSpecificFramework == true)
            {
                entry.Add(new XAttribute(XName.Get("Condition"), $" '$(TargetFramework)' == '{framework.GetShortFolderName()}' "));
            }

            foreach (var pair in properties)
            {
                var subItem = new XElement(XName.Get(pair.Key, ns.NamespaceName), pair.Value);
                entry.Add(subItem);
            }

            propertyGroup.Add(entry);
            doc.Add(propertyGroup);
        }
    }
}
