using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace NuGet.Build.Tasks
{
    public class GetRestorePackageReferencesTask : Task
    {
        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string ProjectUniqueName { get; set; }

        [Required]
        public ITaskItem[] PackageReferences { get; set; }

        /// <summary>
        /// Target frameworks to apply this for. If empty this applies to all.
        /// </summary>
        public string TargetFrameworks { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] RestoreGraphItems { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);
            log.LogDebug($"(in) ProjectUniqueName '{ProjectUniqueName}'");
            log.LogDebug($"(in) TargetFrameworks '{TargetFrameworks}'");
            log.LogDebug($"(in) PackageReferences '{string.Join(";", PackageReferences.Select(p => p.ItemSpec))}'");

            var entries = new List<ITaskItem>();

            foreach (var msbuildItem in PackageReferences)
            {
                var properties = new Dictionary<string, string>();
                properties.Add("ProjectUniqueName", ProjectUniqueName);
                properties.Add("Type", "Dependency");
                properties.Add("Id", msbuildItem.ItemSpec);
                BuildTasksUtility.CopyPropertyIfExists(msbuildItem, properties, "Version", "VersionRange");

                if (!string.IsNullOrEmpty(TargetFrameworks))
                {
                    properties.Add("TargetFrameworks", TargetFrameworks);
                }

                BuildTasksUtility.CopyPropertyIfExists(msbuildItem, properties, "IncludeAssets");
                BuildTasksUtility.CopyPropertyIfExists(msbuildItem, properties, "ExcludeAssets");
                BuildTasksUtility.CopyPropertyIfExists(msbuildItem, properties, "PrivateAssets");

                entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));
            }

            RestoreGraphItems = entries.ToArray();

            return true;
        }
    }
}