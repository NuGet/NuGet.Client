using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace NuGet.Common
{
    /// <summary>
    /// Represents the solution loaded from a sln file. We use the internal class
    /// Microsoft.Build.Construction.SolutionParser to parse sln files.
    /// </summary>
    internal class Solution
    {
        public List<ProjectInSolution> Projects { get; private set; }

        public Solution(string solutionFileName, string msbuildPath)
        {
            if (string.IsNullOrEmpty(msbuildPath))
            {
                throw new ArgumentNullException(nameof(msbuildPath));
            }

            Assembly msbuildAssembly = Assembly.LoadFile(
                Path.Combine(msbuildPath, "Microsoft.Build.dll"));
            switch (msbuildAssembly.GetName().Version.Major)
            {
                case 4:
                case 12:
                    LoadSolutionWithMsbuild4or12(msbuildAssembly, solutionFileName);
                    break;

                case 14:
                    LoadSolutionWithMsbuild14(msbuildAssembly, solutionFileName);
                    break;

                default:
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.InvariantCulture,
                        LocalizedResourceManager.GetString(nameof(NuGet.CommandLine.NuGetResources.Error_UnsupportedMsbuild)),
                        msbuildAssembly.FullName));
            }
        }

        // Load the solution file with msbuild 4 or msbuild 12. In this case,
        // the internal class SolutionParser is used to parse the solution file
        private void LoadSolutionWithMsbuild4or12(Assembly msbuildAssembly, string solutionFileName)
        {
            Type solutionParserType = msbuildAssembly.GetType(
                "Microsoft.Build.Construction.SolutionParser",
                throwOnError: true);
            PropertyInfo solutionReaderProperty = solutionParserType.GetProperty(
                "SolutionReader",
                BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo parseSolutionMethod = solutionParserType.GetMethod(
                "ParseSolution",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo projectsProperty = solutionParserType.GetProperty(
                "Projects",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var solutionParser = solutionParserType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null).Invoke(null);
            using (var streamReader = new StreamReader(solutionFileName))
            {
                solutionReaderProperty.SetValue(solutionParser, streamReader, index: null);
                try
                {
                    parseSolutionMethod.Invoke(solutionParser, parameters: null);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException ?? ex;
                }
            }

            // load projects
            Type projectInSolutionType = msbuildAssembly.GetType(
                "Microsoft.Build.Construction.ProjectInSolution",
                throwOnError: true);
            PropertyInfo relativePathProperty = projectInSolutionType.GetProperty(
                "RelativePath",
                BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo projectTypeProperty = projectInSolutionType.GetProperty(
                "ProjectType",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var projects = new List<ProjectInSolution>();
            foreach (var proj in (object[])projectsProperty.GetValue(solutionParser, index: null))
            {
                string projectType = projectTypeProperty.GetValue(proj, index: null).ToString();
                var isSolutionFolder = projectType.Equals("SolutionFolder", StringComparison.OrdinalIgnoreCase);
                var relativePath = (string)relativePathProperty.GetValue(proj, index: null);
                projects.Add(new ProjectInSolution(relativePath, isSolutionFolder));
            }
            this.Projects = projects;
        }

        // Load the solution file using the public class SolutionFile in msbuild 14
        private void LoadSolutionWithMsbuild14(Assembly msbuildAssembly, string solutionFileName)
        {
            var solutionFileType = msbuildAssembly.GetType("Microsoft.Build.Construction.SolutionFile");
            var parseMethod = solutionFileType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public);
            dynamic solutionFile = parseMethod.Invoke(null, new object[] { solutionFileName });

            // load projects
            var projects = new List<ProjectInSolution>();
            foreach (dynamic project in solutionFile.ProjectsInOrder)
            {
                string projectType = project.ProjectType.ToString();
                var isSolutionFolder = projectType.Equals("SolutionFolder", StringComparison.OrdinalIgnoreCase);
                string relativePath = project.RelativePath;
                projects.Add(new ProjectInSolution(relativePath, isSolutionFolder));
            }
            this.Projects = projects;
        }

        private static Type GetSolutionParserType(Assembly msbuildAssembly)
        {
            var solutionParserType = msbuildAssembly.GetType("Microsoft.Build.Construction.SolutionParser");

            if (solutionParserType == null)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString("Error_CannotLoadTypeSolutionParser"));
            }

            return solutionParserType;
        }
    }
}