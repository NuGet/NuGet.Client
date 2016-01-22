// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.CommandLine.XPlat
{
    public class Program
    {
        private static ILogger _log;

        public static int Main(string[] args)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                while (!Debugger.IsAttached)
                {

                }
                Debugger.Break();
            }
#endif

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = Strings.App_FullName;
            app.HelpOption("-h|--help");
            app.VersionOption("--version", typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());

            var verbosity = app.Option("-v|--verbosity <verbosity>", Strings.Switch_Verbosity, CommandOptionType.SingleValue);

            // Set up logging
            _log = new CommandOutputLogger(verbosity);

#if !DNXCORE50
            // Increase the maximum number of connections per server.
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            else
            {
                // Keep mono limited to a single download to avoid issues.
                ServicePointManager.DefaultConnectionLimit = 1;
            }
#endif

            app.Command("restore", restore =>
                {
                    restore.Description = Strings.Restore_Description;

                    var sources = restore.Option(
                        "-s|--source <source>",
                        Strings.Restore_Switch_Source_Description,
                        CommandOptionType.MultipleValue);
                    var packagesDirectory = restore.Option(
                        "--packages <packagesDirectory>",
                        Strings.Restore_Switch_Packages_Description,
                        CommandOptionType.SingleValue);
                    var parallel = restore.Option(
                        "-p|--parallel <noneOrNumberOfParallelTasks>",
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Restore_Switch_Parallel_Description,
                            RestoreRequest.DefaultDegreeOfConcurrency),
                        CommandOptionType.SingleValue);
                    var fallBack = restore.Option(
                        "-f|--fallbacksource <FEED>",
                        Strings.Restore_Switch_Fallback_Description,
                        CommandOptionType.MultipleValue);
                    var runtime = restore.Option(
                        "--runtime <RID>",
                        Strings.Restore_Switch_Runtime_Description,
                        CommandOptionType.MultipleValue);
                    var argRoot = restore.Argument(
                        "[root]",
                        Strings.Restore_Arg_ProjectName_Description,
                        multipleValues: true);

                    restore.OnExecute(async () =>
                    {
                        // Ignore casing on windows
                        var comparer = RuntimeEnvironmentHelper.IsWindows ?
                            StringComparer.OrdinalIgnoreCase
                            : StringComparer.Ordinal;

                        var inputValues = new HashSet<string>(comparer);

                        if (argRoot.Values.Count < 1)
                        {
                            // Use the current directory if no path was given
                            var workingDir = Path.GetFullPath(".");

                            inputValues.UnionWith(GetProjectJsonFilesInDirectory(workingDir));
                        }
                        else
                        {
                            foreach (var inputPath in argRoot.Values)
                            {
                                var fullPath = Path.GetFullPath(inputPath);

                                // For directories find all children
                                if (Directory.Exists(inputPath))
                                {
                                    inputValues.UnionWith(GetProjectJsonFilesInDirectory(fullPath));
                                }
                                else
                                {
                                    // Add the input directly
                                    inputValues.Add(fullPath);
                                }
                            }
                        }

                        var exitCode = 0;

                        foreach (var inputPath in inputValues)
                        {
                            var currentExitCode = await ExecuteRestore(
                                sources,
                                packagesDirectory,
                                parallel,
                                fallBack,
                                runtime,
                                inputPath);

                            if (currentExitCode != 0)
                            {
                                exitCode = currentExitCode;
                            }
                        }

                        return exitCode;
                    });
                });

            app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return 0;
                });

            return app.Execute(args);
        }

        private static IEnumerable<string> GetProjectJsonFilesInDirectory(string path)
        {
            return Directory.GetFiles(path, "project.json", SearchOption.AllDirectories);
        }

        private static async Task<int> ExecuteRestore
            (CommandOption sources,
            CommandOption packagesDirectory,
            CommandOption parallel,
            CommandOption fallBack,
            CommandOption runtime,
            string inputPath)
        {
            // Figure out the project directory
            IEnumerable<string> externalProjects = null;

            PackageSpec project;
            var projectPath = Path.GetFullPath(inputPath);
            if (string.Equals(PackageSpec.PackageSpecFileName, Path.GetFileName(projectPath), StringComparison.OrdinalIgnoreCase))
            {
                _log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject,
                    inputPath));

                projectPath = Path.GetDirectoryName(projectPath);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(inputPath), Path.GetFileName(projectPath), inputPath);
            }
            else if (MsBuildUtility.IsMsBuildBasedProject(projectPath))
            {
#if DNXCORE50
                                throw new NotSupportedException();
#else
                // TODO: This only finds the top level dependencies, the rest are found through folders?
                externalProjects = MsBuildUtility.GetProjectReferences(projectPath);

                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
                var packageSpecFile = Path.Combine(projectDirectory, PackageSpec.PackageSpecFileName);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(packageSpecFile), projectPath, inputPath);
                _log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject, inputPath));
#endif
            }
            else
            {
                var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                _log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject,
                    file));

                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectPath), file);
            }
            _log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_LoadedProject,
                    project.Name, project.FilePath));

            // Resolve the root directory
            var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectPath);
            _log.LogVerbose(string.Format(
                CultureInfo.CurrentCulture,
                Strings.Log_FoundProjectRoot,
                rootDirectory));

            // Load settings based on the current project path.
            var settings = Settings.LoadDefaultSettings(projectPath,
                configFileName: null,
                machineWideSettings: null);

            var packageSources = GetSources(sources, fallBack, settings);

            using (var request = new RestoreRequest(
                project,
                packageSources,
                packagesDirectory: null))
            {
                request.CompatibilityCheckAsWarning = true;

                if (packagesDirectory.HasValue())
                {
                    request.PackagesDirectory = packagesDirectory.Value();
                }
                else
                {
                    request.PackagesDirectory = SettingsUtility.GetGlobalPackagesFolder(settings);
                }

                // Resolve the packages directory
                _log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_UsingPackagesDirectory,
                    request.PackagesDirectory));

                if (externalProjects != null)
                {
                    foreach (var externalReference in externalProjects)
                    {
                        var dirName = Path.GetDirectoryName(externalReference);
                        var specPath = Path.Combine(dirName, PackageSpec.PackageSpecFileName);

                        request.ExternalProjects.Add(
                            new ExternalProjectReference(
                                externalReference,
                                JsonPackageSpecReader.GetPackageSpec(externalReference, specPath),
                                msbuildProjectPath: null,
                                projectReferences: Enumerable.Empty<string>()));
                    }
                }

                // Runtime ids
                request.RequestedRuntimes.UnionWith(runtime.Values);

                var runtimeEnvironment = PlatformServices.Default.Runtime;

                var defaultRuntimes = RequestRuntimeUtility.GetDefaultRestoreRuntimes(
                    runtimeEnvironment.OperatingSystem,
                    runtimeEnvironment.GetRuntimeOsName());

                request.FallbackRuntimes.UnionWith(defaultRuntimes);

                // Run the restore
                if (parallel.HasValue())
                {
                    int parallelDegree;
                    if (string.Equals(parallel.Value(), "none", StringComparison.OrdinalIgnoreCase))
                    {
                        request.MaxDegreeOfConcurrency = 1;
                    }
                    else if (int.TryParse(parallel.Value(), out parallelDegree))
                    {
                        request.MaxDegreeOfConcurrency = parallelDegree;
                    }
                }
                if (request.MaxDegreeOfConcurrency <= 1)
                {
                    _log.LogInformation(Strings.Log_RunningNonParallelRestore);
                }
                else
                {
                    _log.LogInformation(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_RunningParallelRestore,
                        request.MaxDegreeOfConcurrency));
                }
                var command = new RestoreCommand(_log, request);
                var sw = Stopwatch.StartNew();
                var result = await command.ExecuteAsync();

                // Commit the result
                _log.LogInformation(Strings.Log_Committing);
                result.Commit(_log);

                sw.Stop();

                if (result.Success)
                {
                    _log.LogInformation(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_RestoreComplete,
                        sw.ElapsedMilliseconds));
                    return 0;
                }
                else
                {
                    _log.LogInformation(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_RestoreFailed,
                        sw.ElapsedMilliseconds));

                    return 1;
                }
            }
        }

        /// <summary>
        /// Returns a unique list of sources. New sources will be cached
        /// and shared between restores.
        /// </summary>
        private static List<SourceRepository> GetSources(
            CommandOption sources,
            CommandOption fallBack,
            ISettings settings)
        {

            // CommandLineSourceRepositoryProvider caches repositories to avoid duplicates
            var packageSourceProvider = new PackageSourceProvider(settings);

            // Take the passed in sources
            var packageSources = sources.Values.Select(s => new PackageSource(s));

            // If no sources were passed in use the NuGet.Config sources
            if (!packageSources.Any())
            {
                // Add enabled sources
                packageSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            }

            packageSources = packageSources.Concat(
                fallBack.Values.Select(s => new PackageSource(s)));

            return packageSources.Select(source => _sourceProvider.CreateRepository(source))
                .Distinct()
                .ToList();
        }

        // Create a caching source provider with the default settings, the sources will be passed in
        private static CachingSourceProvider _sourceProvider = new CachingSourceProvider(
            new PackageSourceProvider(
                Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null)));
    }
}
