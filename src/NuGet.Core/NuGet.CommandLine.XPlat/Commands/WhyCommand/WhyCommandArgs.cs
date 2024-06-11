// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    internal class WhyCommandArgs
    {
        public string Path { get; }
        public string Package { get; }
        public List<string> Frameworks { get; }
        public ILoggerWithColor Logger { get; }

        /// <summary>
        /// A constructor for the arguments of the 'why' command.
        /// </summary>
        /// <param name="path">The path to the solution or project file.</param>
        /// <param name="package">The package for which we show the dependency graphs.</param>
        /// <param name="frameworks">The target framework(s) for which we show the dependency graphs.</param>
        /// <param name="logger"></param>
        public WhyCommandArgs(
            string path,
            string package,
            List<string> frameworks,
            ILoggerWithColor logger)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            Frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates that the input 'PATH' argument is a valid path to a directory, solution file or project file.
        /// </summary>
        public bool ValidatePathArgument()
        {
            if (string.IsNullOrEmpty(Path))
            {
                Logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                        "PROJECT|SOLUTION"));
                return false;
            }

            // Check that the input is a valid path
            string fullPath;
            try
            {
                fullPath = System.IO.Path.GetFullPath(Path);
            }
            catch (ArgumentException)
            {
                Logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.WhyCommand_Error_PathIsMissingOrInvalid,
                        Path));
                return false;
            }

            // Check that the path is a directory, solution file or project file
            if (Directory.Exists(fullPath)
                || (File.Exists(fullPath)
                    && (XPlatUtility.IsSolutionFile(fullPath) || XPlatUtility.IsProjectFile(fullPath))))
            {
                return true;
            }
            else
            {
                Logger.LogError(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_PathIsMissingOrInvalid,
                    Path));
                return false;
            }
        }

        public bool ValidatePackageArgument()
        {
            if (string.IsNullOrEmpty(Package))
            {
                Logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                        "PACKAGE"));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the input frameworks options have corresponding targets in the assets file. Outputs a warning message if a framework does not exist.
        /// </summary>
        public void ValidateFrameworksOptionsExistInAssetsFile(LockFile assetsFile)
        {
            foreach (var frameworkAlias in Frameworks)
            {
                if (assetsFile.GetTarget(frameworkAlias, runtimeIdentifier: null) == null)
                {
                    Logger.LogWarning(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.WhyCommand_Warning_AssetsFileDoesNotContainSpecifiedTarget,
                            assetsFile.Path,
                            assetsFile.PackageSpec.Name,
                            frameworkAlias));
                }
            }
        }

        /// <summary>
        /// Get the list of project paths from the input 'path' argument.
        /// </summary>
        /// <returns>List of project paths. Returns null if path was a directory with none or multiple project/solution files.</returns>
        public IEnumerable<string>? GetListOfProjectPaths()
        {
            string fullPath = System.IO.Path.GetFullPath(Path);

            string? projectOrSolutionFile;

            // the path points to a directory
            if (Directory.Exists(fullPath))
            {
                try
                {
                    projectOrSolutionFile = XPlatUtility.GetProjectOrSolutionFileFromDirectory(fullPath);
                }
                catch (ArgumentException ex)
                {
                    Logger.LogError(ex.Message);
                    return null;
                }
            }
            // the path points to a project or solution file
            else
            {
                projectOrSolutionFile = fullPath;
            }

            return XPlatUtility.IsSolutionFile(projectOrSolutionFile)
                        ? MSBuildAPIUtility.GetProjectsFromSolution(projectOrSolutionFile).Where(f => File.Exists(f))
                        : [projectOrSolutionFile];
        }
    }
}
