using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace doc.tasks
{
    public class MarkdownDocTask : Task
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

            using (var file = new StreamWriter(OutputFile.GetMetadata("Identity")))
            {
                for (int i = 0; i < ProjectFiles.Length; i++)
                {
                    var project = ProjectFiles[i];
                    var desc = Descriptions[i];

                    file.WriteLine($"Project: {project.ItemSpec} {desc.GetMetadata("Identity")}");
                }
            }

            return true;
        }
    }
}
