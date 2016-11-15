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
using System.Threading.Tasks;
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
        public static async Task<DependencyGraphSpec> GetProjectReferencesAsync(
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
                    var errors = new StringBuilder();
                    using (var errorTask = ConsumeStreamReaderAsync(process.StandardError, errors))
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
                            await errorTask;
                            throw new CommandLineException(errors.ToString());
                        }
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

        private static async Task ConsumeStreamReaderAsync(StreamReader reader, StringBuilder lines)
        {
            await Task.Yield();

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lines.AppendLine(line);
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
        private static Version GetMSBuildVersionInPath()
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
                    var output = new StringBuilder();
                    using (var outputTask = ConsumeStreamReaderAsync(process.StandardOutput, output))
                    {
                        process.WaitForExit(MsBuildWaitTime);
                        if (process.ExitCode == 0)
                        {
                            outputTask.Wait();

                            // The output of msbuid /version /nologo with MSBuild 12 & 14 is something like:
                            // 14.0.23107.0
                            var lines = output.ToString().Split(
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
        /// <remarks>This method is not intended to be called directly. It's marked public so that it
        /// can be called by unit tests.</remarks>
        public static MsbuildToolSet SelectMsbuildToolset(
            Version msbuildVersion,
            IEnumerable<MsbuildToolSet> installedToolsets)
        {
            MsbuildToolSet selectedToolset;
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
                        var v = SafeParseVersion(toolset.ToolsVersion);
                        return v.Major == msbuildVersion.Major && v.Minor == v.Minor;
                    });

                if (selectedToolset == null)
                {
                    // no match found. Now search by major only
                    selectedToolset = installedToolsets.FirstOrDefault(
                        toolset =>
                        {
                            var v = SafeParseVersion(toolset.ToolsVersion);
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
            // Try to find msbuild for mono from hard code path.
            // Mono always tell user we are on unix even user is on Mac.
            if (RuntimeEnvironmentHelper.IsMono)
            {
                if (userVersion != null)
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
                    var path = new[] { new MsbuildToolSet("15.0", CommandLineConstants.MsbuildPathOnMac15),
                       new MsbuildToolSet("14.1", CommandLineConstants.MsbuildPathOnMac14) }
                    .FirstOrDefault(p => Directory.Exists(p.ToolsPath));

                    if (path != null)
                    {
                        if (console != null)
                        {
                            if (console.Verbosity == Verbosity.Detailed)
                            {
                                console.WriteLine(
                                    LocalizedResourceManager.GetString(
                                        nameof(NuGetResources.MSBuildAutoDetection_Verbose)),
                                    path.ToolsVersion,
                                    path.ToolsPath);
                            }
                            else
                            {
                                console.WriteLine(
                                    LocalizedResourceManager.GetString(
                                        nameof(NuGetResources.MSBuildAutoDetection)),
                                    path.ToolsVersion,
                                    path.ToolsPath);
                            }
                        }

                        return path.ToolsPath;
                    }
                }
            }

            try
            {
                List<MsbuildToolSet> installedToolsets = new List<MsbuildToolSet>();
                var assembly = Assembly.Load(
                        "Microsoft.Build, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                Type projectCollectionType = assembly.GetType(
                   "Microsoft.Build.Evaluation.ProjectCollection",
                   throwOnError: true);
                var projectCollection = Activator.CreateInstance(projectCollectionType) as IDisposable;

                using (projectCollection)
                {
                    var installed = ((dynamic)projectCollection).Toolsets;

                    foreach (dynamic item in installed)
                    {
                        installedToolsets.Add(new MsbuildToolSet(item.ToolsVersion, item.ToolsPath));
                    }

                    installedToolsets = installedToolsets.OrderByDescending(toolset => SafeParseVersion(toolset.ToolsVersion)).ToList();
                }

                return GetMsbuildDirectoryInternal(userVersion, console, installedToolsets);
            }
            catch (Exception e)
            {
                throw new CommandLineException(LocalizedResourceManager.GetString(
                            nameof(NuGetResources.MsbuildLoadToolSetError)), e);
            }
        }

        // This method is called by GetMsbuildDirectory(). This method is not intended to be called directly.
        // It's marked public so that it can be called by unit tests.
        public static string GetMsbuildDirectoryInternal(
            string userVersion,
            IConsole console,
            IEnumerable<MsbuildToolSet> installedToolsets)
        {
            if (string.IsNullOrEmpty(userVersion))
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

        // We sort the none offical version to be first so they don't get automatically picked up
        private static Version SafeParseVersion(string version)
        {
            Version result;

            if (Version.TryParse(version, out result))
            {
                return result;
            }
            else
            {
                return new Version(0, 0);
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