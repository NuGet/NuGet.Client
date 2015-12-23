using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.LibraryModel;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.MSBuildTasks
{
    public class ProjectReferencesTask : Task
    {
        [Required]
        public ITaskItem ProjectFile { get; set; }

        [Output]
        public ITaskItem[] ProjectClosureOutput { get; set; }

        public override bool Execute()
        {
            var filePath = ProjectFile.ToString();
            var output = XProjUtility.GetProjectReferences(filePath);

            ProjectClosureOutput = output.Select(name => new TaskItem(name)).ToArray();

            return true;
        }
    }
}
