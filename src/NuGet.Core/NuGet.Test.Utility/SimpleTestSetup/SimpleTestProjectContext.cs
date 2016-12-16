// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Test.Utility
{
    public class SimpleTestProjectContext
    {
        public SimpleTestProjectContext(string projectName, ProjectStyle type, string solutionRoot)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException(nameof(projectName));
            }

            if (string.IsNullOrWhiteSpace(solutionRoot))
            {
                throw new ArgumentException(nameof(solutionRoot));
            }

            ProjectName = projectName;
            ProjectPath = Path.Combine(solutionRoot, projectName, $"{projectName}.csproj");
            OutputPath = Path.Combine(solutionRoot, projectName, "obj");
            Type = type;
        }

        public string Version { get; set; } = "1.0.0";

        public Guid ProjectGuid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// MSBuild project name
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// Project file full path.
        /// </summary>
        public string ProjectPath { get; set; }

        /// <summary>
        /// Base intermediate directory path
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Additional MSBuild properties
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Target frameworks containing dependencies.
        /// </summary>
        public List<SimpleTestProjectFrameworkContext> Frameworks { get; set; } = new List<SimpleTestProjectFrameworkContext>();

        /// <summary>
        /// Project type
        /// </summary>
        public ProjectStyle Type { get; set; }

        /// <summary>
        /// Tool references
        /// </summary>
        public List<SimpleTestPackageContext> DotnetCLIToolReferences { get; set; } = new List<SimpleTestPackageContext>();

        /// <summary>
        /// Project.json file
        /// </summary>
        public JObject ProjectJson { get; set; }

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

        /// <summary>
        /// project.lock.json or project.assets.json
        /// </summary>
        public string AssetsFileOutputPath
        {
            get
            {
                switch (Type)
                {
                    case ProjectStyle.PackageReference:
                        return Path.Combine(OutputPath, "project.assets.json");
                    case ProjectStyle.ProjectJson:
                        return Path.Combine(Path.GetDirectoryName(ProjectPath), "project.lock.json");
                    default:
                        return null;
                }
            }
        }

        public string TargetsOutput
        {
            get
            {
                switch (Type)
                {
                    case ProjectStyle.PackageReference:
                        return Path.Combine(OutputPath, $"{Path.GetFileName(ProjectPath)}.nuget.g.targets");
                    case ProjectStyle.ProjectJson:
                        return Path.Combine(Path.GetDirectoryName(ProjectPath), $"{Path.GetFileNameWithoutExtension(ProjectPath)}.nuget.targets");
                    default:
                        return ProjectPath;
                }
            }
        }

        public string PropsOutput
        {
            get
            {
                switch (Type)
                {
                    case ProjectStyle.PackageReference:
                        return Path.Combine(OutputPath, $"{Path.GetFileName(ProjectPath)}.nuget.g.props");
                    case ProjectStyle.ProjectJson:
                        return Path.Combine(Path.GetDirectoryName(ProjectPath), $"{Path.GetFileNameWithoutExtension(ProjectPath)}.nuget.props");
                    default:
                        return ProjectPath;
                }
            }
        }


        public LockFile AssetsFile
        {
            get
            {
                var path = AssetsFileOutputPath;

                if (File.Exists(path))
                {
                    var format = new LockFileFormat();
                    return format.Read(path);
                }

                return null;
            }
        }

        public void AddPackageToAllFrameworks(params SimpleTestPackageContext[] packages)
        {
            foreach (var framework in Frameworks)
            {
                framework.PackageReferences.AddRange(packages);
            }
        }

        public void AddProjectToAllFrameworks(params SimpleTestProjectContext[] projects)
        {
            foreach (var framework in Frameworks)
            {
                framework.ProjectReferences.AddRange(projects);
            }
        }

        /// <summary>
        /// Package references from all TFMs
        /// </summary>
        public List<SimpleTestPackageContext> AllPackageDependencies
        {
            get
            {
                return Frameworks.SelectMany(f => f.PackageReferences).Distinct().ToList();
            }
        }

        /// <summary>
        /// Project references from all TFMs
        /// </summary>
        public List<SimpleTestProjectContext> AllProjectReferences
        {
            get
            {
                return Frameworks.SelectMany(f => f.ProjectReferences).Distinct().ToList();
            }
        }

        public void Save()
        {
            Save(ProjectPath);
        }

        public void Save(string path)
        {
            var projectFile = new FileInfo(path);
            projectFile.Directory.Create();

            var xml = GetXML();

            File.WriteAllText(path, xml.ToString());

            if (ProjectJson != null)
            {
                var jsonPath = ProjectJsonPathUtilities.GetProjectConfigPath(
                    projectFile.Directory.FullName,
                    Path.GetFileNameWithoutExtension(ProjectPath));

                File.WriteAllText(jsonPath, ProjectJson.ToString());
            }
        }

        public static SimpleTestProjectContext CreateNETCore(
            string projectName,
            string solutionRoot,
            params NuGetFramework[] frameworks)
        {
            var context = new SimpleTestProjectContext(projectName, ProjectStyle.PackageReference, solutionRoot);
            context.Frameworks.AddRange(frameworks.Select(e => new SimpleTestProjectFrameworkContext(e)));
            return context;
        }

        public static SimpleTestProjectContext CreateNonNuGet(
            string projectName,
            string solutionRoot,
            NuGetFramework framework)
        {
            var context = new SimpleTestProjectContext(projectName, ProjectStyle.Unknown, solutionRoot);
            context.Frameworks.Add(new SimpleTestProjectFrameworkContext(framework));
            return context;
        }

        public static SimpleTestProjectContext CreateUAP(
            string projectName,
            string solutionRoot,
            NuGetFramework framework,
            JObject projectJson)
        {
            var context = new SimpleTestProjectContext(projectName, ProjectStyle.ProjectJson, solutionRoot);
            context.Frameworks.Add(new SimpleTestProjectFrameworkContext(framework));
            context.ProjectJson = projectJson;
            return context;
        }

        public XDocument GetXML()
        {
            var s = ResourceTestUtility.GetResource("NuGet.Test.Utility.compiler.resources.project1.csproj", typeof(SimpleTestProjectContext));
            var xml = XDocument.Parse(s);

            AddProperties(xml, new Dictionary<string, string>()
            {
                { "ProjectGuid", "{" + ProjectGuid.ToString() + "}" },
                { "BaseIntermediateOutputPath", OutputPath },
                { "AssemblyName", ProjectName }
            });

            AddProperties(xml, Properties);

            if (Type == ProjectStyle.PackageReference)
            {
                AddProperties(xml, new Dictionary<string, string>()
                {
                    { "Version", Version },
                    { "DebugType", "portable" },
                    { "TargetFrameworks", string.Join(";", Frameworks.Select(f => f.Framework.GetShortFolderName())) },
                });

                var addedToAll = new HashSet<SimpleTestProjectContext>();

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

                        props.Add("Version", package.Version.ToString());

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
                            package.Id,
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

                            if (!addedToAll.Add(project))
                            {
                                // Skip since this was already added
                                continue;
                            }
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

                // Add tool references
                foreach (var tool in DotnetCLIToolReferences)
                {
                    var props = new Dictionary<string, string>();
                    props.Add("Version", tool.Version.ToString());

                    AddItem(
                        xml,
                        "DotnetCLIToolReference",
                        $"{tool.Id}",
                        NuGetFramework.AnyFramework,
                        props);
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

            var lastPropGroup = doc.Root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").Last();
            lastPropGroup.AddAfterSelf(propertyGroup);
        }

        private static void AddItem(XDocument doc,
            string name,
            string identity,
            NuGetFramework framework,
            Dictionary<string, string> properties)
        {
            var ns = doc.Root.GetDefaultNamespace();

            var itemGroup = new XElement(XName.Get("ItemGroup", ns.NamespaceName));
            var entry = new XElement(XName.Get(name, ns.NamespaceName));
            entry.Add(new XAttribute(XName.Get("Include"), identity));
            itemGroup.Add(entry);

            if (framework?.IsSpecificFramework == true)
            {
                entry.Add(new XAttribute(XName.Get("Condition"), $" '$(TargetFramework)' == '{framework.GetShortFolderName()}' "));
            }

            foreach (var pair in properties)
            {
                var subItem = new XElement(XName.Get(pair.Key, ns.NamespaceName), pair.Value);
                entry.Add(subItem);
            }

            var lastItemGroup = doc.Root.Elements().Where(e => e.Name.LocalName == "ItemGroup").Last();
            lastItemGroup.AddAfterSelf(itemGroup);
        }

        public override bool Equals(object obj)
        {
            return StringComparer.Ordinal.Equals(ProjectPath, (obj as SimpleTestProjectContext)?.ProjectPath);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(ProjectPath);
        }

        public override string ToString()
        {
            return ProjectName;
        }
    }
}
