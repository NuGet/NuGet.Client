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
using System.Xml.Linq;
using Microsoft.VisualStudio.Setup.Configuration;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.CommandLine
{
    public static class MsBuildUtility
    {
        internal const int MsBuildWaitTime = 2 * 60 * 1000; // 2 minutes in milliseconds

        private const string NuGetTargets = "NuGet.CommandLine.NuGet.targets";
        private static readonly XNamespace MSBuildNamespace = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");

        private readonly static string[] MSBuildVersions = new string[] { "14", "12", "4" };

        public static bool IsMsBuildBasedProject(string projectFullPath)
        {
            return projectFullPath.EndsWith("proj", StringComparison.OrdinalIgnoreCase);
        }

        public static int Build(string msbuildDirectory,
                                    string args)
        {
            var msbuildPath = GetMsbuild(msbuildDirectory);

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
            bool recursive,
            string solutionDirectory,
            string restoreConfigFile,
            string[] sources,
            string packagesDirectory)
        {
            var msbuildPath = GetMsbuild(msbuildDirectory);

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

            using (var inputTargetPath = new TempFile(".nugetinputs.targets"))
            using (var entryPointTargetPath = new TempFile(".nugetrestore.targets"))
            using (var resultsPath = new TempFile(".output.dg"))
            {
                // Read NuGet.targets from nuget.exe and write it to disk for msbuild.exe
                ExtractResource(NuGetTargets, entryPointTargetPath);

                // Build a .targets file of all restore inputs, this is needed to avoid going over the limit on command line arguments.
                var properties = new Dictionary<string, string>()
                {
                    { "RestoreUseCustomAfterTargets", "true" },
                    { "RestoreGraphOutputPath", resultsPath },
                    { "RestoreRecursive", recursive.ToString().ToLowerInvariant() },
                    { "RestoreProjectFilterMode", "exclusionlist" }
                };

                var inputTargetXML = GetRestoreInputFile(entryPointTargetPath, properties, projectPaths);

                inputTargetXML.Save(inputTargetPath);

                // Create msbuild parameters and include global properties that cannot be set in the input targets path
                var arguments = GetMSBuildArguments(entryPointTargetPath, inputTargetPath, nugetExePath, solutionDirectory, restoreConfigFile, sources, packagesDirectory);

                var processStartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = msbuildPath,
                    Arguments = arguments,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                console.LogDebug($"{processStartInfo.FileName} {processStartInfo.Arguments}");

                using (var process = Process.Start(processStartInfo))
                {
                    var errors = new StringBuilder();
                    var output = new StringBuilder();
                    var excluded = new string[] { "msb4011", entryPointTargetPath };

                    // Read console output
                    var errorTask = ConsumeStreamReaderAsync(process.StandardError, errors, filter: null);
                    var outputTask = ConsumeStreamReaderAsync(process.StandardOutput, output, filter: (line) => IsIgnoredOutput(line, excluded));

                    // Run msbuild
                    var finished = process.WaitForExit(timeOut);

                    // Handle timeouts
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
                    }

                    // Read all console output from msbuild.
                    await Task.WhenAll(outputTask, errorTask);

                    // By default log msbuild output so that it is only
                    // displayed under -Verbosity detailed
                    var logLevel = LogLevel.Verbose;

                    if (process.ExitCode != 0 || !finished)
                    {
                        // If a problem occurred log all msbuild output as an error 
                        // so that the user can see it.
                        // By default this runs with /v:q which means that only
                        // errors and warnings will be in the output.
                        logLevel = LogLevel.Error;
                    }

                    // MSBuild writes errors to the output stream, parsing the console output to find
                    // the errors would be error prone so here we log all output combined with any
                    // errors on the error stream (haven't seen the error stream used to date) 
                    // to give the user the complete info.
                    await console.LogAsync(logLevel, output.ToString() + errors.ToString());

                    if (!finished)
                    {
                        // MSBuild timed out
                        throw new CommandLineException(
                                LocalizedResourceManager.GetString(nameof(NuGetResources.Error_MsBuildTimedOut)));
                    }

                    await outputTask;
                    
                    if (process.ExitCode != 0)
                    {
                        // Do not continue if msbuild failed.
                        throw new ExitCodeException(1);
                    }
                }

                DependencyGraphSpec spec = null;

                if (File.Exists(resultsPath) && new FileInfo(resultsPath).Length != 0)
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

        public static string GetMSBuildArguments(
            string entryPointTargetPath,
            string inputTargetPath,
            string nugetExePath,
            string solutionDirectory,
            string restoreConfigFile,
            string[] sources,
            string packagesDirectory)
        {
            // args for MSBuild.exe
            var args = new List<string>()
            {
                EscapeQuoted(inputTargetPath),
                "/t:GenerateRestoreGraphFile",
                "/nologo",
                "/nr:false"
            };

            // Set the msbuild verbosity level if specified
            var msbuildVerbosity = Environment.GetEnvironmentVariable("NUGET_RESTORE_MSBUILD_VERBOSITY");

            if (string.IsNullOrEmpty(msbuildVerbosity))
            {
                args.Add("/v:q");
            }
            else
            {
                args.Add($"/v:{msbuildVerbosity} ");
            }

            // Override the target under ImportsAfter with the current NuGet.targets version.
            AddProperty(args, "NuGetRestoreTargets", entryPointTargetPath);

            // Set path to nuget.exe or the build task
            AddProperty(args, "RestoreTaskAssemblyFile", nugetExePath);

            // Settings
            AddRestoreSources(args, sources);
            AddPropertyIfHasValue(args, "RestoreSolutionDirectory", solutionDirectory);
            AddPropertyIfHasValue(args, "RestoreConfigFile", restoreConfigFile);
            AddPropertyIfHasValue(args, "RestorePackagesPath", packagesDirectory);
            AddPropertyIfHasValue(args, "SolutionDir", solutionDirectory);

            // Add additional args to msbuild if needed
            var msbuildAdditionalArgs = Environment.GetEnvironmentVariable("NUGET_RESTORE_MSBUILD_ARGS");

            if (!string.IsNullOrEmpty(msbuildAdditionalArgs))
            {
                args.Add(msbuildAdditionalArgs);
            }

            return string.Join(" ", args);
        }

        private static void AddRestoreSources(List<string> args, string[] sources)
        {
            if (sources.Length != 0)
            {
                var isMono = RuntimeEnvironmentHelper.IsMono && !RuntimeEnvironmentHelper.IsWindows;

                var sourceBuilder = new StringBuilder();

                if (isMono)
                {
                    sourceBuilder.Append("/p:RestoreSources=\\\"");
                }
                else
                {
                    sourceBuilder.Append("/p:RestoreSources=\"");
                }

                for (var i = 0; i < sources.Length; i++)
                {
                    if (isMono)
                    {
                        sourceBuilder.Append(sources[i])
                            .Append("\\;");
                    }
                    else
                    {
                        sourceBuilder.Append(sources[i])
                            .Append(";");
                    }
                }

                if (isMono)
                {
                    sourceBuilder.Append("\\\" ");
                }
                else
                {
                    sourceBuilder.Append("\" ");
                }

                args.Add(sourceBuilder.ToString());
            }
        }

        public static XDocument GetRestoreInputFile(string restoreTargetPath, Dictionary<string, string> properties, IEnumerable<string> projectPaths)
        {
            return GenerateMSBuildFile(
                new XElement(MSBuildNamespace + "PropertyGroup", properties.Select(e => new XElement(MSBuildNamespace + e.Key, e.Value))),
                new XElement(MSBuildNamespace + "ItemGroup", projectPaths.Select(GetRestoreGraphProjectInputItem)),
                new XElement(MSBuildNamespace + "Import", new XAttribute(XName.Get("Project"), restoreTargetPath)));
        }

        public static XDocument GenerateMSBuildFile(params XElement[] elements)
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", "no"),
                new XElement(MSBuildNamespace + "Project",
                    new XAttribute("ToolsVersion", "14.0"),
                    elements));
        }

        private static XElement GetRestoreGraphProjectInputItem(string path)
        {
            return new XElement(MSBuildNamespace + "RestoreGraphProjectInputItems", new XAttribute(XName.Get("Include"), path));
        }

        private static bool IsIgnoredOutput(string line, string[] excluded)
        {
            return excluded.All(p => line.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static async Task ConsumeStreamReaderAsync(StreamReader reader, StringBuilder lines, Func<string, bool> filter)
        {
            await Task.Yield();

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (filter == null ||
                    !filter(line))
                {
                    lines.AppendLine(line);
                }
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
        public static IEnumerable<string> GetAllProjectFileNamesWithMsBuild(
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
                var exMessage = ex.Message;
                if (ex.InnerException != null)
                    exMessage += "  " + ex.InnerException.Message;
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("Error_SolutionFileParseError"),
                    solutionFile,
                    exMessage);

                throw new CommandLineException(message);
            }
        }

        public static IEnumerable<string> GetAllProjectFileNames(
            string solutionFile,
            string msbuildPath)
        {
            if (RuntimeEnvironmentHelper.IsMono && msbuildPath.Contains("xbuild"))
            {
                return GetAllProjectFileNamesWithXBuild(solutionFile);
            }

            return GetAllProjectFileNamesWithMsBuild(solutionFile, msbuildPath);
        }

        /// <summary>
        /// Returns the msbuild directory. If <paramref name="userVersion"/> is null, then the directory containing
        /// the highest installed msbuild version is returned. Otherwise, the directory containing msbuild
        /// whose version matches <paramref name="userVersion"/> is returned. If no match is found,
        /// an exception will be thrown. Note that we use Microsoft.Build types as
        /// </summary>
        /// <param name="userVersion">version string as passed by user (so may be empty)</param>
        /// <param name="console">The console used to output messages.</param>
        /// <returns>The msbuild directory.</returns>
        public static string GetMsBuildDirectory(string userVersion, IConsole console)
        {
            var currentDirectoryCache = Directory.GetCurrentDirectory();
            var msBuildDirectory = string.Empty;
            var installedToolsets = new List<MsBuildToolset>();

            // If Mono, test well known paths and bail if found
            var toolset = GetMsBuildFromMonoPaths(userVersion);
            if (toolset != null)
            {
                LogToolsetToConsole(console, toolset);
                return toolset.Path;
            }

            using (var projectCollection = LoadProjectCollection())
            {
                var installed = ((dynamic)projectCollection)?.Toolsets;
                if (installed != null)
                {
                    foreach (var item in installed)
                    {
                        installedToolsets.Add(new MsBuildToolset(version: item.ToolsVersion, path: item.ToolsPath));
                    }

                    installedToolsets = installedToolsets.ToList();
                }
            }

            // In a non-Mono environment, we have the potential for SxS installs of MSBuild 15.1+. Let's add these here.
            if (!RuntimeEnvironmentHelper.IsMono)
            {
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

            if (!installedToolsets.Any())
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(
                        nameof(NuGetResources.Error_CannotFindMsbuild)));
            }

            msBuildDirectory = GetMsBuildDirectoryInternal(
                userVersion, console, installedToolsets.OrderByDescending(t => t), () => GetMsBuildPathInPathVar());
            Directory.SetCurrentDirectory(currentDirectoryCache);
            return msBuildDirectory;
        }

        /// <summary>
        /// This method is called by GetMsBuildDirectory(). This method is not intended to be called directly.
        /// It's marked public so that it can be called by unit tests.
        /// </summary>
        /// <param name="userVersion">version string as passed by user (so may be empty)</param>
        /// <param name="console">console for status reporting</param>
        /// <param name="installedToolsets">all msbuild toolsets discovered by caller</param>
        /// <param name="getMsBuildPathInPathVar">delegate to provide msbuild exe discovered in path environemtnb var/s
        /// (using a delegate allows for testability)</param>
        /// <returns>directory to use for msbuild exe</returns>
        public static string GetMsBuildDirectoryInternal(
            string userVersion,
            IConsole console,
            IEnumerable<MsBuildToolset> installedToolsets,
            Func<string> getMsBuildPathInPathVar)
        {
            MsBuildToolset toolset;
            if (string.IsNullOrEmpty(userVersion))
            {
                var msbuildPathInPath = getMsBuildPathInPathVar();
                toolset = GetToolsetFromPath(msbuildPathInPath, installedToolsets);
            }
            else
            {
                toolset = GetToolsetFromUserVersion(userVersion, installedToolsets);
            }

            if (toolset == null)
            {
                return null;
            }

            LogToolsetToConsole(console, toolset);
            return toolset.Path;
        }

        /// <summary>
        /// Fetch project collection type from the GAC--this will service MSBuild 14 (and any toolsets included with 14).
        /// </summary>
        /// <returns>ProjectCollection instance to use for toolset enumeration</returns>
        private static IDisposable LoadProjectCollection()
        {
            foreach (var version in MSBuildVersions)
            {
                try
                {
                    var msBuildTypesAssembly = Assembly.Load($"Microsoft.Build, Version={version}.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    var projectCollectionType = msBuildTypesAssembly.GetType("Microsoft.Build.Evaluation.ProjectCollection", throwOnError: true);
                    return Activator.CreateInstance(projectCollectionType) as IDisposable;
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        /// <summary>
        /// Try to find msbuild for mono from hard code path
        /// </summary>
        /// <param name="userVersion">version string as passed by user (so may be empty)</param>
        /// <returns></returns>
        public static MsBuildToolset GetMsBuildFromMonoPaths(string userVersion)
        {
            // Mono always tell user we are on unix even when user is on Mac.
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                return null;
            }

            if (string.IsNullOrEmpty(userVersion))
            {
                return new[] {
                        new MsBuildToolset(version: "15.0", path: CommandLineConstants.MsBuildPathOnMac15),
                        new MsBuildToolset(version: "14.1", path: CommandLineConstants.MsBuildPathOnMac14)}
                    .FirstOrDefault(t => Directory.Exists(t.Path));
            }
            else
            {
                switch (userVersion)
                {
                    case "14.1": return new MsBuildToolset(version: "14.1", path: CommandLineConstants.MsBuildPathOnMac14);
                    case "15":
                    case "15.0": return new MsBuildToolset(version: userVersion, path: CommandLineConstants.MsBuildPathOnMac15);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the (first) path of MSBuild to appear in environment variable PATH.
        /// </summary>
        /// <returns>The path of MSBuild in PATH environment variable. Returns null if MSBuild location does not exist
        /// in the variable string.</returns>
        private static string GetMsBuildPathInPathVar()
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
        /// Gets the msbuild toolset found in/under the path passed.
        /// </summary>
        /// <param name="msBuildPath">The msbuild path as found in PATH env var. Can be null.</param>
        /// <param name="installedToolsets">List of installed toolsets,
        /// ordered by ToolsVersion, from highest to lowest.</param>
        /// <returns>The matching toolset.</returns>
        private static MsBuildToolset GetToolsetFromPath(
            string msBuildPath,
            IEnumerable<MsBuildToolset> installedToolsets)
        {
            MsBuildToolset selectedToolset;
            if (string.IsNullOrEmpty(msBuildPath))
            {
                // We have no path for a specifically requested msbuild. Use the highest installed version.
                selectedToolset = installedToolsets.FirstOrDefault();
            }
            else
            {
                // Search by path. We use a StartsWith match because a toolset's path may have an architecture specialization.
                // e.g.
                //     c:\Program Files (x86)\MSBuild\14.0\Bin
                // is specified in the path (a path which we have validated contains an msbuild.exe) and the toolset is located at
                //     c:\Program Files (x86)\MSBuild\14.0\Bin\amd64
                selectedToolset = installedToolsets.FirstOrDefault(
                    t => t.Path.StartsWith(msBuildPath, StringComparison.OrdinalIgnoreCase));

                if (selectedToolset == null)
                {
                    // No match. Fail silently. Use the highest installed version in this case
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

        private static MsBuildToolset GetToolsetFromUserVersion(
            string userVersion,
            IEnumerable<MsBuildToolset> installedToolsets)
        {
            // Version.TryParse only take decimal string like "14.0", "14" need to be converted.
            var versionParts = userVersion.Split('.');
            var major = versionParts.Length > 0 ? versionParts[0] : "0";
            var minor = versionParts.Length > 1 ? versionParts[1] : "0";

            var userVersionString = string.Join(".", major, minor);

            // First match by string comparison
            var selectedToolset = installedToolsets.FirstOrDefault(
                t => string.Equals(userVersion, t.Version, StringComparison.OrdinalIgnoreCase));

            if (selectedToolset != null)
            {
                return selectedToolset;
            }

            // Then match by Major & Minor version numbers. And we want an actual parsing of t.ToolsVersion,
            // without the safe fallback to 0.0 built into t.ParsedToolsVersion.
            selectedToolset = installedToolsets.FirstOrDefault(t =>
            {
                Version parsedUserVersion;
                Version parsedToolsVersion;
                if (Version.TryParse(userVersionString, out parsedUserVersion) &&
                    Version.TryParse(t.Version, out parsedToolsVersion))
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

        private static void LogToolsetToConsole(IConsole console, MsBuildToolset toolset)
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
                    toolset.Version,
                    toolset.Path);
            }
            else
            {
                console.WriteLine(
                    LocalizedResourceManager.GetString(
                        nameof(NuGetResources.MSBuildAutoDetection)),
                    toolset.Version,
                    toolset.Path);
            }
        }

        public static Lazy<string> GetMsBuildDirectoryFromMsBuildPath(string msbuildPath, string msbuildVersion, IConsole console)
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
                return new Lazy<string>(() => GetMsBuildDirectory(msbuildVersion, console));
            }
        }

        private static void AddProperty(List<string> args, string property, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(nameof(value));
            }

            AddPropertyIfHasValue(args, property, value);
        }

        private static void AddPropertyIfHasValue(List<string> args, string property, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                args.Add($"/p:{property}={EscapeQuoted(value)}");
            }
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

        private static List<MsBuildToolset> GetInstalledSxsToolsets()
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

            var setupInstances = new List<MsBuildToolset>();
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
                var index = 0;
                while (index < fetched)
                {
                    if (fetchedInstances[index] != null)
                    {
                        setupInstances.Add(new MsBuildToolset(fetchedInstances[index]));
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

        public static string EscapeQuoted(string argument)
        {
            if (argument == string.Empty)
            {
                return "\"\"";
            }
            var escaped = Regex.Replace(argument, @"(\\*)" + "\"", @"$1$1\" + "\"");
            escaped = "\"" + Regex.Replace(escaped, @"(\\+)$", @"$1$1") + "\"";
            return escaped;

        }

        private static string GetMsbuild(string msbuildDirectory)
        {
            if (RuntimeEnvironmentHelper.IsMono)
            {
                // Try to find msbuild or xbuild in $Path.
                var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

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
                var path = new[] { CommandLineConstants.MsBuildPathOnMac15, CommandLineConstants.MsBuildPathOnMac14 }.
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

                var tempDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp);

                Directory.CreateDirectory(tempDirectory);

                var count = 0;
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
                            // Ignore and try again
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
                try
                {
                    FileUtility.Delete(_filePath);
                }
                catch
                {
                    // Ignore failures
                }
            }
        }
    }
}