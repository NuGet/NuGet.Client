// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Logging;
using NuGet.ProjectModel;

namespace NuGet.CommandLine
{
    public class Program
    {
        private ILogger _log;

        public int Main(string[] args)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                System.Diagnostics.Debugger.Launch();
            }
#endif

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = ".NET Package Manager";
            app.HelpOption("-h|--help");
            app.VersionOption("--version", GetType().GetTypeInfo().Assembly.GetName().Version.ToString());

            var verbosity = app.Option("-v|--verbosity <verbosity>", "The verbosity of logging to use. Allowed values: Debug, Verbose, Information, Warning, Error", CommandOptionType.SingleValue);

            // Set up logging
            _log = new CommandOutputLogger(verbosity);

            app.Command("restore", restore =>
                {
                    restore.Description = "Restores packages for a project and writes a lock file";

                    var sources = restore.Option(
                        "-s|--source <source>",
                        "Specifies a NuGet package source to use during the restore",
                        CommandOptionType.MultipleValue);
                    var packagesDirectory = restore.Option(
                        "--packages <packagesDirectory>",
                        "Directory to install packages in",
                        CommandOptionType.SingleValue);
                    var parallel = restore.Option(
                        "-p|--parallel <noneOrNumberOfParallelTasks>",
                        $"The number of concurrent tasks to use when restoring. Defaults to {RestoreRequest.DefaultDegreeOfConcurrency}; pass 'none' to run without concurrency.",
                        CommandOptionType.SingleValue);
                    var fallBack = restore.Option(
                        "-f|--fallbacksource <FEED>",
                        "A list of packages sources to use as a fallback",
                        CommandOptionType.MultipleValue);
                    var projectFile = restore.Argument(
                        "[project file]",
                        "The path to the project to restore for, either a project.json or the directory containing it. Defaults to the current directory");

                    restore.OnExecute(async () =>
                        {
                            // Figure out the project directory
                            IEnumerable<string> externalProjects = null;

                            PackageSpec project;
                            var projectPath = Path.GetFullPath(projectFile.Value ?? ".");
                            if (string.Equals(PackageSpec.PackageSpecFileName, Path.GetFileName(projectPath), StringComparison.OrdinalIgnoreCase))
                            {
                                _log.LogVerbose($"Reading project file {projectFile.Value}");
                                projectPath = Path.GetDirectoryName(projectPath);
                                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(projectFile.Value), Path.GetFileName(projectPath), projectFile.Value);
                            }
                            else if (MsBuildUtility.IsMsBuildBasedProject(projectPath))
                            {
#if DNXCORE50
                                throw new NotSupportedException();
#else
                                externalProjects = MsBuildUtility.GetProjectReferences(projectPath);

                                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
                                var packageSpecFile = Path.Combine(projectDirectory, PackageSpec.PackageSpecFileName);
                                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(packageSpecFile), projectPath, projectFile.Value);
                                _log.LogVerbose($"Reading project file {projectFile.Value}");
#endif
                            }
                            else
                            {
                                var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                                _log.LogVerbose($"Reading project file {file}");
                                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectPath), file);
                            }
                            _log.LogVerbose($"Loaded project {project.Name} from {project.FilePath}");

                            // Resolve the root directory
                            var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectPath);
                            _log.LogVerbose($"Found project root directory: {rootDirectory}");

                            // Resolve the packages directory
                            var packagesDir = packagesDirectory.HasValue() ?
                                packagesDirectory.Value() :
                                Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".nuget", "packages");
                            _log.LogVerbose($"Using packages directory: {packagesDir}");

                            var packageSources = sources.Values.Select(s => new PackageSource(s));
                            var settings = Settings.LoadDefaultSettings(projectPath,
                                configFileName: null,
                                machineWideSettings: null);
                            if (!packageSources.Any())
                            {
                                var packageSourceProvider = new PackageSourceProvider(settings);
                                packageSources = packageSourceProvider.LoadPackageSources();
                            }

                            packageSources = packageSources.Concat(
                                fallBack.Values.Select(s => new PackageSource(s)));

                            var request = new RestoreRequest(
                                project,
                                packageSources);

                            if (packagesDirectory.HasValue())
                            {
                                request.PackagesDirectory = packagesDirectory.Value();
                            }
                            else
                            {
                                request.PackagesDirectory = SettingsUtility.GetGlobalPackagesFolder(settings);
                            }

                            // Resolve the packages directory
                            _log.LogVerbose($"Using packages directory: {request.PackagesDirectory}");

                            if (externalProjects != null)
                            {
                                foreach (var externalReference in externalProjects)
                                {
                                    request.ExternalProjects.Add(
                                        new ExternalProjectReference(
                                            externalReference,
                                            Path.Combine(Path.GetDirectoryName(externalReference), PackageSpec.PackageSpecFileName),
                                            projectReferences: Enumerable.Empty<string>()));
                                }
                            }

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
                                _log.LogInformation("Running non-parallel restore");
                            }
                            else
                            {
                                _log.LogInformation($"Running restore with {request.MaxDegreeOfConcurrency} concurrent jobs");
                            }
                            var command = new RestoreCommand(_log, request);
                            var sw = Stopwatch.StartNew();
                            var result = await command.ExecuteAsync();
                            sw.Stop();

                            if (result.Success)
                            {
                                _log.LogInformation($"Restore completed in {sw.ElapsedMilliseconds:0.00}ms!");
                                return 0;
                            }
                            else
                            {
                                _log.LogError($"Restore failed in {sw.ElapsedMilliseconds:0.00}ms!");
                                return 1;
                            }
                        });
                });

            app.Command("diag", diag =>
                {
                    diag.Description = "Diagnostic commands for debugging package dependency graphs";
                    diag.Command("lockfile", lockfile =>
                        {
                            lockfile.Description = "Dumps data from the project lock file";

                            var project = lockfile.Option("--project <project>", "Path containing the project lockfile, or the patht to the lockfile itself", CommandOptionType.SingleValue);
                            var target = lockfile.Option("--target <target>", "View information about a specific project target", CommandOptionType.SingleValue);
                            var library = lockfile.Argument("<library>", "Optionally, get detailed information about a specific library");

                            lockfile.OnExecute(() =>
                                {
                                    var diagnostics = new DiagnosticCommands(_log);
                                    var projectFile = project.HasValue() ? project.Value() : Path.GetFullPath(".");
                                    return diagnostics.Lockfile(projectFile, target.Value(), library.Value);
                                });
                        });
                    diag.OnExecute(() =>
                        {
                            diag.ShowHelp();
                            return 0;
                        });
                });

            app.OnExecute(() =>
                {
                    app.ShowHelp();
                    return 0;
                });

            return app.Execute(args);
        }
    }
}
