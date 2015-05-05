using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Configuration;
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
            if(args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                System.Diagnostics.Debugger.Launch();
            }
#endif

            // Set up logging
            _log = new CommandOutputLogger();

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = ".NET Package Manager";
            app.HelpOption("-h|--help");
            app.VersionOption("--version", GetType().GetTypeInfo().Assembly.GetName().Version.ToString());

            app.Command("restore", restore =>
            {
                restore.Description = "Restores packages for a project and writes a lock file";

                var sources = restore.Option("-s|--source <source>", "Specifies a NuGet package source to use during the restore", CommandOptionType.MultipleValue);
                var packagesDirectory = restore.Option("--packages <packagesDirectory>", "Directory to install packages in", CommandOptionType.SingleValue);
                var parallel = restore.Option("-p|--parallel <noneOrNumberOfParallelTasks>", $"The number of concurrent tasks to use when restoring. Defaults to {RestoreRequest.DefaultDegreeOfConcurrency}; pass 'none' to run without concurrency.", CommandOptionType.SingleValue);
                var projectFile = restore.Argument("[project file]", "The path to the project to restore for, either a project.json or the directory containing it. Defaults to the current directory");

                restore.OnExecute(async () =>
                {
                    // Figure out the project directory
                    PackageSpec project;
                    string projectDirectory = projectFile.Value ?? Path.GetFullPath(".");
                    if (string.Equals(PackageSpec.PackageSpecFileName, Path.GetFileName(projectDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        _log.LogVerbose($"Reading project file {projectFile.Value}");
                        projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectDirectory));
                        project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(projectFile.Value), Path.GetFileName(projectDirectory), projectFile.Value);
                    }
                    else
                    {
                        string file = Path.Combine(projectDirectory, PackageSpec.PackageSpecFileName);

                        _log.LogVerbose($"Reading project file {file}");
                        project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectDirectory), file);
                    }
                    _log.LogVerbose($"Loaded project {project.Name} from {project.FilePath}");

                    // Resolve the root directory
                    var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectDirectory);
                    _log.LogVerbose($"Found project root directory: {rootDirectory}");

                    // Resolve the packages directory
                    // TODO: Do this for real :) 
                    var packagesDir = packagesDirectory.HasValue() ?
                        packagesDirectory.Value() :
                        Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".dnx", "packages");
                    _log.LogVerbose($"Using packages directory: {packagesDirectory}");

                    // Run the restore
                    var request = new RestoreRequest(
                        project,
                        sources.Values.Select(s => new PackageSource(s)),
                        packagesDir);
                    if (parallel.HasValue())
                    {
                        int parallelDegree;
                        if(string.Equals(parallel.Value(), "none", StringComparison.OrdinalIgnoreCase))
                        {
                            request.MaxDegreeOfConcurrency = 1;
                        }
                        else if(int.TryParse(parallel.Value(), out parallelDegree))
                        {
                            request.MaxDegreeOfConcurrency = parallelDegree;   
                        }
                    }
                    if (request.MaxDegreeOfConcurrency <= 1)
                    {
                        _log.LogInformation("Running non-parallel restore");
                    } else
                    {
                        _log.LogInformation($"Running restore with {request.MaxDegreeOfConcurrency} concurrent jobs");
                    }
                    var command = new RestoreCommand(_log);
                    var sw = Stopwatch.StartNew();
                    var result = await command.ExecuteAsync(request);
                    sw.Stop();

                    _log.LogInformation($"Restore completed in {sw.ElapsedMilliseconds:0.00}ms!");

                    return 0;
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
                diag.OnExecute(() => { diag.ShowHelp(); return 0; });
            });

            app.OnExecute(() => { app.ShowHelp(); return 0; });

            return app.Execute(args);
        }
    }
}
