using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.CommandLine;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Strawman.Commands;
using System;
using System.IO;
using System.Linq;
using Microsoft.Framework.Logging.Console;

namespace NuGet.CommandLine
{
    public class Program
    {
        private readonly IApplicationEnvironment _applicationEnvironment;
        private ILogger _log;

        public Program(IApplicationEnvironment applicationEnvironment)
        {
            _applicationEnvironment = applicationEnvironment;
        }

        public int Main(string[] args)
        {
            // Set up logging
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddConsole(LogLevel.Verbose);
            _log = loggerFactory.CreateLogger<Program>();

            var app = new CommandLineApplication();
            app.HelpOption("-h|--help");
            app.VersionOption("--version", _applicationEnvironment.Version);

            app.Command("restore", restore =>
            {
                var sources = restore.Option("-s|--source <source>", "Specifies a NuGet package source to use during the restore", CommandOptionType.MultipleValue);
                var projectFile = restore.Argument("[project file]", "The path to the project to restore for, either a project.json or the directory containing it. Defaults to the current directory");

                // Figure out the project directory
                PackageSpec project;
                string projectDirectory = projectFile.Value ?? Environment.CurrentDirectory;
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
                var packagesDirectory = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".dnx", "packages2");
                _log.LogVerbose($"Using packages directory: {packagesDirectory}");

                // Run the restore
                var request = new RestoreRequest(
                    project,
                    sources.Values.Select(s => new PackageSource(s)),
                    packagesDirectory);
                var command = new RestoreCommand(loggerFactory);
                var result = command.ExecuteAsync(request).Result;

                _log.LogInformation("Restore completed!");
            });

            return app.Execute(args);
        }
    }
}
