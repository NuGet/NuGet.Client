using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NuGetTasks
{
    public class GenerateMarkdownDoc : Task
    {
        public ITaskItem[] ProjectFiles { get; set; }
        public ITaskItem[] Descriptions { get; set; }
        public ITaskItem OutputFile { get; set; }

        public override bool Execute()
        {
            if (ProjectFiles.Length != Descriptions.Length)
            {
                throw new ArgumentException("List doesn't match");
            }

            var outputFilePath = OutputFile.GetMetadata("Identity");

            Log.LogMessage(MessageImportance.High, "Creating directories");
            Directory.CreateDirectory(outputFilePath);

            Log.LogMessage(MessageImportance.High, "Creating documentation");
            using (var file = new StreamWriter(outputFilePath))
            {
                file.WriteLine("## NuGet Project Overview\n\n");

                for (int i = 0; i < ProjectFiles.Length; i++)
                {
                    var project = ProjectFiles[i];
                    var desc = Descriptions[i];

                    file.WriteLine($"- Project: {project.ItemSpec} {desc.GetMetadata("Identity")}");
                }
            }
            Log.LogMessage(MessageImportance.High, "Documentation complete");

            return true;
        }
    }
}
