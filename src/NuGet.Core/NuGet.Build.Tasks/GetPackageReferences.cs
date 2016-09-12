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
using NuGet.Versioning;

namespace NuGet.Build.Tasks
{
    public class GetPackageReferences : Task
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

            foreach (var project in PackageReferences)
            {
                var parts = project.ItemSpec.Split('/');

                if (parts.Length != 2)
                {
                    throw new InvalidDataException(project.ItemSpec);
                }

                var properties = new Dictionary<string, string>();
                properties.Add("ProjectUniqueName", ProjectUniqueName);
                properties.Add("Type", "Dependency");
                properties.Add("Id", parts[0]);
                properties.Add("VersionRange", parts[1]);

                if (!string.IsNullOrEmpty(TargetFrameworks))
                {
                    properties.Add("TargetFrameworks", TargetFrameworks);
                }

                BuildTasksUtility.CopyPropertyIfExists(project, properties, "IncludeAssets");
                BuildTasksUtility.CopyPropertyIfExists(project, properties, "ExcludeAssets");
                BuildTasksUtility.CopyPropertyIfExists(project, properties, "PrivateAssets");

                entries.Add(new TaskItem(Guid.NewGuid().ToString(), properties));
            }

            RestoreGraphItems = entries.ToArray();

            return true;
        }
    }
}
