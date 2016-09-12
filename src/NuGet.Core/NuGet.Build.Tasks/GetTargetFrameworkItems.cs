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
    public class GetTargetFrameworkItems : Task
    {
        [Required]
        public string TargetFrameworks { get; set; }

        /// <summary>
        /// Output items
        /// </summary>
        [Output]
        public ITaskItem[] TargetFrameworksOutput { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);
            log.LogDebug($"(in) TargetFrameworks '{TargetFrameworks}'");

            var items = new List<ITaskItem>();

            var tfmStrings = TargetFrameworks.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            var seen = new HashSet<NuGetFramework>();

            foreach (var tfmString in tfmStrings)
            {
                var framework = NuGetFramework.Parse(tfmString);

                // Ignore duplicates
                if (seen.Add(framework))
                {
                    items.Add(new TaskItem(tfmString));
                }
            }

            TargetFrameworksOutput = items.ToArray();

            return true;
        }
    }
}
