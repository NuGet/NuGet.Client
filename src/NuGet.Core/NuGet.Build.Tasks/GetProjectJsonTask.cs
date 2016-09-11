using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Build.Tasks
{
    public class GetProjectJsonTask : Task
    {
        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string RestoreProjectPath { get; set; }

        [Required]
        public string[] ProjectReferences { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Required]
        public string OutputType { get; set; }

        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public TaskItem[] RestoreItemsFromProjectJson { get; set; }

        public override bool Execute()
        {
            var entries = new List<TaskItem>();

            var packageSpec = GetPackageSpec(RestoreProjectPath);

            if (packageSpec != null)
            {
                foreach (var frameworkInfo in packageSpec.TargetFrameworks)
                {
                    var entryGuid = Guid.NewGuid().ToString();

                    var properties = new Dictionary<string, string>();

                    var frameworkShortName = frameworkInfo.FrameworkName.GetShortFolderName();

                    properties.Add("Type", "ProjectSpec");
                    properties.Add("ProjectSpecId", entryGuid);
                    properties.Add("ProjectJsonPath", packageSpec.FilePath);
                    properties.Add("ProjectPath", RestoreProjectPath);
                    properties.Add("OutputType", OutputType);
                    properties.Add("TargetFrameworks", frameworkShortName);

                    // Temporarily add the framework short name to the end
                    var outputInfo = new DirectoryInfo(OutputPath);
                    if (NuGetFramework.Parse(outputInfo.Name).IsSpecificFramework)
                    {
                        properties.Add("OutputPath", OutputPath);
                    }
                    else
                    {
                        properties.Add("OutputPath", Path.Combine(OutputPath, frameworkShortName));
                    }

                    if (frameworkInfo.Imports.Count > 0)
                    {
                        properties.Add("Imports", string.Join(";", frameworkInfo.Imports.Select(f => f.GetShortFolderName())));
                    }

                    if (packageSpec.RuntimeGraph.Supports.Count > 0)
                    {
                        properties.Add("Supports", string.Join(";", packageSpec.RuntimeGraph.Supports.Select(e => e.Key)));
                    }

                    if (packageSpec.RuntimeGraph.Runtimes.Count > 0)
                    {
                        properties.Add("Runtimes", string.Join(";", packageSpec.RuntimeGraph.Runtimes.Select(e => e.Key)));
                    }

                    entries.Add(new TaskItem(entryGuid, properties));

                    // Add projects
                    foreach (var projectReference in ProjectReferences)
                    {
                        properties = new Dictionary<string, string>();
                        properties.Add("ProjectSpecId", entryGuid);
                        properties.Add("Type", "ProjectReference");
                        properties.Add("ProjectPath", projectReference);

                        entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));
                    }

                    // Add dependencies
                    var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var dependency in frameworkInfo.Dependencies.Concat(packageSpec.Dependencies))
                    {
                        if (dependency.LibraryRange.TypeConstraintAllowsAnyOf(LibraryDependencyTarget.PackageProjectExternal))
                        {
                            if (!added.Add(dependency.Name))
                            {
                                var includeFlags = LibraryIncludeFlagUtils.GetFlagString(dependency.IncludeType);
                                var suppressParent = LibraryIncludeFlagUtils.GetFlagString(dependency.SuppressParent);
                                var target = LibraryDependencyTargetUtils.GetFlagString(dependency.LibraryRange.TypeConstraint);

                                properties = new Dictionary<string, string>();

                                properties.Add("ProjectSpecId", entryGuid);
                                properties.Add("Type", "Dependency");
                                properties.Add("Id", dependency.Name);
                                properties.Add("VersionRange", dependency.LibraryRange.VersionRange.ToNormalizedString());
                                properties.Add("IncludeFlags", includeFlags);
                                properties.Add("SuppressParent", suppressParent);
                                properties.Add("Target", target);

                                entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));
                            }
                        }
                        else if (dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference)
                        {
                            var suppressParent = LibraryIncludeFlagUtils.GetFlagString(dependency.SuppressParent);

                            properties = new Dictionary<string, string>();

                            properties.Add("ProjectSpecId", entryGuid);
                            properties.Add("Type", "FrameworkAssembly");
                            properties.Add("Id", dependency.Name);
                            properties.Add("VersionRange", dependency.LibraryRange.VersionRange.ToNormalizedString());
                            properties.Add("SuppressParent", suppressParent);

                            entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));
                        }
                    }
                }
            }

            RestoreItemsFromProjectJson = entries.ToArray();

            return true;
        }

        private PackageSpec GetPackageSpec(string msbuildProjectPath)
        {
            PackageSpec result = null;
            var directory = Path.GetDirectoryName(msbuildProjectPath);
            var projectName = Path.GetFileNameWithoutExtension(msbuildProjectPath);

            if (msbuildProjectPath.EndsWith(XProjUtility.XProjExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"{msbuildProjectPath} : xproj is not supported.");
            }

            // Allow project.json or projectName.project.json
            var path = ProjectJsonPathUtilities.GetProjectConfigPath(directory, projectName);

            if (File.Exists(path))
            {
                result = JsonPackageSpecReader.GetPackageSpec(projectName, path);
            }

            return result;
        }
    }
}
