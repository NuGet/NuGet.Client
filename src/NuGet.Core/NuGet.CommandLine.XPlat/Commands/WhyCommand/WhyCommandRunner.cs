// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.Evaluation;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    internal static class WhyCommandRunner
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";

        /// <summary>
        /// Executes the 'why' command.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments for the 'why' command.</param>
        public static int ExecuteCommand(WhyCommandArgs whyCommandArgs)
        {
            bool validArgumentsUsed = whyCommandArgs.ValidatePathArgument()
                                        && whyCommandArgs.ValidatePackageArgument();
            if (!validArgumentsUsed)
            {
                return ExitCodes.InvalidArguments;
            }

            IEnumerable<string>? projectPaths = whyCommandArgs.GetListOfProjectPaths();

            if (projectPaths == null)
            {
                return ExitCodes.InvalidArguments;
            }

            string targetPackage = whyCommandArgs.Package;

            foreach (var projectPath in projectPaths)
            {
                Project project = MSBuildAPIUtility.GetProject(projectPath);

                string usingNetSdk = project.GetPropertyValue("UsingMicrosoftNETSdk");

                if (!string.IsNullOrEmpty(usingNetSdk))
                {
                    LockFile? assetsFile = GetProjectAssetsFile(project, whyCommandArgs.Logger);

                    if (assetsFile != null)
                    {
                        whyCommandArgs.ValidateFrameworksOptionsExistInAssetsFile(assetsFile);

                        Dictionary<string, List<DependencyNode>?>? dependencyGraphPerFramework = DependencyGraphFinder.GetAllDependencyGraphsForTarget(
                            assetsFile,
                            whyCommandArgs.Package,
                            whyCommandArgs.Frameworks);

                        if (dependencyGraphPerFramework != null)
                        {
                            whyCommandArgs.Logger.LogMinimal(
                                string.Format(
                                    Strings.WhyCommand_Message_DependencyGraphsFoundInProject,
                                    assetsFile.PackageSpec.Name,
                                    targetPackage));

                            DependencyGraphPrinter.PrintAllDependencyGraphs(dependencyGraphPerFramework, targetPackage, whyCommandArgs.Logger);
                        }
                        else
                        {
                            whyCommandArgs.Logger.LogMinimal(
                                string.Format(
                                    Strings.WhyCommand_Message_NoDependencyGraphsFoundInProject,
                                    assetsFile.PackageSpec.Name,
                                    targetPackage));
                        }
                    }
                }
                else
                {
                    whyCommandArgs.Logger.LogMinimal(
                            string.Format(
                                Strings.WhyCommand_Message_NonSDKStyleProjectsAreNotSupported,
                                project.GetPropertyValue("MSBuildProjectName")));
                }

                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }

            return ExitCodes.Success;
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
