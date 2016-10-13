// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Setup.Configuration;
using NuGet.Commands;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.CommandLine
{
    public static class MsBuildUtility
    {
        internal const int MsBuildWaitTime = 2 * 60 * 1000; // 2 minutes in milliseconds

        private const string NuGetTargets =
            "NuGet.CommandLine.NuGet.targets";

        public static bool IsMsBuildBasedProject(string projectFullPath)
        {
            return projectFullPath.EndsWith("proj", StringComparison.OrdinalIgnoreCase);
        }

        public static int Build(string msbuildDirectory,
                                    string args)
        {
            string msbuildPath = Path.Combine(msbuildDirectory, "msbuild.exe");

            if (!File.Exists(msbuildPath))
            {
                throw new CommandLineException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString(nameof(NuGetResources.MsBuildDoesNotExistAtPath)),
                        msbuildPath));
            }

            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = msbuildPath,
                Arguments = args,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            using (var process = Process.Start(processStartInfo))
            {
                process.WaitForExit();

                return process.ExitCode;
            }
        }

        /// <summary>
        /// Returns the closure of project references for projects specified in <paramref name="projectPaths"/>.
        /// </summary>
        public static DependencyGraphSpec GetProjectReferences(
            string msbuildDirectory,
            string[] projectPaths,
            int timeOut,
            IConsole console,
            bool recursive)
        {
            string msbuildPath = Path.Combine(msbuildDirectory, "msbuild.exe");

            if (!File.Exists(msbuildPath))
            {
                throw new CommandLineException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString(nameof(NuGetResources.MsBuildDoesNotExistAtPath)),
                        msbuildPath));
            }

            var nugetExePath = Assembly.GetEntryAssembly().Location;

            // Check for the non-ILMerged path
            var buildTasksPath = Path.Combine(Path.GetDirectoryName(nugetExePath), "NuGet.Build.Tasks.dll");

            if (File.Exists(buildTasksPath))
            {
                nugetExePath = buildTasksPath;
            }

            using (var entryPointTargetPath = new TempFile(".targets"))
            using (var resultsPath = new TempFile(".result"))
            {
                ExtractResource(NuGetTargets, entryPointTargetPath);

                // Use RestoreUseCustomAfterTargets=true to allow recursion
                // for scenarios where NuGet is not part of ImportsAfter.
                var argumentBuilder = new StringBuilder(
                    "/t:GenerateRestoreGraphFile " +
                    "/nologo /nr:false /p:RestoreUseCustomAfterTargets=true " +
                    "/p:BuildProjectReferences=false");

                // Set the msbuild verbosity level if specified
                var msbuildVerbosity = Environment.GetEnvironmentVariable("NUGET_RESTORE_MSBUILD_VERBOSITY");

                if (string.IsNullOrEmpty(msbuildVerbosity))
                {
                    argumentBuilder.Append(" /v:q ");
                }
                else
                {
                    argumentBuilder.Append($" /v:{msbuildVerbosity} ");
                }

                // Add additional args to msbuild if needed
                var msbuildAdditionalArgs = Environment.GetEnvironmentVariable("NUGET_RESTORE_MSBUILD_ARGS");

                if (!string.IsNullOrEmpty(msbuildAdditionalArgs))
                {
                    argumentBuilder.Append($" {msbuildAdditionalArgs} ");
                }

                // Override the target under ImportsAfter with the current NuGet.targets version.
                argumentBuilder.Append(" /p:NuGetRestoreTargets=");
                AppendQuoted(argumentBuilder, entryPointTargetPath);

                // Set path to nuget.exe or the build task
                argumentBuilder.Append(" /p:RestoreTaskAssemblyFile=");
                AppendQuoted(argumentBuilder, nugetExePath);

                // dg file output path
                argumentBuilder.Append(" /p:RestoreGraphOutputPath=");
                AppendQuoted(argumentBuilder, resultsPath);

                // Disallow the import of targets/props from packages
                argumentBuilder.Append(" /p:ExcludeRestorePackageImports=true ");

                // Add all depenencies as top level restore projects if recursive is set
                if (recursive)
                {
                    argumentBuilder.Append($" /p:RestoreRecursive=true ");
                }

                // Projects to restore
                argumentBuilder.Append(" /p:RestoreGraphProjectInput=\"");
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

                console.LogDebug($"{processStartInfo.FileName} {processStartInfo.Arguments}");

                using (var process = Process.Start(processStartInfo))
                {
                    var finished = process.WaitForExit(timeOut);

                    if (!finished)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                            throw new CommandLineException(
                                LocalizedResourceManager.GetString(nameof(NuGetResources.Error_CannotKillMsBuild)) + " : " +
                                ex.Message,
                                ex);
                        }

                        throw new CommandLineException(
                            LocalizedResourceManager.GetString(nameof(NuGetResources.Error_MsBuildTimedOut)));
                    }

                    if (process.ExitCode != 0)
                    {
                        throw new CommandLineException(process.StandardError.ReadToEnd());
                    }
                }

                DependencyGraphSpec spec = null;

                if (File.Exists(resultsPath))
                {
                    spec = DependencyGraphSpec.Load(resultsPath);
                    File.Delete(resultsPath);
                }
                else
                {
                    spec = new DependencyGraphSpec();
                }

                return spec;
            }
        }

        /// <summary>
        /// Gets the list of project files in a solution, using XBuild's solution parser.
        /// </summary>
        /// <param name="solutionFile">The solution file. </param>
        /// <returns>The list of project files (in full path) in the solution.</returns>
        public static IEnumerable<string> GetAllProjectFileNamesWithXBuild(string solutionFile)
        {
            try
            {
                var assembly = Assembly.Load(
                    "Microsoft.Build.Engine, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                var solutionParserType = assembly.GetType("Mono.XBuild.CommandLine.SolutionParser");
                if (solutionParserType == null)
                {
                    throw new CommandLineException(
                        LocalizedResourceManager.GetString("Error_CannotGetXBuildSolutionParser"));
                }

                var getAllProjectFileNamesMethod = solutionParserType.GetMethod(
                    "GetAllProjectFileNames",
                    new Type[] { typeof(string) });
                if (getAllProjectFileNamesMethod == null)
                {
                    throw new CommandLineException(
                        LocalizedResourceManager.GetString("Error_CannotGetGetAllProjectFileNamesMethod"));
                }

                var names = (IEnumerable<string>)getAllProjectFileNamesMethod.Invoke(
                    null, new object[] { solutionFile });
                return names;
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
        /// Gets the list of project files in a solution, using MSBuild API.
        /// </summary>
        /// <param name="solutionFile">The solution file. </param>
        /// <param name="msbuildPath">The directory that contains msbuild.</param>
        /// <returns>The list of project files (in full path) in the solution.</returns>
        public static IEnumerable<string> GetAllProjectFileNamesWithMsbuild(
            string solutionFile,
            string msbuildPath)
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

        public static IEnumerable<string> GetAllProjectFileNames(
            string solutionFile,
            string msbuildPath)
        {
            if (EnvironmentUtility.IsMonoRuntime)
            {
                return GetAllProjectFileNamesWithXBuild(solutionFile);
            }
            else
            {
                return GetAllProjectFileNamesWithMsbuild(solutionFile, msbuildPath);
            }
        }

        /// <summary>
        /// Gets the version of MSBuild in PATH.
        /// </summary>
        /// <returns>The version of MSBuild in PATH. Returns null if MSBuild does not exist in PATH.</returns>
        private static Version GetMsBuildVersionInPath()
        {
            // run msbuild to get the version
            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = "msbuild.exe",
                Arguments = "/version /nologo",
                RedirectStandardOutput = true
            };

            try
            {
                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit(MsBuildWaitTime);

                    if (process.ExitCode == 0)
                    {
                        var output = process.StandardOutput.ReadToEnd();

                        // The output of msbuid /version /nologo with MSBuild 12 & 14 is something like:
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
        /// Gets the path of MSBuild in PATH.
        /// </summary>
        /// <returns>The path of MSBuild in PATH. Returns null if MSBuild does not exist in PATH.</returns>
        private static string GetMsBuildPathInPath()
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            var paths = path?.Split(new char[] { ';' });
            return paths?.Select(p =>
            {
                // Strip leading/trailing quotes
                if (p.Length > 0 && p[0] == '\"')
                {
                    p = p.Substring(1);
                }
                if (p.Length > 0 && p[p.Length - 1] == '\"')
                {
                    p = p.Substring(0, p.Length - 1);
                }

                return p;
            }).FirstOrDefault(p =>
            {
                try
                {
                    return File.Exists(Path.Combine(p, "msbuild.exe"));
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Returns the msbuild directory. If <paramref name="userVersion"/> is null, then the directory containing
        /// the highest installed msbuild version is returned. Otherwise, the directory containing msbuild
        /// whose version matches <paramref name="userVersion"/> is returned. If no match is found,
        /// an exception will be thrown.
        /// </summary>
        /// <param name="userVersion">The user specified version. Can be null</param>
        /// <param name="console">The console used to output messages.</param>
        /// <returns>The msbuild directory.</returns>
        public static string GetMsbuildDirectory(string userVersion, IConsole console)
        {
            var currentDirectoryCache = Directory.GetCurrentDirectory();
            var msBuildDirectory = string.Empty;
            List<MsBuildToolsetEx> installedToolsets = new List<MsBuildToolsetEx>();




/*
            if mono, try hardcoded paths
            if that fails, fall back to 14 in GAC, to get toolsets

            if not mono, use nupkg microsoft.build to get toolsets and integrate
*/
            












            // Try to find msbuild for mono from hard code path.
            // Mono always tell user we are on unix even when user is on Mac.
            if (RuntimeEnvironmentHelper.IsMono)
            {
                if (!string.IsNullOrEmpty(userVersion))
                {
                    switch (userVersion)
                    {
                        case "14.1": return CommandLineConstants.MsbuildPathOnMac14;
                        case "15":
                        case "15.0": return CommandLineConstants.MsbuildPathOnMac15;
                    }
                }
                else
                {
                    var toolset = new[] {
                        new MsBuildToolsetEx("15.0", CommandLineConstants.MsbuildPathOnMac15),
                        new MsBuildToolsetEx("14.1", CommandLineConstants.MsbuildPathOnMac14)}
                    .FirstOrDefault(t => Directory.Exists(t.ToolsPath));

                    if (toolset != null)
                    {
                        LogToolsetToConsole(console, toolset);
                        return toolset.ToolsPath;
                    }
                }

                try
                {
                    var assembly = Assembly.Load("Microsoft.Build, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    Type projectCollectionType = assembly.GetType("Microsoft.Build.Evaluation.ProjectCollection", throwOnError: true);
                    var projectCollection = Activator.CreateInstance(projectCollectionType) as IDisposable;

                    using (projectCollection)
                    {
                        var installed = ((dynamic)projectCollection).Toolsets;

                        foreach (dynamic item in installed)
                        {
                            installedToolsets.Add(new MsBuildToolsetEx(item.ToolsVersion, item.ToolsPath));
                        }

                        installedToolsets = installedToolsets.OrderByDescending(toolset => toolset.ParsedToolsVersion).ToList();
                    }
                }
                catch (Exception e)
                {
                    throw new CommandLineException(LocalizedResourceManager.GetString(
                                nameof(NuGetResources.MsbuildLoadToolSetError)), e);
                }
            }
            else
            {
                using (var projectCollection = new ProjectCollection())
                {
                    installedToolsets = MsBuildToolsetEx.AsMsToolsetExCollection(projectCollection.Toolsets).ToList();
                }

                var installedSxsToolsets = GetInstalledSxsToolsets();
                if (installedToolsets == null)
                {
                    installedToolsets = installedSxsToolsets;
                }
                else if (installedSxsToolsets != null)
                {
                    installedToolsets.AddRange(installedSxsToolsets);
                }
            }

            msBuildDirectory = GetMsBuildDirectoryInternal(userVersion, console, installedToolsets, () => GetMsBuildPathInPath());
            Directory.SetCurrentDirectory(currentDirectoryCache);
            return msBuildDirectory;
        }

        // This method is called by GetMsbuildDirectory(). This method is not intended to be called directly.
        // It's marked public so that it can be called by unit tests.
        public static string GetMsBuildDirectoryInternal(
            string userVersion,
            IConsole console,
            IEnumerable<MsBuildToolsetEx> installedToolsets,
            Func<string> getMSBuildPathInPath)
        {
            if (RuntimeEnvironmentHelper.IsMono)
            {
                if (string.IsNullOrEmpty(userVersion))
                {
                    var msbuildVersion = GetMsBuildVersionInPath();
                    var toolset = SelectMsBuildToolset(msbuildVersion, installedToolsets);
                    LogToolsetToConsole(console, toolset);
                    return toolset.ToolsPath;
                }
                else
                {
                    // append ".0" if the userVersion is a number
                    string userVersionString = userVersion;
                    int unused;

                    if (int.TryParse(userVersion, out unused))
                    {
                        userVersionString = userVersion + ".0";
                    }

                    Version ver;
                    bool hasNumericVersion = Version.TryParse(userVersionString, out ver);

                    var selectedToolset = installedToolsets.FirstOrDefault(
                    toolset =>
                    {
                            // first match by string comparison
                            if (string.Equals(userVersionString, toolset.ToolsVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                            // then match by Major & Minor version numbers.
                            Version toolsVersion;
                        if (hasNumericVersion && Version.TryParse(toolset.ToolsVersion, out toolsVersion))
                        {
                            return (toolsVersion.Major == ver.Major &&
                                toolsVersion.Minor == ver.Minor);
                        }

                        return false;
                    });

                    if (selectedToolset == null)
                    {
                        var message = string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString(
                                nameof(NuGetResources.Error_CannotFindMsbuild)),
                            userVersion);

                        throw new CommandLineException(message);
                    }

                    return selectedToolset.ToolsPath;
                }
            }
            else
            {
                MsBuildToolsetEx toolset;
                if (string.IsNullOrEmpty(userVersion))
                {
                    var msbuildPathInPath = getMSBuildPathInPath();
                    toolset = SelectMsBuildToolsetStartingWithPath(msbuildPathInPath, installedToolsets);
                }
                else
                {
                    toolset = SelectMsBuildToolsetForUserVersion(userVersion, installedToolsets);
                }

                if (toolset == null)
                {
                    return null;
                }

                LogToolsetToConsole(console, toolset);
                return toolset.ToolsPath;
            }
        }


        /// <summary>
        /// Gets the msbuild toolset that matches the given <paramref name="msbuildVersion"/>.
        /// </summary>
        /// <param name="msbuildVersion">The msbuild version. Can be null.</param>
        /// <param name="installedToolsets">List of installed toolsets,
        /// ordered by ToolsVersion, from highest to lowest.</param>
        /// <returns>The matching toolset.</returns>
        /// <remarks>This method is not intended to be called directly. It's marked public so that it
        /// can be called by unit tests.</remarks>
        public static MsBuildToolsetEx SelectMsBuildToolset(
            Version msbuildVersion,
            IEnumerable<MsBuildToolsetEx> installedToolsets)
        {
            MsBuildToolsetEx selectedToolset;
            if (msbuildVersion == null)
            {
                // MSBuild does not exist in PATH. In this case, the highest installed version is used
                selectedToolset = installedToolsets.FirstOrDefault();
            }
            else
            {
                // Search by major & minor version
                selectedToolset = installedToolsets.FirstOrDefault(t =>
                    t.ParsedToolsVersion.Major == msbuildVersion.Major && 
                    t.ParsedToolsVersion.Minor == msbuildVersion.Minor);

                if (selectedToolset == null)
                {
                    // no match found. Now search by major only
                    selectedToolset = installedToolsets.FirstOrDefault(t => t.ParsedToolsVersion.Major == msbuildVersion.Major);
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

        /// <summary>
        /// Gets the msbuild toolset that matches the given <paramref name="msbuildVersion"/>.
        /// </summary>
        /// <param name="msbuildVersion">The msbuild version. Can be null.</param>
        /// <param name="installedToolsets">List of installed toolsets,
        /// ordered by ToolsVersion, from highest to lowest.</param>
        /// <returns>The matching toolset.</returns>
        private static MsBuildToolsetEx SelectMsBuildToolsetStartingWithPath(
            string msbuildPathInPath,
            IEnumerable<MsBuildToolsetEx> installedToolsets)
        {
            MsBuildToolsetEx selectedToolset;
            if (string.IsNullOrEmpty(msbuildPathInPath))
            {
                // MSBuild does not exist in PATH. In this case, the highest installed version is used
                selectedToolset = installedToolsets.OrderByDescending(t => t).FirstOrDefault();
            }
            else
            {
                // Search by path. We use a StartsWith match because a toolset's path may have an architecture specialization.
                // e.g. 
                //     c:\Program Files (x86)\MSBuild\14.0\Bin
                // is specified in the path (a path which we have validated contains an msbuild.exe) and the toolset is locaated at 
                //     c:\Program Files (x86)\MSBuild\14.0\Bin\amd64
                selectedToolset = installedToolsets.OrderByDescending(t => t).FirstOrDefault(
                    t => t.ToolsPath.StartsWith(msbuildPathInPath, StringComparison.OrdinalIgnoreCase));

                if (selectedToolset == null)
                {
                    // No match. Fail silently. Use the highest installed version in this case
                    selectedToolset = installedToolsets.OrderByDescending(t => t).FirstOrDefault();
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

        private static MsBuildToolsetEx SelectMsBuildToolsetForUserVersion(
            string userVersion,
            IEnumerable<MsBuildToolsetEx> installedToolsets)
        {
            // Force version string to 1 decimal place
            string userVersionString = userVersion;
            decimal parsedVersion = 0;
            if (decimal.TryParse(userVersion, out parsedVersion))
            {
                decimal adjustedVersion = (decimal)(((int)(parsedVersion * 10)) / 10F);
                userVersionString = adjustedVersion.ToString("F1");
            }

            var selectedToolset = installedToolsets.OrderByDescending(t => t).FirstOrDefault(
            t =>
            {
                // first match by string comparison
                if (string.Equals(userVersionString, t.ToolsVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // then match by Major & Minor version numbers. And we want an actual parsing of t.ToolsVersion,
                // without the safe fallback to 0.0 built into t.ParsedToolsVersion.
                Version parsedUserVersion;
                Version parsedToolsVersion;
                if (Version.TryParse(userVersionString, out parsedUserVersion) &&
                    Version.TryParse(t.ToolsVersion, out parsedToolsVersion))
                {
                    return parsedToolsVersion.Major == parsedUserVersion.Major &&
                        parsedToolsVersion.Minor == parsedUserVersion.Minor;
                }

                return false;
            });

            if (selectedToolset == null)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString(
                        nameof(NuGetResources.Error_CannotFindMsbuild)),
                    userVersion);

                throw new CommandLineException(message);
            }

            return selectedToolset;
        }

        private static void LogToolsetToConsole(IConsole console, MsBuildToolsetEx toolset)
        {
            if (console == null)
            {
                return;
            }

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

        public static Lazy<string> GetMsbuildDirectoryFromMsbuildPath(string msbuildPath, string msbuildVersion, IConsole console)
        {
            if (msbuildPath != null)
            {
                if (msbuildVersion != null)
                {
                    console?.WriteWarning(LocalizedResourceManager.GetString(
                        nameof(NuGetResources.Warning_MsbuildPath)),
                        msbuildPath, msbuildVersion);
                }

                console?.WriteLine(LocalizedResourceManager.GetString(
                               nameof(NuGetResources.MSbuildFromPath)),
                           msbuildPath);

                if (!Directory.Exists(msbuildPath))
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString(
                            nameof(NuGetResources.MsbuildPathNotExist)),
                        msbuildPath);

                    throw new CommandLineException(message);
                }
                return new Lazy<string>(() => msbuildPath);
            }
            else
            {
                return new Lazy<string>(() => GetMsbuildDirectory(msbuildVersion, console));
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

        /// <summary>
        /// This class is used to create a temp file, which is deleted in Dispose().
        /// </summary>
        private class TempFile : IDisposable
        {
            private readonly string _filePath;

            /// <summary>
            /// Constructor. It creates an empty temp file under the temp directory / NuGet, with
            /// extension <paramref name="extension"/>.
            /// </summary>
            /// <param name="extension">The extension of the temp file.</param>
            public TempFile(string extension)
            {
                if (string.IsNullOrEmpty(extension))
                {
                    throw new ArgumentNullException(nameof(extension));
                }

                var tempDirectory = Path.Combine(Path.GetTempPath(), "NuGet-Scratch");

                Directory.CreateDirectory(tempDirectory);

                int count = 0;
                do
                {
                    _filePath = Path.Combine(tempDirectory, Path.GetRandomFileName() + extension);

                    if (!File.Exists(_filePath))
                    {
                        try
                        {
                            // create an empty file
                            using (var filestream = File.Open(_filePath, FileMode.CreateNew))
                            {
                            }

                            // file is created successfully.
                            return;
                        }
                        catch
                        {
                        }
                    }

                    count++;
                }
                while (count < 3);

                throw new InvalidOperationException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.Error_FailedToCreateRandomFileForP2P)));
            }

            public static implicit operator string(TempFile f)
            {
                return f._filePath;
            }

            public void Dispose()
            {
                if (File.Exists(_filePath))
                {
                    try
                    {
                        File.Delete(_filePath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static List<MsBuildToolsetEx> GetInstalledSxsToolsets()
        {
            ISetupConfiguration configuration;
            try
            {
                configuration = new SetupConfiguration() as ISetupConfiguration2;
            }
            catch (Exception)
            {
                return null; // No COM class
            }

            if (configuration == null)
            {
                return null;
            }

            var enumerator = configuration.EnumInstances();
            if (enumerator == null)
            {
                return null;
            }

            var setupInstances = new List<MsBuildToolsetEx>();
            while (true)
            {
                var fetchedInstances = new ISetupInstance[3];
                int fetched;
                enumerator.Next(fetchedInstances.Length, fetchedInstances, out fetched);
                if (fetched == 0)
                {
                    break;
                }

                // fetched will return the value 3 even if only one instance returned                
                int index = 0;
                while (index < fetched)
                {
                    if (fetchedInstances[index] != null)
                    {
                        setupInstances.Add(new MsBuildToolsetEx(fetchedInstances[index]));
                    }

                    index++;
                }
            }

            if (setupInstances.Count == 0)
            {
                return null;
            }

            return setupInstances;
        }

        private static string GetMsbuild(string msbuildDirectory)
        {
            if (RuntimeEnvironmentHelper.IsMono)
            {
                // Try to find msbuild or xbuild in $Path.
                string[] pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

                if (pathDirs?.Length > 0)
                {
                    foreach (var exeName in new[] { "msbuild", "xbuild" })
                    {
                        var exePath = pathDirs.Select(dir => Path.Combine(dir, exeName)).FirstOrDefault(File.Exists);
                        if (exePath != null)
                        {
                            return exePath;
                        }
                    }
                }

                // Try to find msbuild.exe from hard code path.
                var path = new[] { CommandLineConstants.MsbuildPathOnMac15, CommandLineConstants.MsbuildPathOnMac14 }.
                    Select(p => Path.Combine(p, "msbuild.exe")).FirstOrDefault(File.Exists);

                if (path != null)
                {
                    return path;
                }
                else
                {
                    return Path.Combine(msbuildDirectory, "xbuild.exe");
                }
            }
            else
            {
                return Path.Combine(msbuildDirectory, "msbuild.exe");
            }

        }

        /// <summary>
        /// Escapes a string so that it can be safely passed as a command line argument when starting a msbuild process.
        /// Source: http://stackoverflow.com/a/12364234
        /// </summary>
        public static string Escape(string argument)
        {
            if (argument == string.Empty)
            {
                return "\"\"";
            }

            var escaped = Regex.Replace(argument, @"(\\*)""", @"$1\$0");

            escaped = Regex.Replace(
                escaped,
                @"^(.*\s.*?)(\\*)$", @"""$1$2$2""",
                RegexOptions.Singleline);

            return escaped;
        }
    }
}