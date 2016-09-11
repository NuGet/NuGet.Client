using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Commands;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Generate dg file output.
    /// </summary>
    public class WriteRestoreGraphTask : Task
    {
        /// <summary>
        /// DG file entries
        /// </summary>
        [Required]
        public ITaskItem[] RestoreGraphItems { get; set; }

        [Required]
        public string RestoreGraphOutputPath { get; set; }

        public override bool Execute()
        {
            if (RestoreGraphItems.Length < 1)
            {
                Log.LogWarning("Unable to find a project to restore!");
                return true;
            }

            var log = new MSBuildLogger(Log);

            // Convert to the internal wrapper
            var wrappedItems = RestoreGraphItems.Select(GetMSBuildItem);

            // Log the graph input
            MSBuildRestoreUtility.Dump(wrappedItems, log);

            // Create file
            var dgFile = MSBuildRestoreUtility.GetDependencySpec(wrappedItems);

            var fileInfo = new FileInfo(RestoreGraphOutputPath);
            fileInfo.Directory.Create();

            // Save file
            log.LogMinimal($"Writing {fileInfo.FullName}");

            dgFile.Save(fileInfo.FullName);

            return true;
        }

        /// <summary>
        /// Convert empty strings to null
        /// </summary>
        private static string GetNullForEmpty(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static MSBuildTaskItem GetMSBuildItem(ITaskItem item)
        {
            return new MSBuildTaskItem(item);
        }
    }
}