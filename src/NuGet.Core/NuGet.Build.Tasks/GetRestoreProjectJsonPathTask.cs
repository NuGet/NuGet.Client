using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Common;

namespace NuGet.Build.Tasks
{
    public class GetRestoreProjectJsonPathTask : Task
    {
        /// <summary>
        /// Full path to the msbuild project.
        /// </summary>
        [Required]
        public string ProjectPath { get; set; }

        /// <summary>
        /// Output path to project.json if it exists.
        /// </summary>
        [Output]
        public string ProjectJsonPath { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);
            log.LogDebug($"(in) ProjectPath '{ProjectPath}'");

            var directory = Path.GetDirectoryName(ProjectPath);
            var projectName = Path.GetFileNameWithoutExtension(ProjectPath);

            // Allow project.json or projectName.project.json
            var path = ProjectJsonPathUtilities.GetProjectConfigPath(directory, projectName);

            if (File.Exists(path))
            {
                ProjectJsonPath = path;
            }

            log.LogDebug($"(out) ProjectJsonPath '{ProjectJsonPath}'");

            return true;
        }
    }
}