using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.ProjectModel;

namespace NuGet.Build.Tasks
{
    public class GetRestoreDotnetCliToolsTask : Task
    {
        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string ProjectPath { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] RestoreGraphItems { get; set; }

        /// <summary>
        /// Tool runtime framework where this will be executed.
        /// </summary>
        [Required]
        public string ToolFramework { get; set; }

        /// <summary>
        /// NuGet sources, ; delimited
        /// </summary>
        public string RestoreSources { get; set; }

        /// <summary>
        /// NuGet fallback folders
        /// </summary>
        public string RestoreFallbackFolders { get; set; }

        /// <summary>
        /// User packages folder
        /// </summary>
        public string RestorePackagesPath { get; set; }

        [Required]
        public ITaskItem[] DotnetCliToolReferences { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);
            log.LogDebug($"(in) ProjectPath '{ProjectPath}'");
            log.LogDebug($"(in) DotnetCliToolReferences '{string.Join(";", DotnetCliToolReferences.Select(p => p.ItemSpec))}'");

            var entries = new List<ITaskItem>();

            foreach (var msbuildItem in DotnetCliToolReferences)
            {
                if (string.IsNullOrEmpty(msbuildItem.ItemSpec))
                {
                    throw new InvalidDataException($"Invalid DotnetCliToolReference in {ProjectPath}");
                }

                var uniqueName = $"{msbuildItem.ItemSpec}-{Guid.NewGuid().ToString()}";

                // Create top level project
                var properties = new Dictionary<string, string>();
                properties.Add("ProjectUniqueName", uniqueName);
                properties.Add("Type", "ProjectSpec");
                properties.Add("ProjectPath", ProjectPath);
                properties.Add("ProjectName", $"DotnetCliToolReference-{msbuildItem.ItemSpec}");
                BuildTasksUtility.AddPropertyIfExists(properties, "Sources", RestoreSources);
                BuildTasksUtility.AddPropertyIfExists(properties, "FallbackFolders", RestoreFallbackFolders);
                BuildTasksUtility.AddPropertyIfExists(properties, "PackagesPath", RestorePackagesPath);
                properties.Add("TargetFrameworks", ToolFramework);
                properties.Add("ProjectStyle", ProjectStyle.DotnetCliTool.ToString());
                BuildTasksUtility.CopyPropertyIfExists(msbuildItem, properties, "Version");

                entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));

                // Add reference to package
                var packageProperties = new Dictionary<string, string>();
                packageProperties.Add("ProjectUniqueName", uniqueName);
                packageProperties.Add("Type", "Dependency");
                packageProperties.Add("Id", msbuildItem.ItemSpec);
                BuildTasksUtility.CopyPropertyIfExists(msbuildItem, packageProperties, "Version", "VersionRange");
                packageProperties.Add("TargetFrameworks", ToolFramework);

                entries.Add(new TaskItem(Guid.NewGuid().ToString(), packageProperties));

                // Add restore spec to ensure this is executed during restore
                var restoreProperties = new Dictionary<string, string>();
                restoreProperties.Add("ProjectUniqueName", uniqueName);
                restoreProperties.Add("Type", "RestoreSpec");

                entries.Add(new TaskItem(Guid.NewGuid().ToString(), restoreProperties));
            }

            RestoreGraphItems = entries.ToArray();

            return true;
        }
    }
}