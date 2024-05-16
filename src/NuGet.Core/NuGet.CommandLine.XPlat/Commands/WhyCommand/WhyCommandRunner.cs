// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat.WhyCommandUtility;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    internal static class WhyCommandRunner
    {
        private const string ProjectName = "MSBuildProjectName";
        private const string ProjectAssetsFile = "ProjectAssetsFile";

        /// <summary>
        /// Executes the 'why' command.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments for the 'why' command.</param>
        public static int ExecuteCommand(WhyCommandArgs whyCommandArgs)
        {
            try
            {
                ValidatePathArgument(whyCommandArgs.Path);
                ValidatePackageArgument(whyCommandArgs.Package);
            }
            catch (ArgumentException ex)
            {
                whyCommandArgs.Logger.LogError(ex.Message);
                return ExitCodes.InvalidArguments;
            }

            string targetPackage = whyCommandArgs.Package;

            IEnumerable<string> projectPaths = Path.GetExtension(whyCommandArgs.Path).Equals(".sln")
                                                    ? MSBuildAPIUtility.GetProjectsFromSolution(whyCommandArgs.Path).Where(f => File.Exists(f))
                                                    : [whyCommandArgs.Path];

            foreach (var projectPath in projectPaths)
            {
                Project project = MSBuildAPIUtility.GetProject(projectPath);
                LockFile? assetsFile = GetProjectAssetsFile(project, whyCommandArgs.Logger);

                if (assetsFile != null)
                {
                    Dictionary<string, List<DependencyNode>?>? dependencyGraphPerFramework = DependencyGraphFinder.GetAllDependencyGraphs(whyCommandArgs, assetsFile, project.DirectoryPath);

                    if (dependencyGraphPerFramework != null)
                    {
                        whyCommandArgs.Logger.LogMinimal(
                            string.Format(
                                Strings.WhyCommand_Message_DependencyGraphsFoundInProject,
                                project.GetPropertyValue(ProjectName),
                                targetPackage));

                        DependencyGraphPrinter.PrintAllDependencyGraphs(dependencyGraphPerFramework, targetPackage, whyCommandArgs.Logger);
                    }
                    else
                    {
                        whyCommandArgs.Logger.LogMinimal(
                            string.Format(
                                Strings.WhyCommand_Message_NoDependencyGraphsFoundInProject,
                                project.GetPropertyValue(ProjectName),
                                targetPackage));
                    }
                }

                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }

            return ExitCodes.Success;
        }

        private static void ValidatePathArgument(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    "PROJECT|SOLUTION"));
            }

            if (!File.Exists(path)
                || (!path.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_PathIsMissingOrInvalid,
                    path));
            }
        }

        private static void ValidatePackageArgument(string package)
        {
            if (string.IsNullOrEmpty(package))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    "PACKAGE"));
            }
        }

        /// <summary>
        /// Validates and returns the assets file for the given project.
        /// </summary>
        /// <param name="project">Evaluated MSBuild project</param>
        /// <param name="logger">Logger for the 'why' command</param>
        /// <returns>Assets file for the given project. Returns null if there was any issue finding or parsing the assets file.</returns>
        private static LockFile? GetProjectAssetsFile(Project project, ILoggerWithColor logger)
        {
            if (!MSBuildAPIUtility.IsPackageReferenceProject(project))
            {
                logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_NotPRProject,
                        project.FullPath));

                return null;
            }

            string assetsPath = project.GetPropertyValue(ProjectAssetsFile);

            if (!File.Exists(assetsPath))
            {
                logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_AssetsFileNotFound,
                        project.FullPath));

                return null;
            }

            var lockFileFormat = new LockFileFormat();
            LockFile assetsFile = lockFileFormat.Read(assetsPath);

            // assets file validation
            if (assetsFile.PackageSpec == null
                || assetsFile.Targets == null
                || assetsFile.Targets.Count == 0)
            {
                logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.WhyCommand_Error_InvalidAssetsFile,
                        assetsFile.Path,
                        project.FullPath));

                return null;
            }

            return assetsFile;
        }
    }
}
