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
        /// Core Unit Test project files item group
        /// </summary>
        public ITaskItem[] CoreUnitTestProjects { get; set; }
        /// <summary>
        /// Core Functional Test project files item group
        /// </summary>
        public ITaskItem[] CoreFuncTestProjects {get; set; }
        /// <summary>
        /// VS Unit Test project files item group
        /// </summary>
        public ITaskItem[] VSUnitTestProjects { get; set;}
        /// <summary>
        /// Filepath to the generated project-overview documentation
        /// </summary>
        public ITaskItem OutputFile { get; set; }
        /// <summary>
        /// Filepath to the generated test-overview documentation
        /// </summary>
        public ITaskItem TestOverviewOutputFile { get; set; }
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
        /// Title for the test-overview document
        /// </summary>
        public string TestProjectTitle { get; set; } = "NuGet Client Test Projects Overview";
        /// <summary>
        /// Sub-title for projects list
        /// </summary>
        public string ProjectsSubtitle { get; set; } = "Projects";
        /// <summary>
        /// Sub-title for test projects list
        /// </summary>
        public string TestProjectsSubtitle { get; set; } = "Test Projects";
        /// <summary>
        /// Sub-title for test projects list
        /// </summary>
        public string CoreUnitTestProjectsSubtitle { get; set; } = "Core Unit Test Projects";
                /// <summary>
        /// Sub-title for test projects list
        /// </summary>
        public string CoreFuncTestProjectsSubtitle { get; set; } = "Core Functional Test Projects";
                /// <summary>
        /// Sub-title for test projects list
        /// </summary>
        public string VSUnitTestProjectsSubtitle { get; set; } = "VS Unit Test Projects";
        /// <summary>
        /// FullPath MSBuild item metadata
        /// </summary>
        private static readonly string MetaFullPath = "FullPath";

        public override bool Execute()
        {
            GenerateProjectOverviewDoc();

            GenerateTestProjectOverviewDoc();
            
            Log.LogMessage(MessageImportance.Low, "Documentations all complete");

            return true;
        }

        private void GenerateProjectOverviewDoc()
        {
            var outputFilePath = OutputFile.GetMetadata(MetaFullPath);


            using (var file = new StreamWriter(outputFilePath))
            {
                file.WriteLine("---");
                file.WriteLine($"date-generated: {DateTime.Now:s}");
                file.WriteLine($"tool: {typeof(GenerateMarkdownDoc).FullName}");
                file.WriteLine("---\n");

                file.WriteLine($"\n\n# {Title}\n\n");

                file.WriteLine($"\n\n## {ProjectsSubtitle}\n\n");
                SortAndWriteDescriptions(ProductProjects, file);

                file.WriteLine($"\n\n## {TestProjectsSubtitle}\n\n");
                SortAndWriteDescriptions(TestProjects, file);
            }
            Log.LogMessage(MessageImportance.Low, "Documentation complete");
        }

        private void GenerateTestProjectOverviewDoc()
        {
            var outputFilePath = TestOverviewOutputFile.GetMetadata(MetaFullPath);

            var dirname = Path.GetDirectoryName(outputFilePath);
            Directory.CreateDirectory(dirname);
            Log.LogMessage(MessageImportance.Low, $"Created directories: {dirname}");

            Log.LogMessage(MessageImportance.Low, $"Creating documentation: {outputFilePath}");
            using (var file = new StreamWriter(outputFilePath))
            {
                file.WriteLine("---");
                file.WriteLine($"date-generated: {DateTime.Now:s}");
                file.WriteLine($"tool: {typeof(GenerateMarkdownDoc).FullName}");
                file.WriteLine("---\n");

                file.WriteLine($"\n\n# {TestProjectTitle}\n\n");

                file.WriteLine($"\n\n## {CoreUnitTestProjectsSubtitle}\n\n");
                SortAndWriteDescriptions(CoreUnitTestProjects, file);

                file.WriteLine($"\n\n## {CoreFuncTestProjectsSubtitle}\n\n");
                SortAndWriteDescriptions(CoreFuncTestProjects, file);

                file.WriteLine($"\n\n## {VSUnitTestProjectsSubtitle}\n\n");
                SortAndWriteDescriptions(VSUnitTestProjects, file);
            }
            Log.LogMessage(MessageImportance.Low, "Documentation complete");
        }

        private void LogCreateDirectoryAndFile(string outputFilePath)
        {
                        var dirname = Path.GetDirectoryName(outputFilePath);
            Directory.CreateDirectory(dirname);
            Log.LogMessage(MessageImportance.Low, $"Created directories: {dirname}");

            Log.LogMessage(MessageImportance.Low, $"Creating documentation: {outputFilePath}");
        }
        private void SortAndWriteDescriptions(ITaskItem[] projects, StreamWriter file)
        {
            Array.Sort(projects, (a, b) => a.GetMetadata(MetaFullPath).CompareTo(b.GetMetadata(MetaFullPath)));

            for (int i = 0; i < projects.Length; i++)
            {
                var projectPath = projects[i].GetMetadata(MetaFullPath);
                var desc = GetDescriptions(projectPath);

                file.WriteLine($"- [`{RelativizePath(projectPath)}`]({GenerateGitHubLink(projectPath)}): {desc}");
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

        private string GenerateGitHubLink(string projectFilePath)
        {
            var relativePath = RelativizePath(projectFilePath).Replace("\\", "/");

            return $"{GitHubRepositoryUrl}/tree/dev/{relativePath}";
        }
    }
}
