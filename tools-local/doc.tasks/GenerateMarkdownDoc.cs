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
        /// Project files item group
        /// </summary>
        public ITaskItem[] ProductProjects { get; set; }
        /// <summary>
        /// Test project files item group
        /// </summary>
        public ITaskItem[] TestProjects { get; set; }
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
        public string Title { get; set; } = "NuGet Client Projects Overview";
        /// <summary>
        /// Sub-title for projects list
        /// </summary>
        public string ProjectsSubtitle { get; set; } = "Projects";
        /// <summary>
        /// Sub-title for test projects list
        /// </summary>
        public string TestProjectsSubtitle { get; set; } = "Test Projects";
        /// <summary>
        /// FullPath MSBuild item metadata
        /// </summary>
        private static readonly string MetaFullPath = "FullPath";

        public override bool Execute()
        {
            var outputFilePath = OutputFile.GetMetadata(MetaFullPath);

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
                file.WriteLine("Below is a list of all source code projects for NuGet libraries and supported NuGet clients\n");

                file.WriteLine($"\n\n## {ProjectsSubtitle}\n");
                file.WriteLine("All shipped NuGet libraries and clients lives in `src/` folder.\n");
                file.WriteLine($"Projects count: {ProductProjects.Length}\n");
                SortAndWriteDescriptions(ProductProjects, file);

                file.WriteLine($"\n\n## {TestProjectsSubtitle}\n");
                file.WriteLine("Most production assemblies has an associated test project, whose name ends with `.Test`.\n");
                file.WriteLine($"Test Projects count: {TestProjects.Length}\n");
                SortAndWriteDescriptions(TestProjects, file);
            }
            Log.LogMessage(MessageImportance.Low, "Documentation complete");

            return true;
        }

        private void SortAndWriteDescriptions(ITaskItem[] projects, StreamWriter file)
        {
            Array.Sort(projects, (a, b) => a.GetMetadata(MetaFullPath).CompareTo(b.GetMetadata(MetaFullPath)));

            for (int i = 0; i < projects.Length; i++)
            {
                var projectPath = projects[i].GetMetadata(MetaFullPath);
                var desc = GetDescriptions(projectPath);

                file.WriteLine($"- [`{RelativizePath(projectPath)}`]({GenerateRelativeLink(projectPath)}): {desc}");
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
