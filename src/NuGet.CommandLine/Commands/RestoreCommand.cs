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
        [Option(typeof(NuGetCommand), "RestoreCommandRequireConsent")]
        public bool RequireConsent { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandPackagesDirectory", AltName = "OutputDirectory")]
        public string PackagesDirectory { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandSolutionDirectory")]
        public string SolutionDirectory { get; set; }

        [Option(typeof(NuGetCommand), "CommandMSBuildVersion")]
        public string MSBuildVersion { get; set; }

        [ImportingConstructor]
        public RestoreCommand()
            : base(MachineCache.Default)
        {
        }

        // The directory that contains msbuild
        private string _msbuildDirectory;

        public override async Task ExecuteCommandAsync()
        {
            bool restoreResult = true;

            _msbuildDirectory = MsBuildUtility.GetMsbuildDirectory(MSBuildVersion);

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
                var v2RestoreResult = await PerformNuGetV2RestoreAsync(restoreInputs);
                restoreResult &= v2RestoreResult;
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

                var v3RestoreTasks = new List<Task<bool>>();
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
                    var results = await Task.WhenAll(v3RestoreTasks);
                    restoreResult &= results.All(r => r);
                }
            }

            if (!restoreResult)
            {
                throw new CommandLineException();
            }
        }

        private static string GetEffectiveGlobalPackagesFolder(
            string packagesDirectoryParameter,
            string solutionDirectoryParameter,
            PackageRestoreInputs packageRestoreInputs,
            string globalPackagesFolder)
        {
            // Return the -PackagesDirectory parameter if specified
            if (!string.IsNullOrEmpty(packagesDirectoryParameter))
            {
                return packagesDirectoryParameter;
            }

            // Return the globalPackagesFolder as-is if it is a full path
            if (Path.IsPathRooted(globalPackagesFolder))
            {
                return globalPackagesFolder;
            }
            else if (!string.IsNullOrEmpty(solutionDirectoryParameter)
                || packageRestoreInputs.RestoringWithSolutionFile)
            {
                var solutionDirectory = packageRestoreInputs.RestoringWithSolutionFile ?
                    packageRestoreInputs.DirectoryOfSolutionFile :
                    solutionDirectoryParameter;

                // -PackagesDirectory parameter was not provided and globalPackagesFolder is a relative path.
                // Use the solutionDirectory to construct the full path
                return Path.Combine(solutionDirectory, globalPackagesFolder);
            }

            // -PackagesDirectory parameter was not provided and globalPackagesFolder is a relative path.
            // solution directory is not available either. Throw
            var message = string.Format(
                CultureInfo.CurrentCulture,
                LocalizedResourceManager.GetString("RestoreCommandCannotDetermineGlobalPackagesFolder"));

            throw new CommandLineException(message);
        }

        private async Task<bool> PerformNuGetV3RestoreAsync(string packagesDir, string inputPath)
        {
            var inputFileName = Path.GetFileName(inputPath);
            PackageSpec packageSpec;
            string projectJsonPath = null;

            // Determine the type of the input and restore it appropriately
            // Inputs can be: project.json files or msbuild project files

            IEnumerable<string> externalProjects = null;
            if (BuildIntegratedProjectUtility.IsProjectConfig(inputPath))
            {
                // Restore a project.json file using the directory as the Id
                Console.LogVerbose($"Reading project file {Arguments[0]}");
                var projectDirectory = Path.GetDirectoryName(inputPath);

                projectJsonPath = inputPath;

                packageSpec = JsonPackageSpecReader.GetPackageSpec(
                    File.ReadAllText(projectJsonPath),
                    Path.GetFileName(projectDirectory),
                    projectJsonPath);
            }
            else if (MsBuildUtility.IsMsBuildBasedProject(inputPath))
            {
                // Restore a .csproj or other msbuild project file using the 
                // file name without the extension as the Id
                externalProjects = MsBuildUtility.GetProjectReferences(_msbuildDirectory, inputPath);

                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(inputPath));
                var projectName = Path.GetFileNameWithoutExtension(inputPath);
                projectJsonPath = BuildIntegratedProjectUtility.GetProjectConfigPath(projectDirectory, projectName);

                Console.LogVerbose($"Reading project file {inputPath}");

                packageSpec = JsonPackageSpecReader.GetPackageSpec(
                    File.ReadAllText(projectJsonPath),
                    projectName,
                    projectJsonPath);
            }
            else
            {
                // Restore an unknown file type using the file name
                // without the extension as the Id
                var projectName = Path.GetFileNameWithoutExtension(inputPath);
                projectJsonPath = BuildIntegratedProjectUtility.GetProjectConfigPath(inputPath, projectName);

                Console.LogVerbose($"Reading project file {projectJsonPath}");

                packageSpec = JsonPackageSpecReader.GetPackageSpec(
                    File.ReadAllText(projectJsonPath),
                    projectName,
                    projectJsonPath);
            }

            Console.LogVerbose($"Loaded project {packageSpec.Name} from {packageSpec.FilePath}");

            // Resolve the root directory
            var rootDirectory = PackageSpecResolver.ResolveRootDirectory(inputPath);
            Console.LogVerbose($"Found project root directory: {rootDirectory}");

            Console.LogVerbose($"Using packages directory: {packagesDir}");

            var packageSources = GetPackageSources(Settings);
            var request = new RestoreRequest(
                packageSpec,
                packageSources);

            request.PackagesDirectory = packagesDir;

            if (DisableParallelProcessing)
            {
                request.MaxDegreeOfConcurrency = 1;
            }
            else
            {
                request.MaxDegreeOfConcurrency = PackageManagementConstants.DefaultMaxDegreeOfParallelism;
            }

            request.CacheContext.NoCache = NoCache;

            // Read the existing lock file, this is needed to support IsLocked=true
            var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(projectJsonPath);
            request.LockFilePath = lockFilePath;
            request.ExistingLockFile = BuildIntegratedRestoreUtility.GetLockFile(lockFilePath, Console);

            // Resolve the packages directory
            Console.LogVerbose($"Using packages directory: {request.PackagesDirectory}");

            if (externalProjects != null)
            {
                foreach (var externalReference in externalProjects)
                {
                    var projectDir = Path.GetDirectoryName(externalReference);
                    var projectName = Path.GetFileNameWithoutExtension(externalReference);
                    var childProjectJson =
                        BuildIntegratedProjectUtility.GetProjectConfigPath(projectDir, projectName);

                    Debug.Assert(childProjectJson != null && File.Exists(childProjectJson), childProjectJson);

                    request.ExternalProjects.Add(
                        new ExternalProjectReference(
                            externalReference,
                            childProjectJson,
                            projectReferences: Enumerable.Empty<string>()));
                }
            }

            CheckRequireConsent();

            // Run the restore
            var command = new Commands.RestoreCommand(Console, request);
            var result = await command.ExecuteAsync();
            result.Commit(Console);

            return result.Success;
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

        private async Task<bool> PerformNuGetV2RestoreAsync(PackageRestoreInputs packageRestoreInputs)
        {
            ReadSettings(packageRestoreInputs);
            var packagesFolderPath = GetPackagesFolder(packageRestoreInputs);

            var sourceRepositoryProvider = new SourceRepositoryProvider(SourceProvider,
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
                return true;
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
            var result = await PackageRestoreManager.RestoreMissingPackagesAsync(
                packageRestoreContext,
                new ConsoleProjectContext(Console));

            foreach (var item in bag)
            {
                Console.WriteError(item.Exception.Message);
            }

            return result.Restored;
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
                // An argument was passed in. It might be a solution file, 
                // project file, project.json, or packages.config file

                var projectFilePath = Path.GetFullPath(Arguments.First());
                var projectFileName = Path.GetFileName(projectFilePath);

                if (BuildIntegratedProjectUtility.IsProjectConfig(projectFileName))
                {
                    // project.json or projName.project.json
                    packageRestoreInputs.V3RestoreFiles.Add(projectFilePath);
                }
                else if (string.Equals(projectFileName, Constants.PackageReferenceFile)
                    || (projectFileName.StartsWith("packages.", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        Path.GetExtension(projectFileName),
                        Path.GetExtension(Constants.PackageReferenceFile), StringComparison.OrdinalIgnoreCase)))
                {
                    // restoring from packages.config or packages.projectname.config file
                    packageRestoreInputs.PackageReferenceFiles.Add(projectFilePath);
                }
                else if (MsBuildUtility.IsMsBuildBasedProject(projectFileName))
                {
                    // For msbuild files find the project.json or packages.config file,
                    // if neither exist skip it

                    var projectName = Path.GetFileNameWithoutExtension(projectFileName);
                    var dir = Path.GetDirectoryName(projectFilePath);

                    var projectJsonPath = BuildIntegratedProjectUtility.GetProjectConfigPath(dir, projectName);
                    var packagesConfigPath = GetPackageReferenceFile(projectFilePath);

                    // Check for project.json
                    if (File.Exists(projectJsonPath))
                    {
                        packageRestoreInputs.V3RestoreFiles.Add(projectFilePath);
                    }
                    else if (File.Exists(packagesConfigPath))
                    {
                        // Check for packages.config
                        packageRestoreInputs.PackageReferenceFiles.Add(packagesConfigPath);
                    }
                }
                else
                {
                    // Check if it is a solution file
                    var solutionFileFullPath = GetSolutionFile(projectFilePath);
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

            var projectFiles = MsBuildUtility.GetAllProjectFileNames(solutionFileFullPath, _msbuildDirectory);
            foreach (var projectFile in projectFiles)
            {
                if (!File.Exists(projectFile))
                {
                    continue;
                }

                // packages.config
                var packagesConfigFilePath = GetPackageReferenceFile(projectFile);

                // project.json
                var dir = Path.GetDirectoryName(projectFile);
                var projectName = Path.GetFileNameWithoutExtension(projectFile);
                var projectJsonPath = BuildIntegratedProjectUtility.GetProjectConfigPath(dir, projectName);

                // project.json overrides packages.config
                if (File.Exists(projectJsonPath))
                {
                    restoreInputs.V3RestoreFiles.Add(projectFile);
                }
                else if (File.Exists(packagesConfigFilePath))
                {
                    restoreInputs.PackageReferenceFiles.Add(packagesConfigFilePath);
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
