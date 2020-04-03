// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.XPath;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NuGetTasks
{
    public class GenerateMarkdownDoc : Task
    {
        /// <summary>
        /// Filepath to the generated documentation
        /// </summary>
        [Required]
        public ITaskItem OutputFile { get; set; }
        /// <summary>
        /// Repository path on disk to relativize paths
        /// </summary>
        [Required]
        public string RepositoryRoot { get; set; }
        /// <summary>
        /// Title for the document
        /// </summary>
        [Required]
        public string Title { get; set; }
        /// <summary>
        /// A brief description for the whole document
        /// </summary>
        public string Summary { get; set; }
        /// <summary>
        /// Section descriptors, including Title, Summary and projects
        /// </summary>
        [Required]
        public ITaskItem[] Sections { get; set; }

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
                file.WriteLine($"tool: {typeof(GenerateMarkdownDoc).FullName}");
                file.WriteLine("---");

                file.WriteLine($"\n\n# {Title}\n");

                if(!string.IsNullOrEmpty(Summary))
                {
                    file.WriteLine($"{Summary}\n");
                }
                
                for (int j = 0; j < Sections.Length; j++) 
                {
                    string sectionTitle = Sections[j].GetMetadata("Title");
                    if (!string.IsNullOrEmpty(sectionTitle))
                    {
                        file.WriteLine($"## {sectionTitle}\n");
                    }

                    string summary = Sections[j].GetMetadata("Summary");
                    if (!string.IsNullOrEmpty(summary))
                    {
                        file.WriteLine($"{summary}\n");
                    }
                    
                    WriteSectionProjecs(Sections[j], file);

                    if (j + 1 < Sections.Length)
                    {
                        file.WriteLine("\n");
                    }
                }
            }
            Log.LogMessage(MessageImportance.Low, "Documentation complete");

            return true;
        }

        private void WriteSectionProjecs(ITaskItem section, StreamWriter file)
        {
            string[] projectFiles = section.GetMetadata("Projects").Split(';');

            file.WriteLine($"Projects in section: {projectFiles.Length}\n");
                    
            Array.Sort(projectFiles, (a, b) => a.CompareTo(b));

            for (int i = 0; i < projectFiles.Length; i++)
            {
                var projectPath = projectFiles[i];
                var desc = GetDescriptions(projectPath);
                var link = GenerateRelativeLink(projectPath);
                var projectBullet = Path.GetFileName(projectPath);

                file.WriteLine($"- [`{projectBullet}`]({link}): {desc}");
            }
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

        private string GenerateRelativeLink(string projectFilePath)
        {
            var relativePath = RelativizePath(projectFilePath).Replace("\\", "/");

            return $"../{relativePath}";
        }
    }
}
