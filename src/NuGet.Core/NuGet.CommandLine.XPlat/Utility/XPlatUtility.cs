// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Globalization;
using System;
using System.IO;
using System.Linq;

#if IS_CORECLR
using System.Runtime.InteropServices;
#endif

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal static class XPlatUtility
    {
        public const string HelpOption = "-h|--help";

        /// <summary>
        /// Note that the .NET CLI itself has parameter parsing which limits the values that will be passed here by the
        /// user. In other words, the default case should only be hit with <c>m</c> or <c>minimal</c> but we use <see cref="Common.LogLevel.Minimal"/>
        /// as the default case to avoid errors.
        /// </summary>
        public static LogLevel MSBuildVerbosityToNuGetLogLevel(string verbosity)
        {
            switch (verbosity?.ToUpperInvariant())
            {
                case "Q":
                case "QUIET":
                    return LogLevel.Warning;
                case "N":
                case "NORMAL":
                    return LogLevel.Information;
                case "D":
                case "DETAILED":
                case "DIAG":
                case "DIAGNOSTIC":
                    return LogLevel.Debug;
                default:
                    return LogLevel.Minimal;
            }
        }

        public static ISettings GetSettingsForCurrentWorkingDirectory()
        {
            return Settings.LoadDefaultSettings(
                Directory.GetCurrentDirectory(),
                configFileName: null,
                machineWideSettings: new XPlatMachineWideSetting());
        }

        public static void ConfigureProtocol()
        {
            // Set connection limit
            NetworkProtocolUtility.SetConnectionLimit();

            // Set user agent string used for network calls
            SetUserAgent();
        }

        public static void SetUserAgent()
        {
#if IS_CORECLR
            UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet xplat")
                .WithOSDescription(RuntimeInformation.OSDescription));
#else
            UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet xplat"));
#endif
        }

        internal static ISettings ProcessConfigFile(string configFile)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return GetSettingsForCurrentWorkingDirectory();
            }

            var configFileFullPath = Path.GetFullPath(configFile);
            var directory = Path.GetDirectoryName(configFileFullPath);
            var configFileName = Path.GetFileName(configFileFullPath);
            return Settings.LoadDefaultSettings(
                directory,
                configFileName,
                machineWideSettings: new XPlatMachineWideSetting());
        }

        /// <summary>
        /// Get the project or solution file from the given directory.
        /// </summary>
        /// <param name="directory">A directory with exactly one project or solution file.</param>
        /// <returns>A single project or solution file.</returns>
        /// <exception cref="ArgumentException">Throws an exception if the directory has none or multiple project/solution files.</exception>
        internal static string ADVAY_GetProjectOrSolutionFileFromDirectory(string directory)
        {
            var topLevelFiles = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
            var projectOrSolutionFiles = topLevelFiles
                                            .Where(file => IsSolutionFile(file) || IsProjectFile(file))
                                            .ToArray();

            if (projectOrSolutionFiles.Length > 1)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_MultipleProjectOrSolutionFilesInDirectory,
                        directory));
            }
            else if (projectOrSolutionFiles.Length == 0)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_NoProjectOrSolutionFilesInDirectory,
                        directory));
            }

            return projectOrSolutionFiles[0];
        }

        /// <summary>
        /// Get the project or solution file from the given directory.
        /// </summary>
        /// <param name="directory">A directory with exactly one project or solution file.</param>
        /// <returns>A single project or solution file.</returns>
        /// <exception cref="ArgumentException">Throws an exception if the directory has none or multiple project/solution files.</exception>
        internal static string JP_GetProjectOrSolutionFileFromDirectory(string directory)
        {
            var topLevelFiles = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);

            string? candidateFile = null;
            foreach (string file in topLevelFiles)
            {
                if (IsSolutionFile(file) || IsProjectFile(file))
                {
                    if (candidateFile == null)
                    {
                        candidateFile = file;
                    }
                    else
                    {
                        throw new ArgumentException(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Error_MultipleProjectOrSolutionFilesInDirectory,
                                directory));
                    }
                }
            }

            if (candidateFile == null)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_NoProjectOrSolutionFilesInDirectory,
                        directory));
            }

            return candidateFile;
        }

        /// <summary>
        /// Get the project or solution file from the given directory.
        /// </summary>
        /// <param name="directory">A directory with exactly one project or solution file.</param>
        /// <returns>A single project or solution file.</returns>
        /// <exception cref="ArgumentException">Throws an exception if the directory has none or multiple project/solution files.</exception>
        internal static string JP_LINQ_TAKE_GetProjectOrSolutionFileFromDirectory(string directory)
        {
            var projectOrSolutionFiles = Directory
                                            .GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                                            .Where(file => IsSolutionFile(file) || IsProjectFile(file));

            if (!projectOrSolutionFiles.Any())
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_NoProjectOrSolutionFilesInDirectory,
                        directory));
            }

            if (projectOrSolutionFiles.Take(2).Count() > 1)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_MultipleProjectOrSolutionFilesInDirectory,
                        directory));
            }

            return projectOrSolutionFiles.Single();
        }
        // For readability, I prefer just iterating directly over the files instead of using LINQ's Take like we do here.

        /// <summary>
        /// Get the project or solution file from the given directory.
        /// </summary>
        /// <param name="directory">A directory with exactly one project or solution file.</param>
        /// <returns>A single project or solution file.</returns>
        /// <exception cref="ArgumentException">Throws an exception if the directory has none or multiple project/solution files.</exception>
        internal static string ADVAY_AVOIDARRAY_GetProjectOrSolutionFileFromDirectory(string directory)
        {
            var projectOrSolutionFiles = Directory
                                            .GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                                            .Where(file => IsSolutionFile(file) || IsProjectFile(file));

            string? projectOrSolutionFile = null;
            foreach (var file in projectOrSolutionFiles)
            {
                if (projectOrSolutionFile == null)
                {
                    projectOrSolutionFile = file;
                }
                else
                {
                    throw new ArgumentException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Error_MultipleProjectOrSolutionFilesInDirectory,
                            directory));
                }
            }

            if (projectOrSolutionFile == null)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_NoProjectOrSolutionFilesInDirectory,
                        directory));
            }

            return projectOrSolutionFile;
        }

        /// <summary>
        /// Get the project or solution file from the given directory.
        /// </summary>
        /// <param name="directory">A directory with exactly one project or solution file.</param>
        /// <returns>A single project or solution file.</returns>
        /// <exception cref="ArgumentException">Throws an exception if the directory has none or multiple project/solution files.</exception>
        internal static string JP_LINQ_GetProjectOrSolutionFileFromDirectory(string directory)
        {
            var projectOrSolutionFiles = Directory
                                            .GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                                            .Where(file => IsSolutionFile(file) || IsProjectFile(file));

            string? projectOrSolutionFile;
            try
            {
                projectOrSolutionFile = projectOrSolutionFiles.SingleOrDefault();
            }
            catch (InvalidOperationException)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_MultipleProjectOrSolutionFilesInDirectory,
                        directory));
            }

            if (projectOrSolutionFile == default)
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_NoProjectOrSolutionFilesInDirectory,
                        directory));
            }

            return projectOrSolutionFile;
        }

        /// <summary>
        /// Get the project or solution file from the given directory.
        /// </summary>
        /// <param name="directory">A directory with exactly one project or solution file.</param>
        /// <returns>A single project or solution file.</returns>
        /// <exception cref="ArgumentException">Throws an exception if the directory has none or multiple project/solution files.</exception>
        internal static string GetProjectOrSolutionFileFromDirectory(string directory)
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
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_MultipleProjectOrSolutionFilesInDirectory,
                        directory));
            }

            if (solutionFiles.Length == 1)
            {
                return solutionFiles[0];
            }

            if (projectFiles.Length == 1)
            {
                return projectFiles[0];
            }

            throw new ArgumentException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Error_NoProjectOrSolutionFilesInDirectory,
                    directory));
        }

        /// <summary>
        /// Checks if the given file is a solution file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>Returns true if the given file exists and has a .sln extension.</returns>
        internal static bool IsSolutionFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName))
            {
                var extension = System.IO.Path.GetExtension(fileName);

                return string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Checks if the given file is a project file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>Returns true if the given file exists and has a .*proj extension.</returns>
        internal static bool IsProjectFile(string fileName)
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
