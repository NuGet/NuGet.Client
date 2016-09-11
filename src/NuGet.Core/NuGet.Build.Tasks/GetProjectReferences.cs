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
    public class GetProjectReferences : Task
    {
        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string ProjectUniqueName { get; set; }

        [Required]
        public ITaskItem[] ProjectReferences { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] RestoreGraphItems { get; set; }

        public override bool Execute()
        {
            var entries = new List<ITaskItem>();

            foreach (var project in ProjectReferences)
            {
                var referencePath = Path.GetFullPath(project.ItemSpec);

                var properties = new Dictionary<string, string>();
                properties.Add("ProjectUniqueName", ProjectUniqueName);
                properties.Add("Type", "ProjectReference");
                properties.Add("ProjectPath", referencePath);
                properties.Add("ProjectReferenceUniqueName", referencePath);

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
