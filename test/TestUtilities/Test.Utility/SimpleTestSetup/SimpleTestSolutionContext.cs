// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Solution
    /// </summary>
    public class SimpleTestSolutionContext
    {
        public SimpleTestSolutionContext(string solutionRoot, params SimpleTestProjectContext[] projects)
        {
            SolutionPath = Path.Combine(solutionRoot, "solution.sln");

            Projects.AddRange(projects);
        }

        /// <summary>
        /// Full path
        /// </summary>
        public string SolutionPath { get; set; }

        /// <summary>
        /// Projects
        /// </summary>
        public List<SimpleTestProjectContext> Projects { get; set; } = new List<SimpleTestProjectContext>();

        /// <summary>
        /// Guid
        /// </summary>
        public Guid SolutionGuid { get; set; } = Guid.NewGuid();

        public void Save()
        {
            Save(SolutionPath);
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            File.WriteAllText(path, GetContent().ToString());
        }

        public StringBuilder GetContent()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            sb.AppendLine("# Visual Studio 2012");

            foreach (var project in Projects)
            {
                sb.AppendLine("Project(\"{" + SolutionGuid.ToString() + "}" + $"\") = \"{project.ProjectName}\", " + "\"" + project.ProjectPath +"\", \"{" + project.ProjectGuid +"}\"");
                sb.AppendLine("EndProject");
            }

            sb.AppendLine("Global");
            sb.AppendLine("EndGlobal");

            return sb;
        }

        /// <summary>
        /// Create an entire solution and projects, this will adjust the paths as needed
        /// </summary>
        public void Create(string solutionFolder)
        {
            Save();

            foreach (var project in GetAllProjects())
            {
                // only save after updating everything
                project.Save();
            }
        }

        /// <summary>
        /// All projects used in the solution
        /// </summary>
        public HashSet<SimpleTestProjectContext> GetAllProjects()
        {
            var projects = new HashSet<SimpleTestProjectContext>();
            var toWalk = new Stack<SimpleTestProjectContext>(Projects);

            while (toWalk.Count > 0)
            {
                var project = toWalk.Pop();

                if (projects.Add(project))
                {
                    foreach (var dep in project.AllProjectReferences)
                    {
                        toWalk.Push(dep);
                    }
                }
            }

            return projects;
        }

        /// <summary>
        /// All packages used in the solution
        /// </summary>
        public HashSet<SimpleTestPackageContext> GetAllPackages()
        {
            var packages = new HashSet<SimpleTestPackageContext>();
            var toWalk = new Stack<SimpleTestPackageContext>(GetAllProjects().SelectMany(p => p.AllPackageDependencies));

            while (toWalk.Count > 0)
            {
                var package = toWalk.Pop();

                if (packages.Add(package))
                {
                    foreach (var dep in package.Dependencies)
                    {
                        toWalk.Push(dep);
                    }
                }
            }

            return packages;
        }
    }
}
