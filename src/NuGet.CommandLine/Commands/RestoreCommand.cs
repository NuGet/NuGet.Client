using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
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
        [Option(typeof(NuGetCommand), "RestoreCommandRequireConsent")]
        public bool RequireConsent { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandPackagesDirectory", AltName = "OutputDirectory")]
        public string PackagesDirectory { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandSolutionDirectory")]
        public string SolutionDirectory { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandMsBuildPath")]
        public string MsBuildPath { get; set; }

        [ImportingConstructor]
        public RestoreCommand()
            : base(MachineCache.Default)
        {
        }

        public override async Task ExecuteCommandAsync()
        {
            if (!string.IsNullOrEmpty(PackagesDirectory))
            {
                PackagesDirectory = Path.GetFullPath(PackagesDirectory);
            }

            if (!string.IsNullOrEmpty(SolutionDirectory))
            {
                SolutionDirectory = Path.GetFullPath(SolutionDirectory);
            }

            var restoreInputs = DetermineRestoreInputs();
            if (restoreInputs.PackageReferenceFiles.Count > 0)
            {
                await PerformNuGetV2RestoreAsync(restoreInputs);
            }

            if (restoreInputs.V3RestoreFiles.Count > 0)
            {
                // Read the settings outside of parallel loops.
                ReadSettings(restoreInputs);

                // Resolve the packages directory
                var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(Settings);
                var packagesDir = GetEffectiveGlobalPackagesFolder(
                                    PackagesDirectory,
                                    SolutionDirectory,
                                    restoreInputs,
                                    globalPackagesFolder);

                var v3RestoreTasks = new List<Task>();
                foreach (var file in restoreInputs.V3RestoreFiles)
                {
                    if (DisableParallelProcessing)
                    {
                        await PerformNuGetV3RestoreAsync(packagesDir, file);
                    }
                    else
                    {
                        v3RestoreTasks.Add(PerformNuGetV3RestoreAsync(packagesDir, file));
                    }
                }

                if (v3RestoreTasks.Count > 0)
                {
                    await Task.WhenAll(v3RestoreTasks);
                }
            }
        }

        private static string GetEffectiveGlobalPackagesFolder(
            string packagesDirectoryParameter,
            string solutionDirectoryParameter,
            PackageRestoreInputs packageRestoreInputs,
            string globalPackagesFolder)
        {
            if (!string.IsNullOrEmpty(packagesDirectoryParameter))
            {
                return packagesDirectoryParameter;
            }

            if (!string.IsNullOrEmpty(solutionDirectoryParameter) || packageRestoreInputs.RestoringWithSolutionFile)
            {
                var solutionDirectory = packageRestoreInputs.RestoringWithSolutionFile ?
                    packageRestoreInputs.DirectoryOfSolutionFile :
                    solutionDirectoryParameter;

                return Path.Combine(solutionDirectory, globalPackagesFolder);
            }

            // If PackagesDirectory parameter is not provided,
            //    solution directory is not available, and,
            //    globalPackagesFolder setting is a relative path,
            // Throw
            if (!Path.IsPathRooted(globalPackagesFolder))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("RestoreCommandCannotDetermineGlobalPackagesFolder"));

                throw new CommandLineException(message);
            }

            return globalPackagesFolder;
        }

        private async Task PerformNuGetV3RestoreAsync(string packagesDir, string projectPath)
        {
            var projectFileName = Path.GetFileName(projectPath);
            PackageSpec project;
            IEnumerable<string> externalProjects = null;
            if (string.Equals(PackageSpec.PackageSpecFileName, projectFileName, StringComparison.OrdinalIgnoreCase))
            {
                Console.LogVerbose($"Reading project file {Arguments[0]}");
                var projectDirectory = Path.GetDirectoryName(projectPath);
                project = JsonPackageSpecReader.GetPackageSpec(
                    File.ReadAllText(projectPath),
                    Path.GetFileName(projectDirectory),
                    projectPath);
            }
            else if (MsBuildUtility.IsMsBuildBasedProject(projectPath))
            {
                externalProjects = MsBuildUtility.GetProjectReferences(MsBuildPath, projectPath);

                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
                var packageSpecFile = Path.Combine(projectDirectory, PackageSpec.PackageSpecFileName);
                project = JsonPackageSpecReader.GetPackageSpec(
                    File.ReadAllText(packageSpecFile), projectPath, projectPath);
                Console.LogVerbose($"Reading project file {projectPath}");
            }
            else
            {
                var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                Console.LogVerbose($"Reading project file {file}");
                project = JsonPackageSpecReader.GetPackageSpec(
                    File.ReadAllText(file), Path.GetFileName(projectPath), file);
            }
            Console.LogVerbose($"Loaded project {project.Name} from {project.FilePath}");

            // Resolve the root directory
            var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectPath);
            Console.LogVerbose($"Found project root directory: {rootDirectory}");

            Console.LogVerbose($"Using packages directory: {packagesDir}");

            var packageSources = GetPackageSources(Settings);
            var request = new RestoreRequest(
                project,
                packageSources);

            request.PackagesDirectory = packagesDir;

            if (DisableParallelProcessing)
            {
                request.MaxDegreeOfConcurrency = 1;
            }
            request.NoCache = NoCache;

            // Resolve the packages directory
            Console.LogVerbose($"Using packages directory: {request.PackagesDirectory}");

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

            CheckRequireConsent();

            // Run the restore
            var command = new Commands.RestoreCommand(Console, request);
            var result = await command.ExecuteAsync();
            result.Commit(Console);
        }

        private void ReadSettings(PackageRestoreInputs packageRestoreInputs)
        {
            if (!string.IsNullOrEmpty(SolutionDirectory) || packageRestoreInputs.RestoringWithSolutionFile)
            {
                var solutionDirectory = packageRestoreInputs.RestoringWithSolutionFile ?
                    packageRestoreInputs.DirectoryOfSolutionFile :
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
                HttpClient.DefaultCredentialProvider =
                    new SettingsCredentialProvider(new ConsoleCredentialProvider(Console), SourceProvider, Console);
            }
        }

        private async Task PerformNuGetV2RestoreAsync(PackageRestoreInputs packageRestoreInputs)
        {
            ReadSettings(packageRestoreInputs);
            var packagesFolderPath = GetPackagesFolder(packageRestoreInputs);

            var packageSourceProvider = new Configuration.PackageSourceProvider(Settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider,
                Enumerable.Concat(
                    Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider),
                    Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider)));
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, Settings, packagesFolderPath);

            var installedPackageReferences = new HashSet<Packaging.PackageReference>(new PackageReferenceComparer());
            if (packageRestoreInputs.RestoringWithSolutionFile)
            {
                installedPackageReferences.AddRange(packageRestoreInputs
                    .PackageReferenceFiles
                    .SelectMany(GetInstalledPackageReferences));
            }
            else if (packageRestoreInputs.PackageReferenceFiles.Count > 0)
            {
                // By default the PackageReferenceFile does not throw
                // if the file does not exist at the specified path.
                // So we'll need to verify that the file exists.
                Debug.Assert(packageRestoreInputs.PackageReferenceFiles.Count == 1,
                    "Only one packages.config file is allowed to be specified " +
                    "at a time when not performing solution restore.");

                var packageReferenceFile = packageRestoreInputs.PackageReferenceFiles[0];
                if (!File.Exists(packageReferenceFile))
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        "RestoreCommandFileNotFound",
                        packageReferenceFile);

                    throw new InvalidOperationException(message);
                }

                installedPackageReferences.AddRange(GetInstalledPackageReferences(packageReferenceFile));
            }

            var missingPackageReferences = installedPackageReferences.Where(reference =>
                !nuGetPackageManager.PackageExistsInPackagesFolder(reference.PackageIdentity)).ToArray();

            if (missingPackageReferences.Length == 0)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("InstallCommandNothingToInstall"),
                    "packages.config");

                Console.LogInformation(message);
                return;
            }

            var packageRestoreData = missingPackageReferences.Select(reference =>
                new PackageRestoreData(
                    reference,
                    new[] { packageRestoreInputs.RestoringWithSolutionFile
                                ? packageRestoreInputs.DirectoryOfSolutionFile
                                : packageRestoreInputs.PackageReferenceFiles[0] },
                    isMissing: true));
            var packageSources = GetPackageSources(Settings);

            var repositories = packageSources
                .Select(sourceRepositoryProvider.CreateRepository)
                .ToArray();

            var bag = new ConcurrentBag<PackageRestoreFailedEventArgs>();

            var packageRestoreContext = new PackageRestoreContext(
                nuGetPackageManager,
                packageRestoreData,
                CancellationToken.None,
                packageRestoredEvent: null,
                packageRestoreFailedEvent: (sender, args) => { bag.Add(args); },
                sourceRepositories: repositories,
                maxNumberOfParallelTasks: DisableParallelProcessing
                        ? 1
                        : PackageManagementConstants.DefaultMaxDegreeOfParallelism);

            CheckRequireConsent();
            await PackageRestoreManager.RestoreMissingPackagesAsync(
                packageRestoreContext,
                new ConsoleProjectContext(Console));

            foreach(var item in bag)
            {
                Console.WriteError(item.Exception.Message);
            }
        }

        private void CheckRequireConsent()
        {
            if (RequireConsent)
            {
                var packageRestoreConsent = new PackageRestoreConsent(new SettingsToLegacySettings(Settings));

                if (packageRestoreConsent.IsGranted)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("RestoreCommandPackageRestoreOptOutMessage"),
                        NuGet.Resources.NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));

                    Console.LogInformation(message);
                }
                else
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("InstallCommandPackageRestoreConsentNotFound"),
                        NuGet.Resources.NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));

                    throw new CommandLineException(message);
                }
            }
        }

        private PackageRestoreInputs DetermineRestoreInputs()
        {
            var packageRestoreInputs = new PackageRestoreInputs();
            if (Arguments.Count == 0)
            {
                // look for solution files first
                var solutionFileFullPath = GetSolutionFile(Directory.GetCurrentDirectory());
                if (solutionFileFullPath != null)
                {
                    if (Verbosity == Verbosity.Detailed)
                    {
                        Console.WriteLine(
                            LocalizedResourceManager.GetString("RestoreCommandRestoringPackagesForSolution"),
                            solutionFileFullPath);
                    }

                    ProcessSolutionFile(solutionFileFullPath, packageRestoreInputs);
                }
                else if (File.Exists(Constants.PackageReferenceFile)) // look for packages.config file
                {
                    var packagesConfigFileFullPath = Path.GetFullPath(Constants.PackageReferenceFile);
                    if (Verbosity == NuGet.Verbosity.Detailed)
                    {
                        Console.WriteLine(
                            LocalizedResourceManager.GetString(
                                "RestoreCommandRestoringPackagesFromPackagesConfigFile"));
                    }

                    packageRestoreInputs.PackageReferenceFiles.Add(packagesConfigFileFullPath);
                }
                else
                {
                    throw new InvalidOperationException(
                        LocalizedResourceManager.GetString(
                            "Error_NoSolutionFileNorePackagesConfigFile"));
                }
            }
            else
            {
                var projectFilePath = Path.GetFullPath(Arguments.FirstOrDefault() ?? ".");
                var projectFileName = Path.GetFileName(projectFilePath);
                if (string.Equals(
                    PackageSpec.PackageSpecFileName,
                    projectFileName,
                    StringComparison.OrdinalIgnoreCase)
                    || MsBuildUtility.IsMsBuildBasedProject(projectFileName))
                {
                    packageRestoreInputs.V3RestoreFiles.Add(projectFilePath);

                }
                else if (Path.GetFileName(Arguments[0])
                            .Equals(Constants.PackageReferenceFile, StringComparison.OrdinalIgnoreCase)
                    || (Path.GetFileName(Arguments[0]).StartsWith("packages.", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        Path.GetExtension(Arguments[0]),
                        Path.GetExtension(Constants.PackageReferenceFile), StringComparison.OrdinalIgnoreCase)))
                {
                    // restoring from packages.config or packages.projectname.config file
                    packageRestoreInputs.PackageReferenceFiles.Add(projectFilePath);
                }
                else
                {
                    var solutionFileFullPath = GetSolutionFile(Arguments[0]);
                    if (solutionFileFullPath == null)
                    {
                        throw new InvalidOperationException(
                            LocalizedResourceManager.GetString("Error_CannotLocateSolutionFile"));
                    }

                    ProcessSolutionFile(solutionFileFullPath, packageRestoreInputs);
                }
            }

            return packageRestoreInputs;
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

        private string GetPackagesFolder(PackageRestoreInputs packageRestoreInputs)
        {
            if (!string.IsNullOrEmpty(PackagesDirectory))
            {
                return PackagesDirectory;
            }

            var repositoryPath = SettingsUtility.GetRepositoryPath(Settings);
            if (!string.IsNullOrEmpty(repositoryPath))
            {
                return repositoryPath;
            }

            if (!string.IsNullOrEmpty(SolutionDirectory))
            {
                return Path.Combine(SolutionDirectory, CommandLineConstants.PackagesDirectoryName);
            }

            if (packageRestoreInputs.RestoringWithSolutionFile)
            {
                return Path.Combine(
                    packageRestoreInputs.DirectoryOfSolutionFile,
                    CommandLineConstants.PackagesDirectoryName);
            }

            throw new InvalidOperationException(
                LocalizedResourceManager.GetString("RestoreCommandCannotDeterminePackagesFolder"));
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
            var pathWithProjectName = Path.Combine(
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

        private void ProcessSolutionFile(string solutionFileFullPath, PackageRestoreInputs restoreInputs)
        {
            restoreInputs.DirectoryOfSolutionFile = Path.GetDirectoryName(solutionFileFullPath);

            var projectFiles = MsBuildUtility.GetAllProjectFileNames(solutionFileFullPath);
            foreach (var projectFile in projectFiles)
            {
                if (!File.Exists(projectFile))
                {
                    continue;
                }

                var packagesConfigFilePath = GetPackageReferenceFile(projectFile);
                var projectJsonPath = Path.Combine(
                    Path.GetDirectoryName(projectFile),
                    PackageSpec.PackageSpecFileName);

                if (File.Exists(packagesConfigFilePath))
                {
                    restoreInputs.PackageReferenceFiles.Add(packagesConfigFilePath);
                }
                else if (File.Exists(projectJsonPath))
                {
                    restoreInputs.V3RestoreFiles.Add(projectFile);
                }
            }
        }

        private class PackageRestoreInputs
        {
            public bool RestoringWithSolutionFile => !string.IsNullOrEmpty(DirectoryOfSolutionFile);

            public string DirectoryOfSolutionFile { get; set; }

            public List<string> PackageReferenceFiles { get; } = new List<string>();

            public List<string> V3RestoreFiles { get; } = new List<string>();
        }
    }
}
