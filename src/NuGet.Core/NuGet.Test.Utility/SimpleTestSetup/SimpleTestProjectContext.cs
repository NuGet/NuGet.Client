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
        private PackageSpec _packageSpec;

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
        /// True for non-xplat.
        /// </summary>
        public bool IsLegacyPackageReference { get; set; }

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
        /// Original Target framework strings.
        /// </summary>
        public List<string> OriginalFrameworkStrings { get; set; } = new List<string>();

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

        public bool ToolingVersion15 { get; set; } = false;

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

        public PackageSpec PackageSpec
        {
            get
            {
                if (_packageSpec == null)
                {
                    _packageSpec = new PackageSpec(Frameworks
                        .Select(f => new TargetFrameworkInformation() { FrameworkName = f.Framework })
                        .ToList());
                    _packageSpec.RestoreMetadata = new ProjectRestoreMetadata();
                    _packageSpec.Name = ProjectName;
                    _packageSpec.FilePath = ProjectPath;
                    _packageSpec.RestoreMetadata.ProjectUniqueName = ProjectName;
                    _packageSpec.RestoreMetadata.ProjectName = ProjectName;
                    _packageSpec.RestoreMetadata.ProjectPath = ProjectPath;
                    _packageSpec.RestoreMetadata.ProjectStyle = Type;
                    _packageSpec.RestoreMetadata.OutputPath = AssetsFileOutputPath;
                    _packageSpec.RestoreMetadata.OriginalTargetFrameworks = OriginalFrameworkStrings;
                    _packageSpec.RestoreMetadata.TargetFrameworks = Frameworks
                        .Select(f => new ProjectRestoreMetadataFrameworkInfo(f.Framework))
                        .ToList();
                    if (Type == ProjectStyle.ProjectJson)
                    {
                        _packageSpec.RestoreMetadata.ProjectJsonPath = Path.Combine(Path.GetDirectoryName(ProjectPath), "project.json");
                    }
                    if (Frameworks.Count() > 1)
                    {
                        _packageSpec.RestoreMetadata.CrossTargeting = true;
                    }
                }

                return _packageSpec;
            }
        }

        public void AddPackageToAllFrameworks(params SimpleTestPackageContext[] packages)
        {
            foreach (var framework in Frameworks)
            {
                framework.PackageReferences.AddRange(packages);
            }
        }

        public void AddPackageToFramework(string packageFramework, params SimpleTestPackageContext[] packages)
        {
            var framework = Frameworks
                .Where(f => f.Framework == NuGetFramework.Parse(packageFramework))
                .First();
            framework.PackageReferences.AddRange(packages);
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

        /// <summary>
        /// Create a UAP package reference project. Framework is only used internally.
        /// </summary>
        public static SimpleTestProjectContext CreateLegacyPackageReference(
            string projectName,
            string solutionRoot,
            NuGetFramework framework)
        {
            var context = new SimpleTestProjectContext(projectName, ProjectStyle.PackageReference, solutionRoot);
            context.Frameworks.Add(new SimpleTestProjectFrameworkContext(framework));
            context.IsLegacyPackageReference = true;
            return context;
        }

        public static SimpleTestProjectContext CreateNETCore(
            string projectName,
            string solutionRoot,
            params NuGetFramework[] frameworks)
        {
            var context = new SimpleTestProjectContext(projectName, ProjectStyle.PackageReference, solutionRoot);
            context.Frameworks.AddRange(frameworks.Select(e => new SimpleTestProjectFrameworkContext(e)));
            context.Properties.Add("RestoreProjectStyle", "PackageReference");
            return context;
        }

        public static SimpleTestProjectContext CreateNETCoreWithSDK(
            string projectName,
            string solutionRoot,
            bool isToolingVersion15,
            params NuGetFramework[] frameworks)
        {
            var context = new SimpleTestProjectContext(projectName, ProjectStyle.PackageReference, solutionRoot);
            context.Frameworks.AddRange(frameworks.Select(e => new SimpleTestProjectFrameworkContext(e)));
            context.ToolingVersion15 = isToolingVersion15;
            return context;
        }

        public static SimpleTestProjectContext CreateNETCoreWithSDK(
            string projectName,
            string solutionRoot,
            bool isToolingVersion15,
            params string[] frameworks)
        {
            var context = new SimpleTestProjectContext(projectName, ProjectStyle.PackageReference, solutionRoot);
            context.OriginalFrameworkStrings.AddRange(frameworks);
            context.Frameworks.AddRange(frameworks.Select(f => new SimpleTestProjectFrameworkContext(NuGetFramework.Parse(f))));
            context.ToolingVersion15 = isToolingVersion15;
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
            var sampleCSProjPath = (Type == ProjectStyle.PackageReference && ToolingVersion15) ?
                "NuGet.Test.Utility.compiler.resources.project2.csproj" :
                "NuGet.Test.Utility.compiler.resources.project1.csproj";

            var s = ResourceTestUtility.GetResource(sampleCSProjPath, typeof(SimpleTestProjectContext));
            var xml = XDocument.Parse(s);

            ProjectFileUtils.AddProperties(xml, new Dictionary<string, string>()
            {
                { "ProjectGuid", "{" + ProjectGuid.ToString() + "}" },
                { "BaseIntermediateOutputPath", OutputPath },
                { "AssemblyName", ProjectName }
            });

            ProjectFileUtils.AddProperties(xml, Properties);

            if (Type == ProjectStyle.PackageReference)
            {
                ProjectFileUtils.AddProperties(xml, new Dictionary<string, string>()
                {
                    { "Version", Version },
                    { "DebugType", "portable" }
                });

                if (!IsLegacyPackageReference)
                {
                    ProjectFileUtils.AddProperties(xml, new Dictionary<string, string>()
                    {
                        { "TargetFrameworks", string.Join(";", Frameworks.Select(f => f.Framework.GetShortFolderName())) },
                    });
                }

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

                        ProjectFileUtils.AddItem(
                            xml,
                            "PackageReference",
                            package.Id,
                            referenceFramework,
                            props,
                            new Dictionary<string, string>());
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

                        ProjectFileUtils.AddItem(
                            xml,
                            "ProjectReference",
                            $"{project.ProjectPath}",
                            referenceFramework,
                            props,
                            new Dictionary<string, string>());
                    }
                }

                // Add tool references
                foreach (var tool in DotnetCLIToolReferences)
                {
                    var props = new Dictionary<string, string>();
                    props.Add("Version", tool.Version.ToString());

                    ProjectFileUtils.AddItem(
                        xml,
                        "DotnetCLIToolReference",
                        $"{tool.Id}",
                        NuGetFramework.AnyFramework,
                        props,
                        new Dictionary<string, string>());
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

                    ProjectFileUtils.AddItem(
                        xml,
                        "ProjectReference",
                        $"{project.ProjectPath}",
                        NuGetFramework.AnyFramework,
                        props,
                        new Dictionary<string, string>());
                }
            }

            return xml;
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