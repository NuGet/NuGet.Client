// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Xml.XPath;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NuGetTasks
{
    public class GenerateMarkdownDoc : Task
    {
        /// <summary>
        /// Project files to process
        /// </summary>
        public ITaskItem[] ProjectFiles { get; set; }
        /// <summary>
        /// Filepath to the generated documentation
        /// </summary>
        public ITaskItem OutputFile { get; set; }
        /// <summary>
        /// Repository Url to create links to the file
        /// </summary>
        public string GitHubRepositoryUrl { get; set; }
        /// <summary>
        /// Repository path on disk to relativize paths
        /// </summary>
        public string RepositoryRoot { get; set; }
        /// <summary>
        /// Title for the document
        /// </summary>
        public string Title { get; set; } = "NuGet Project Overview";

        public override bool Execute()
        {
            var outputFilePath = OutputFile.GetMetadata("FullPath");

            var dirname = Path.GetDirectoryName(outputFilePath);
            Directory.CreateDirectory(dirname);
            Log.LogMessage(MessageImportance.Low, $"Created directories: {dirname}");

            Log.LogMessage(MessageImportance.Low, $"Creating documentation: {outputFilePath}");
            using (var file = new StreamWriter(outputFilePath))
            {
                file.WriteLine("---");
                file.WriteLine($"date-generated: {DateTime.Now:s}");
                file.WriteLine("---\n");

                file.WriteLine($"# {Title}\n\n");

                for (int i = 0; i < ProjectFiles.Length; i++)
                {
                    var projectPath = ProjectFiles[i].GetMetadata("FullPath");
                    var desc = GetDescriptions(projectPath);

                    file.WriteLine($"- [`{RelativizePath(projectPath)}`]({GenerateGitHubLink(projectPath)}): {desc}");
                }
            }
            Log.LogMessage(MessageImportance.Low, "Documentation complete");

            return true;
        }

        private string GetDescriptions(string projectFilePath)
        {
            XPathDocument xpathDoc = new XPathDocument(projectFilePath);
            XPathNavigator nav = xpathDoc.CreateNavigator();
            XPathExpression expr = nav.Compile("/Project/PropertyGroup/Description");

            XPathNodeIterator iter = nav.Select(expr);
            
            while (iter.MoveNext())
            {
                return iter.Current.Value;
            }

            return string.Empty;
        }

        private string RelativizePath(string projectFilePath)
        {
            return projectFilePath.Replace(RepositoryRoot, string.Empty);
        }

        private string GenerateGitHubLink(string projectFilePath)
        {
            var relativePath = RelativizePath(projectFilePath).Replace("\\", "/");

            return $"{GitHubRepositoryUrl}/tree/dev/{relativePath}";
        }
    }
}
