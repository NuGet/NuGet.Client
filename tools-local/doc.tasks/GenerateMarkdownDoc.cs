using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NuGetTasks
{
    public class GenerateMarkdownDoc : Task
    {
        public ITaskItem[] ProjectFiles { get; set; }
        public ITaskItem OutputFile { get; set; }

        public override bool Execute()
        {

            var outputFilePath = OutputFile.GetMetadata("Identity");

            var dirname = Path.GetDirectoryName(outputFilePath);
            Directory.CreateDirectory(dirname);
            Log.LogMessage(MessageImportance.Low, $"Created directories: {dirname}");
            

            Log.LogMessage(MessageImportance.Low, $"Creating documentation: {outputFilePath}");
            using (var file = new StreamWriter(outputFilePath))
            {
                file.WriteLine("## NuGet Project Overview\n\n");

                for (int i = 0; i < ProjectFiles.Length; i++)
                {
                    var project = ProjectFiles[i];
                    var desc = GetDescriptions(project.GetMetadata("Identity"));

                    file.WriteLine($"- Project: `{project.ItemSpec}` {desc}");
                }
            }
            Log.LogMessage(MessageImportance.Low, "Documentation complete");

            return true;
        }

        private string GetDescriptions(string projectFilePath)
        {
            var xpathDoc = new XPathDocument(projectFilePath);

            XPathNavigator nav = xpathDoc.CreateNavigator();
            XPathExpression expr = nav.Compile("/Project/PropertyGroup/Description");

            XPathNodeIterator iter = nav.Select(expr);
            
            while (iter.MoveNext())
            {
                return iter.Current.Value;
            }

            return string.Empty;
        }
    }
}
