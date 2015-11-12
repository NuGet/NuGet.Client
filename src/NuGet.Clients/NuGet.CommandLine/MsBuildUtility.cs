﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using NuGet.Common;

namespace NuGet.CommandLine
{
    public static class MsBuildUtility
    {
        private const string GetProjectReferencesTarget =
            "NuGet.CommandLine.GetProjectsReferencingProjectJsonFiles.target";
        private const string GetProjectReferencesEntryPointTarget =
            "NuGet.CommandLine.GetProjectsReferencingProjectJsonFilesEntryPoint.target";

        private static readonly HashSet<string> _msbuildExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".csproj",
            ".vbproj",
            ".fsproj",
        };

        public static bool IsMsBuildBasedProject(string projectFullPath)
        {
            return _msbuildExtensions.Contains(Path.GetExtension(projectFullPath));
        }

        /// <summary>
        /// Returns the closure of project references for projects specified in <paramref name="projectPaths"/>.
        /// </summary>
        public static Dictionary<string, HashSet<string>> GetProjectReferences(
            string msbuildDirectory,
            string[] projectPaths)
        {
            string msbuildPath = Path.Combine(msbuildDirectory, "msbuild.exe");
            if (!File.Exists(msbuildPath))
            {
                throw new CommandLineException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        NuGetResources.MsBuildDoesNotExistAtPath,
                        msbuildPath));
            }

            var entryPointTargetPath = Path.GetTempFileName();
            var customAfterBuildTargetPath = Path.GetTempFileName();
            var resultsPath = Path.GetTempFileName();

            ExtractResource(GetProjectReferencesEntryPointTarget, entryPointTargetPath);
            ExtractResource(GetProjectReferencesTarget, customAfterBuildTargetPath);

            var argumentBuilder = new StringBuilder(
                "/t:NuGet_GetProjectsReferencingProjectJson " +
                "/nologo /nr:false /v:q " +
                "/p:BuildProjectReferences=false");

            argumentBuilder.Append(" /p:NuGetCustomAfterBuildTargetPath=");
            AppendQuoted(argumentBuilder, customAfterBuildTargetPath);

            argumentBuilder.Append(" /p:ResultsFile=");
            AppendQuoted(argumentBuilder, resultsPath);

            argumentBuilder.Append(" /p:NuGet_ProjectReferenceToResolve=\"");
            for (var i = 0; i < projectPaths.Length; i++)
            {
                argumentBuilder.Append(projectPaths[i])
                    .Append(";");
            }

            argumentBuilder.Append("\" ");
            AppendQuoted(argumentBuilder, entryPointTargetPath);

            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = msbuildPath,
                Arguments = argumentBuilder.ToString(),
                RedirectStandardError = true
            };
            try
            {
                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit(60 * 1000);

                    if (process.ExitCode != 0)
                    {
                        throw new CommandLineException(process.StandardError.ReadToEnd());
                    }
                }

                var lookup = new Dictionary<string, HashSet<string>>(
                    projectPaths.Length,
                    StringComparer.OrdinalIgnoreCase);
                if (File.Exists(resultsPath))
                {
                    HashSet<string> referencedProjects = null;
                    foreach (var line in File.ReadAllLines(resultsPath))
                    {
                        if (line.StartsWith("#:", StringComparison.Ordinal))
                        {
                            // First entry for each project grouping is of the format "#:ProjectPath". 
                            // We'll use this as a delimiter to start a new grouping.
                            referencedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            lookup[line.Substring(2)] = referencedProjects;
                        }
                        else
                        {
                            Debug.Assert(referencedProjects != null);
                            referencedProjects.Add(line);
                        }
                    }
                }

                return lookup;
            }
            finally
            {
                File.Delete(entryPointTargetPath);
                File.Delete(customAfterBuildTargetPath);
                File.Delete(resultsPath);
            }
        }

        public static IEnumerable<string> GetAllProjectFileNames(string solutionFile, string msbuildPath)
        {
            try
            {
                var solution = new Solution(solutionFile, msbuildPath);
                var solutionDirectory = Path.GetDirectoryName(solutionFile);
                return solution.Projects.Where(project => !project.IsSolutionFolder)
                    .Select(project => Path.Combine(solutionDirectory, project.RelativePath));
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("Error_SolutionFileParseError"),
                    solutionFile,
                    ex.Message);

                throw new CommandLineException(message);
            }
        }

        /// <summary>
        /// Gets the version of MSBuild in PATH. 
        /// </summary>
        /// <returns>The version of MSBuild in PATH. Returns null if MSBuild does not exist in PATH.</returns>
        private static Version GetMSBuildVersionInPath()
        {
            // run msbuild to get the version
            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = "msbuild.exe",
                Arguments = "/version",
                RedirectStandardOutput = true
            };

            try
            {
                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit(10 * 1000);

                    if (process.ExitCode == 0)
                    {
                        var output = process.StandardOutput.ReadToEnd();

                        // The output of msbuid /version with MSBuild 14 is:
                        //
                        // Microsoft (R) Build Engine version 14.0.23107.0
                        // Copyright(C) Microsoft Corporation. All rights reserved.
                        //
                        // 14.0.23107.0                            
                        var lines = output.Split(
                            new[] { Environment.NewLine },
                            StringSplitOptions.RemoveEmptyEntries);

                        var versionString = lines.LastOrDefault(
                            line => !string.IsNullOrWhiteSpace(line));
                        Version version;
                        if (Version.TryParse(versionString, out version))
                        {
                            return version;
                        }
                    }
                }
            }
            catch
            {
                // ignore errors                
            }

            return null;
        }

        /// <summary>
        /// Gets the msbuild toolset that matches the given <paramref name="msbuildVersion"/>.
        /// </summary>
        /// <param name="msbuildVersion">The msbuild version. Can be null.</param>
        /// <param name="installedToolsets">List of installed toolsets, 
        /// ordered by ToolsVersion, from highest to lowest.</param>
        /// <returns>The matching toolset.</returns>
        public static Toolset SelectMsbuildToolset(
            Version msbuildVersion,
            IList<Toolset> installedToolsets)
        {
            Toolset selectedToolset;
            if (msbuildVersion == null)
            {
                // MSBuild does not exist in PATH. In this case, the highest installed version is used
                selectedToolset = installedToolsets.FirstOrDefault();
            }
            else
            {
                // Search by major & minor version
                selectedToolset = installedToolsets.FirstOrDefault(
                    toolset =>
                    {
                        var v = new Version(toolset.ToolsVersion);
                        return v.Major == msbuildVersion.Major && v.Minor == v.Minor;
                    });

                if (selectedToolset == null)
                {
                    // no match found. Now search by major only
                    selectedToolset = installedToolsets.FirstOrDefault(
                        toolset =>
                        {
                            var v = new Version(toolset.ToolsVersion);
                            return v.Major == msbuildVersion.Major;
                        });
                }

                if (selectedToolset == null)
                {
                    // still no match. Use the highest installed version in this case
                    selectedToolset = installedToolsets.FirstOrDefault();
                }
            }

            if (selectedToolset == null)
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(
                            nameof(NuGetResources.Error_MSBuildNotInstalled)));
            }

            return selectedToolset;
        }

        public static string GetMsbuildDirectory(string version, IConsole console)
        {
            List<Toolset> installedToolsets;
            using (var projectCollection = new ProjectCollection())
            {
                installedToolsets = projectCollection.Toolsets.OrderByDescending(
                    toolset => new Version(toolset.ToolsVersion)).ToList();
            }

            if (string.IsNullOrEmpty(version))
            {
                var msbuildVersion = GetMSBuildVersionInPath();
                var toolset = SelectMsbuildToolset(msbuildVersion, installedToolsets);

                if (console != null)
                {
                    if (console.Verbosity == Verbosity.Detailed)
                    {
                        console.WriteLine(
                            LocalizedResourceManager.GetString(
                                nameof(NuGetResources.MSBuildAutoDetection_Verbose)),
                            toolset.ToolsVersion,
                            toolset.ToolsPath);
                    }
                    else
                    {
                        console.WriteLine(
                            LocalizedResourceManager.GetString(
                                nameof(NuGetResources.MSBuildAutoDetection)),
                            toolset.ToolsVersion,
                            toolset.ToolsPath);
                    }
                }

                return toolset.ToolsPath;
            }
            else
            {
                var versionString = version.Contains('.') ?
                    version :
                    version + ".0";
                Version ver;
                if (!Version.TryParse(versionString, out ver))
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString(
                            nameof(NuGetResources.Error_InvalidMsbuildVersion)),
                        version);

                    throw new CommandLineException(message);
                }

                var selectedToolset = installedToolsets.FirstOrDefault(
                    toolset =>
                    {
                        var toolsVersion = new Version(toolset.ToolsVersion);
                        return (toolsVersion.Major == ver.Major &&
                            toolsVersion.Minor == ver.Minor);
                    });
                if (selectedToolset == null)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString(
                            nameof(NuGetResources.Error_CannotFindMsbuild)),
                        version);
                    throw new CommandLineException(message);
                }

                return selectedToolset.ToolsPath;
            }
        }

        private static void AppendQuoted(StringBuilder builder, string targetPath)
        {
            builder
                .Append('"')
                .Append(targetPath)
                .Append('"');
        }

        private static void ExtractResource(string resourceName, string targetPath)
        {
            using (var input = typeof(MsBuildUtility).Assembly.GetManifestResourceStream(resourceName))
            {
                using (var output = File.OpenWrite(targetPath))
                {
                    input.CopyTo(output);
                }
            }
        }
    }
}