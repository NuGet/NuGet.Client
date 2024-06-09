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
                    && (IsSolutionFile(fullPath) || IsProjectFile(fullPath))))
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
                projectOrSolutionFile = GetProjectOrSolutionFileFromDirectory(fullPath);

                if (projectOrSolutionFile == null)
                {
                    return null;
                }
            }
            // the path points to a project or solution file
            else
            {
                projectOrSolutionFile = fullPath;
            }

            return IsSolutionFile(projectOrSolutionFile)
                        ? MSBuildAPIUtility.GetProjectsFromSolution(projectOrSolutionFile).Where(f => File.Exists(f))
                        : [projectOrSolutionFile];
        }

        /// <summary>
        /// Get the project or solution file from the given directory.
        /// </summary>
        /// <returns>A single project or solution file. Returns null if the directory has none or multiple project/solution files.</returns>
        private string? GetProjectOrSolutionFileFromDirectory(string directory)
        {
            var topLevelFiles = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);

            var solutionFiles = topLevelFiles
                                    .Where(file => IsSolutionFile(file))
                                    .ToArray();
            var projectFiles = topLevelFiles
                                    .Where(file => IsProjectFile(file))
                                    .ToArray();

            if (solutionFiles.Length + projectFiles.Length > 1)
            {
                Logger.LogError(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.WhyCommand_Error_MultipleProjectOrSolutionFilesInDirectory,
                            directory));
                return null;
            }

            if (solutionFiles.Length == 1)
            {
                return solutionFiles[0];
            }

            if (projectFiles.Length == 1)
            {
                return projectFiles[0];
            }

            Logger.LogError(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_NoProjectOrSolutionFilesInDirectory,
                    directory));
            return null;
        }

        private static bool IsSolutionFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                var extension = System.IO.Path.GetExtension(fileName);

                return string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsProjectFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                var extension = System.IO.Path.GetExtension(fileName);

                var lastFourCharacters = extension.Length >= 4
                                            ? extension.Substring(extension.Length - 4)
                                            : string.Empty;

                return string.Equals(lastFourCharacters, "proj", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
