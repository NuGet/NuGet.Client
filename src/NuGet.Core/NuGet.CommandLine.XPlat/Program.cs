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
using NuGet.Logging;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
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
            app.FullName = Strings.App_FullName;
            app.HelpOption("-h|--help");
            app.VersionOption("--version", GetType().GetTypeInfo().Assembly.GetName().Version.ToString());

            var verbosity = app.Option("-v|--verbosity <verbosity>", Strings.Switch_Verbosity, CommandOptionType.SingleValue);

            // Set up logging
            _log = new CommandOutputLogger(verbosity);

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
                        Strings.FormatRestore_Switch_Parallel_Description(RestoreRequest.DefaultDegreeOfConcurrency),
                        CommandOptionType.SingleValue);
                    var fallBack = restore.Option(
                        "-f|--fallbacksource <FEED>",
                        Strings.Restore_Switch_Fallback_Description,
                        CommandOptionType.MultipleValue);
                    var projectFile = restore.Argument(
                        "[project]",
                        Strings.Restore_Arg_ProjectName_Description);

                    restore.OnExecute(async () =>
                        {
                        // Figure out the project directory
                        IEnumerable<string> externalProjects = null;

                        PackageSpec project;
                        var projectPath = Path.GetFullPath(projectFile.Value ?? ".");
                        if (string.Equals(PackageSpec.PackageSpecFileName, Path.GetFileName(projectPath), StringComparison.OrdinalIgnoreCase))
                        {
                            _log.LogVerbose(Strings.FormatLog_ReadingProject(projectFile.Value));
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
                            _log.LogVerbose(Strings.FormatLog_ReadingProject(projectFile.Value));
#endif
                        }
                        else
                        {
                            var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                            _log.LogVerbose(Strings.FormatLog_ReadingProject(file));
                            project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectPath), file);
                        }
                        _log.LogVerbose(Strings.FormatLog_LoadedProject(project.Name, project.FilePath));

                        // Resolve the root directory
                        var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectPath);
                        _log.LogVerbose(Strings.FormatLog_FoundProjectRoot(rootDirectory));

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

                            using (var request = new RestoreRequest(
                                project,
                                packageSources,
                                packagesDirectory: null))
                            {

                                if (packagesDirectory.HasValue())
                                {
                                    request.PackagesDirectory = packagesDirectory.Value();
                                }
                                else
                                {
                                    request.PackagesDirectory = SettingsUtility.GetGlobalPackagesFolder(settings);
                                }

                                // Resolve the packages directory
                                _log.LogVerbose(Strings.FormatLog_UsingPackagesDirectory(request.PackagesDirectory));

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
                                    _log.LogInformation(Strings.Log_RunningNonParallelRestore);
                                }
                                else
                                {
                                    _log.LogInformation(Strings.FormatLog_RunningParallelRestore(request.MaxDegreeOfConcurrency));
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
                                    _log.LogInformation(Strings.FormatLog_RestoreComplete(sw.ElapsedMilliseconds));
                                    return 0;
                                }
                                else
                                {
                                    _log.LogInformation(Strings.FormatLog_RestoreFailed(sw.ElapsedMilliseconds));
                                    return 1;
                                }
                            }
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
