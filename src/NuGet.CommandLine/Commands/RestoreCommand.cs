using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "restore", "RestoreCommandDescription",
        MinArgs = 0, MaxArgs = 1, UsageSummaryResourceName = "RestoreCommandUsageSummary",
        UsageDescriptionResourceName = "RestoreCommandUsageDescription",
        UsageExampleResourceName = "RestoreCommandUsageExamples")]
    public class RestoreCommand : DownloadCommandBase
    {
        // True means we're restoring for a solution; False means we're restoring packages
        // listed in a packages.config file.
        private bool _restoringForSolution;

        private string _solutionFileFullPath;
        private string _packagesConfigFileFullPath;

        [Option(typeof(NuGetCommand), "RestoreCommandRequireConsent")]
        public bool RequireConsent { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandPackagesDirectory", AltName = "OutputDirectory")]
        public string PackagesDirectory { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandSolutionDirectory")]
        public string SolutionDirectory { get; set; }

        /// <remarks>
        /// Meant for unit testing.
        /// </remarks>
        internal bool RestoringForSolution
        {
            get { return _restoringForSolution; }
        }

        /// <remarks>
        /// Meant for unit testing.
        /// </remarks>
        internal string SolutionFileFullPath
        {
            get { return _solutionFileFullPath; }
        }

        /// <remarks>
        /// Meant for unit testing.
        /// </remarks>
        internal string PackagesConfigFileFullPath
        {
            get { return _packagesConfigFileFullPath; }
        }

        [ImportingConstructor]
        public RestoreCommand()
            : base(MachineCache.Default)
        {
        }

        public override Task ExecuteCommandAsync()
        {
            var projectFilePath = Path.GetFullPath(Arguments.FirstOrDefault() ?? ".");
            var projectFileName = Path.GetFileName(projectFilePath);
            if (string.Equals(PackageSpec.PackageSpecFileName, projectFileName, StringComparison.OrdinalIgnoreCase))
            {
                return PerformNuGetV3RestoreAsync(projectFilePath);
            }
            else
            {
                return PerformNuGetV2RestoreAsync();
            }
        }

        private async Task PerformNuGetV3RestoreAsync(string projectPath)
        {
            var projectFileName = Path.GetFileName(projectPath);
            PackageSpec project;
            IEnumerable<string> externalProjects = null;
            if (string.Equals(PackageSpec.PackageSpecFileName, projectFileName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogVerbose($"Reading project file {Arguments[0]}");
                var projectDirectory = Path.GetDirectoryName(projectPath);
                project = JsonPackageSpecReader.GetPackageSpec(
                    File.ReadAllText(projectPath),
                    Path.GetFileName(projectDirectory),
                    projectPath);
            }
            else if (MsBuildUtility.IsMsBuildBasedProject(projectPath))
            {
                externalProjects = MsBuildUtility.GetProjectReferences(projectPath);

                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
                var packageSpecFile = Path.Combine(projectDirectory, PackageSpec.PackageSpecFileName);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(packageSpecFile), projectPath, projectPath);
                Logger.LogVerbose($"Reading project file {projectPath}");
            }
            else
            {
                var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                Logger.LogVerbose($"Reading project file {file}");
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectPath), file);
            }
            Logger.LogVerbose($"Loaded project {project.Name} from {project.FilePath}");

            // Resolve the root directory
            var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectPath);
            Logger.LogVerbose($"Found project root directory: {rootDirectory}");

            // Resolve the packages directory
            var packagesDir = !string.IsNullOrEmpty(PackagesDirectory) ?
                PackagesDirectory :
                Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), ".nuget", "packages");
            Logger.LogVerbose($"Using packages directory: {packagesDir}");

            ReadSettings();

            var packageSources = GetPackageSources(Settings);
            var request = new RestoreRequest(
                project,
                packageSources);

            if (!string.IsNullOrEmpty(PackagesDirectory))
            {
                request.PackagesDirectory = PackagesDirectory;
            }
            else
            {
                request.PackagesDirectory = SettingsUtility.GetGlobalPackagesFolder(Settings);
            }

            if (DisableParallelProcessing)
            {
                request.MaxDegreeOfConcurrency = 1;
            }
            request.NoCache = NoCache;

            // Resolve the packages directory
            Logger.LogVerbose($"Using packages directory: {request.PackagesDirectory}");

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
            var command = new Commands.RestoreCommand(Logger, request);
            var result = await command.ExecuteAsync();
            result.Commit(Logger);
        }

        protected void ReadSettings()
        {
            if (_restoringForSolution || !String.IsNullOrEmpty(SolutionDirectory))
            {
                var solutionDirectory = _restoringForSolution ?
                    Path.GetDirectoryName(_solutionFileFullPath) :
                    SolutionDirectory;

                // Read the solution-level settings
                var solutionSettingsFile = Path.Combine(
                    solutionDirectory,
                    NuGetConstants.NuGetSolutionSettingsFolder);
                if (ConfigFile != null)
                {
                    ConfigFile = Path.GetFullPath(ConfigFile);
                }

                Settings = Configuration.Settings.LoadDefaultSettings(
                    solutionSettingsFile,
                    configFileName: ConfigFile,
                    machineWideSettings: MachineWideSettings);

                // Recreate the source provider and credential provider
                SourceProvider = PackageSourceBuilder.CreateSourceProvider(Settings);
                HttpClient.DefaultCredentialProvider = new SettingsCredentialProvider(new ConsoleCredentialProvider(Console), SourceProvider, Console);
            }       
        } 

        private Task PerformNuGetV2RestoreAsync()
        {
            DetermineRestoreMode();
            ReadSettings();
            var packagesFolderPath = GetPackagesFolder();

            var packageSourceProvider = new Configuration.PackageSourceProvider(Settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider,
                Enumerable.Concat(
                    Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider),
                    Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider)));
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, Settings, packagesFolderPath);

            IEnumerable<Packaging.PackageReference> installedPackageReferences;
            if (_restoringForSolution)
            {
                installedPackageReferences = GetInstalledPackageReferencesFromSolutionFile(_solutionFileFullPath);
            }
            else
            {
                // By default the PackageReferenceFile does not throw if the file does not exist at the specified path.
                // So we'll need to verify that the file exists.
                if (!File.Exists(_packagesConfigFileFullPath))
                {
                    string message = String.Format(CultureInfo.CurrentCulture, "RestoreCommandFileNotFound", _packagesConfigFileFullPath);
                    throw new InvalidOperationException(message);
                }

                installedPackageReferences = GetInstalledPackageReferences(_packagesConfigFileFullPath);
            }

            var packageRestoreData = installedPackageReferences.Select(reference =>
                new PackageRestoreData(
                    reference,
                    new[] { _solutionFileFullPath ?? _packagesConfigFileFullPath },
                    isMissing: true));
            var packageSources = GetPackageSources(Settings)
                .Select(sourceRepositoryProvider.CreateRepository);
            var packageRestoreContext = new PackageRestoreContext(
                nuGetPackageManager, 
                packageRestoreData, 
                CancellationToken.None,
                packageRestoredEvent: null,
                packageRestoreFailedEvent: null,
                sourceRepositories: packageSources,
                maxNumberOfParallelTasks: PackageManagementConstants.DefaultMaxDegreeOfParallelism);

            return PackageRestoreManager.RestoreMissingPackagesAsync(packageRestoreContext, new ConsoleProjectContext(Logger));
        }

        internal void DetermineRestoreMode()
        {
            if (Arguments.Count == 0)
            {
                // look for solution files first
                _solutionFileFullPath = GetSolutionFile(Directory.GetCurrentDirectory());
                if (_solutionFileFullPath != null)
                {
                    _restoringForSolution = true;
                    if (Verbosity == Verbosity.Detailed)
                    {
                        Console.WriteLine(LocalizedResourceManager.GetString("RestoreCommandRestoringPackagesForSolution"), _solutionFileFullPath);
                    }

                    return;
                }

                // look for packages.config file
                if (File.Exists(Constants.PackageReferenceFile))
                {
                    _restoringForSolution = false;
                    _packagesConfigFileFullPath = Path.GetFullPath(Constants.PackageReferenceFile);
                    if (Verbosity == NuGet.Verbosity.Detailed)
                    {
                        Console.WriteLine(LocalizedResourceManager.GetString("RestoreCommandRestoringPackagesFromPackagesConfigFile"));
                    }

                    return;
                }

                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_NoSolutionFileNorePackagesConfigFile"));
            }
            else
            {
                if (Path.GetFileName(Arguments[0]).Equals(Constants.PackageReferenceFile, StringComparison.OrdinalIgnoreCase))
                {
                    // restoring from packages.config file
                    _restoringForSolution = false;
                    _packagesConfigFileFullPath = Path.GetFullPath(Arguments[0]);
                }
                else
                {
                    _restoringForSolution = true;
                    _solutionFileFullPath = GetSolutionFile(Arguments[0]);
                    if (_solutionFileFullPath == null)
                    {
                        throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_CannotLocateSolutionFile"));
                    }
                }
            }
        }

        /// <summary>
        /// Gets the solution file, in full path format. If <paramref name="solutionFileOrDirectory"/> is a file, 
        /// that file is returned. Otherwise, searches for a *.sln file in
        /// directory <paramref name="solutionFileOrDirectory"/>. If exactly one sln file is found, 
        /// that file is returned. If multiple sln files are found, an exception is thrown. 
        /// If no sln files are found, returns null.
        /// </summary>
        /// <param name="solutionFileOrDirectory">The solution file or directory to search for solution files.</param>
        /// <returns>The full path of the solution file. Or null if no solution file can be found.</returns>
        private string GetSolutionFile(string solutionFileOrDirectory)
        {
            if (File.Exists(solutionFileOrDirectory))
            {
                return Path.GetFullPath(solutionFileOrDirectory);
            }

            // look for solution files
            var slnFiles = Directory.GetFiles(solutionFileOrDirectory, "*.sln");
            if (slnFiles.Length > 1)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_MultipleSolutions"));
            }

            if (slnFiles.Length == 1)
            {
                return Path.GetFullPath(slnFiles[0]);
            }

            return null;
        }

        private string GetPackagesFolder()
        {
            if (!String.IsNullOrEmpty(PackagesDirectory))
            {
                return PackagesDirectory;
            }

            var repositoryPath = SettingsUtility.GetRepositoryPath(Settings);
            if (!String.IsNullOrEmpty(repositoryPath))
            {
                return repositoryPath;
            }

            if (!String.IsNullOrEmpty(SolutionDirectory))
            {
                return Path.Combine(SolutionDirectory, CommandLineConstants.PackagesDirectoryName);
            }

            if (_restoringForSolution)
            {
                return Path.Combine(Path.GetDirectoryName(_solutionFileFullPath), CommandLineConstants.PackagesDirectoryName);
            }

            throw new InvalidOperationException(LocalizedResourceManager.GetString("RestoreCommandCannotDeterminePackagesFolder"));
        }

        private static string ConstructPackagesConfigFromProjectName(string projectName)
        {
            // we look for packages.<project name>.config file
            // but we don't want any space in the file name, so convert it to underscore.
            return "packages." + projectName.Replace(' ', '_') + ".config";
        }

        // returns the package reference file associated with the project
        private string GetPackageReferenceFile(string projectFile)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            string pathWithProjectName =Path.Combine(
                Path.GetDirectoryName(projectFile),
                ConstructPackagesConfigFromProjectName(projectName));
            if (File.Exists(pathWithProjectName))
            {
                return pathWithProjectName;
            }

            return Path.Combine(
                Path.GetDirectoryName(projectFile),
                Constants.PackageReferenceFile);
        }

        private IEnumerable<Packaging.PackageReference> GetInstalledPackageReferencesFromSolutionFile(string solutionFileFullPath)
        {
            var installedPackageReferences = new HashSet<Packaging.PackageReference>(new PackageReferenceComparer());
            IEnumerable<string> projectFiles = MsBuildUtility.GetAllProjectFileNames(solutionFileFullPath);

            foreach (var projectFile in projectFiles)
            {
                if (!File.Exists(projectFile))
                {
                    continue;
                }

                var projectConfigFilePath = GetPackageReferenceFile(projectFile);
                installedPackageReferences.AddRange(GetInstalledPackageReferences(projectConfigFilePath));
            }

            return installedPackageReferences;
        }
    }
}
