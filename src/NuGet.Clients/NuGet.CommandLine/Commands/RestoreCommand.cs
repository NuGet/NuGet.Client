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
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

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

        [Option(typeof(NuGetCommand), "RestoreCommandP2PTimeOut")]
        public int Project2ProjectTimeOut { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandPackagesDirectory", AltName = "OutputDirectory")]
        public string PackagesDirectory { get; set; }

        [Option(typeof(NuGetCommand), "RestoreCommandSolutionDirectory")]
        public string SolutionDirectory { get; set; }

        [Option(typeof(NuGetCommand), "CommandMSBuildVersion")]
        public string MSBuildVersion { get; set; }

        private static readonly int MaxDegreesOfConcurrency = Environment.ProcessorCount;

        [ImportingConstructor]
        public RestoreCommand()
            : base(MachineCache.Default)
        {
        }

        // The directory that contains msbuild
        private Lazy<string> _msbuildDirectory;

        public override async Task ExecuteCommandAsync()
        {
            CalculateEffectivePackageSaveMode();

            var restoreSummaries = new List<RestoreSummary>();

            _msbuildDirectory = new Lazy<string>(() => MsBuildUtility.GetMsbuildDirectory(MSBuildVersion, Console));

            if (!string.IsNullOrEmpty(PackagesDirectory))
            {
                PackagesDirectory = Path.GetFullPath(PackagesDirectory);
            }

            if (!string.IsNullOrEmpty(SolutionDirectory))
            {
                SolutionDirectory = Path.GetFullPath(SolutionDirectory);
            }

            var restoreInputs = DetermineRestoreInputs();

            var hasPackagesConfigFiles = restoreInputs.PackagesConfigFiles.Count > 0;
            var hasProjectJsonFiles = restoreInputs.RestoreV3Context.Inputs.Any();
            if (!hasPackagesConfigFiles && !hasProjectJsonFiles)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("RestoreCommandNoPackagesConfigOrProjectJson"));
                throw new CommandLineException(message);
            }

            // packages.config
            if (hasPackagesConfigFiles)
            {
                var v2RestoreResult = await PerformNuGetV2RestoreAsync(restoreInputs);
                restoreSummaries.Add(v2RestoreResult);
            }

            // project.json
            if (hasProjectJsonFiles)
            {
                // Read the settings outside of parallel loops.
                ReadSettings(restoreInputs);

                // Check if we can restore based on the nuget.config settings
                CheckRequireConsent();

                using (var cacheContext = new SourceCacheContext())
                {
                    var restoreContext = restoreInputs.RestoreV3Context;

                    var providerCache = new RestoreCommandProvidersCache();

                    // Add restore args to the restore context
                    cacheContext.NoCache = NoCache;
                    restoreContext.CacheContext = cacheContext;
                    restoreContext.DisableParallel = DisableParallelProcessing;
                    restoreContext.ConfigFile = ConfigFile;
                    restoreContext.MachineWideSettings = MachineWideSettings;
                    restoreContext.Sources = Source.ToList();
                    restoreContext.Log = Console;
                    restoreContext.CachingSourceProvider = GetSourceRepositoryProvider();

                    var packageSaveMode = EffectivePackageSaveMode;
                    if (packageSaveMode != Packaging.PackageSaveMode.None)
                    {
                        restoreContext.PackageSaveMode = EffectivePackageSaveMode;
                    }

                    // Override packages folder
                    var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(Settings);
                    restoreContext.GlobalPackagesFolder = GetEffectiveGlobalPackagesFolder(
                                        PackagesDirectory,
                                        SolutionDirectory,
                                        restoreInputs,
                                        globalPackagesFolder);

                    // Providers
                    // Use the settings loaded above in ReadSettings(restoreInputs)
                    restoreContext.RequestProviders.Add(new MSBuildCachedRequestProvider(
                        providerCache,
                        restoreInputs.ProjectReferenceLookup,
                        Settings));

                    restoreContext.RequestProviders.Add(new MSBuildP2PRestoreRequestProvider(providerCache));
                    restoreContext.RequestProviders.Add(new ProjectJsonRestoreRequestProvider(providerCache));

                    // Run restore
                    var v3Summaries = await RestoreRunner.Run(restoreContext);
                    restoreSummaries.AddRange(v3Summaries);
                }
            }

            // Summaries
            RestoreSummary.Log(Console, restoreSummaries);

            if (restoreSummaries.Any(x => !x.Success))
            {
                throw new ExitCodeException(exitCode: 1);
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

        private static CachingSourceProvider _sourceProvider;
        private CachingSourceProvider GetSourceRepositoryProvider()
        {
            if (_sourceProvider == null)
            {
                _sourceProvider = new CachingSourceProvider(SourceProvider);
            }

            return _sourceProvider;
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
                SetDefaultCredentialProvider();
            }
        }

        private async Task<RestoreSummary> PerformNuGetV2RestoreAsync(PackageRestoreInputs packageRestoreInputs)
        {
            ReadSettings(packageRestoreInputs);
            var packagesFolderPath = GetPackagesFolder(packageRestoreInputs);

            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(SourceProvider);
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, Settings, packagesFolderPath);

            var installedPackageReferences = new HashSet<Packaging.PackageReference>(new PackageReferenceComparer());
            if (packageRestoreInputs.RestoringWithSolutionFile)
            {
                installedPackageReferences.AddRange(packageRestoreInputs
                    .PackagesConfigFiles
                    .SelectMany(file => GetInstalledPackageReferences(file, allowDuplicatePackageIds: true)));
            }
            else if (packageRestoreInputs.PackagesConfigFiles.Count > 0)
            {
                // By default the PackageReferenceFile does not throw
                // if the file does not exist at the specified path.
                // So we'll need to verify that the file exists.
                Debug.Assert(packageRestoreInputs.PackagesConfigFiles.Count == 1,
                    "Only one packages.config file is allowed to be specified " +
                    "at a time when not performing solution restore.");

                var packageReferenceFile = packageRestoreInputs.PackagesConfigFiles[0];
                if (!File.Exists(packageReferenceFile))
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("RestoreCommandFileNotFound"),
                        packageReferenceFile);

                    throw new InvalidOperationException(message);
                }

                installedPackageReferences.AddRange(
                    GetInstalledPackageReferences(packageReferenceFile, allowDuplicatePackageIds: true));
            }

            var missingPackageReferences = installedPackageReferences.Where(reference =>
                !nuGetPackageManager.PackageExistsInPackagesFolder(reference.PackageIdentity)).ToArray();

            if (missingPackageReferences.Length == 0)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("InstallCommandNothingToInstall"),
                    "packages.config");

                Console.LogMinimal(message);
                return new RestoreSummary(true);
            }

            var packageRestoreData = missingPackageReferences.Select(reference =>
                new PackageRestoreData(
                    reference,
                    new[] { packageRestoreInputs.RestoringWithSolutionFile
                                ? packageRestoreInputs.DirectoryOfSolutionFile
                                : packageRestoreInputs.PackagesConfigFiles[0] },
                    isMissing: true));

            var packageSources = GetPackageSources(Settings);

            var repositories = packageSources
                .Select(sourceRepositoryProvider.CreateRepository)
                .ToArray();

            var installCount = 0;
            var failedEvents = new ConcurrentQueue<PackageRestoreFailedEventArgs>();

            var packageRestoreContext = new PackageRestoreContext(
                nuGetPackageManager,
                packageRestoreData,
                CancellationToken.None,
                packageRestoredEvent: (sender, args) => { Interlocked.Add(ref installCount, args.Restored ? 1 : 0); },
                packageRestoreFailedEvent: (sender, args) => { failedEvents.Enqueue(args); },
                sourceRepositories: repositories,
                maxNumberOfParallelTasks: DisableParallelProcessing
                        ? 1
                        : PackageManagementConstants.DefaultMaxDegreeOfParallelism);

            CheckRequireConsent();

            var collectorLogger = new CollectorLogger(Console);
            var projectContext = new ConsoleProjectContext(collectorLogger)
            {
                PackageExtractionContext = new PackageExtractionContext(collectorLogger)
            };

            if (EffectivePackageSaveMode != Packaging.PackageSaveMode.None)
            {
                projectContext.PackageExtractionContext.PackageSaveMode = EffectivePackageSaveMode;
            }

            var result = await PackageRestoreManager.RestoreMissingPackagesAsync(
                packageRestoreContext,
                projectContext);

            return new RestoreSummary(
                result.Restored,
                "packages.config projects",
                Settings.Priority.Select(x => Path.Combine(x.Root, x.FileName)),
                packageSources.Select(x => x.Source),
                installCount,
                collectorLogger.Errors.Concat(failedEvents.Select(e => e.Exception.Message)));
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

                    Console.LogMinimal(message);
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

        /// <summary>
        /// Discover all restore inputs, this checks for both v2 and v3
        /// </summary>
        private PackageRestoreInputs DetermineRestoreInputs()
        {
            var packageRestoreInputs = new PackageRestoreInputs();

            if (Arguments.Count == 0)
            {
                // If no arguments were provided use the current directory
                GetInputsFromDirectory(Directory.GetCurrentDirectory(), packageRestoreInputs);
            }
            else
            {
                // Restore takes multiple arguments, each could be a file or directory
                var argument = Arguments.Single();
                var fullPath = Path.GetFullPath(argument);

                if (Directory.Exists(fullPath))
                {
                    // Dir
                    GetInputsFromDirectory(fullPath, packageRestoreInputs);
                }
                else if (File.Exists(fullPath))
                {
                    // File
                    GetInputsFromFile(fullPath, packageRestoreInputs);
                }
                else
                {
                    // Not found
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("RestoreCommandFileNotFound"),
                        argument);

                    throw new InvalidOperationException(message);
                }
            }

            // Find P2P graph for v3 inputs.
            var projectsWithPotentialP2PReferences = packageRestoreInputs.RestoreV3Context.Inputs
                .Where(MsBuildUtility.IsMsBuildBasedProject)
                .ToArray();

            if (projectsWithPotentialP2PReferences.Length > 0)
            {
                int scaleTimeout;

                if (Project2ProjectTimeOut > 0)
                {
                    scaleTimeout = Project2ProjectTimeOut * 1000;
                }
                else
                {
                    scaleTimeout = MsBuildUtility.MsBuildWaitTime *
                        Math.Max(10, projectsWithPotentialP2PReferences.Length / 2) / 10;
                }

                Console.LogVerbose($"MSBuild P2P timeout [ms]: {scaleTimeout}");

                // Call MSBuild to resolve P2P references.
                var referencesLookup = MsBuildUtility.GetProjectReferences(
                    _msbuildDirectory.Value,
                    projectsWithPotentialP2PReferences,
                    scaleTimeout);

                packageRestoreInputs.ProjectReferenceLookup = referencesLookup;
            }

            return packageRestoreInputs;
        }

        /// <summary>
        /// Determine if a file is v2 or v3
        /// </summary>
        private void GetInputsFromFile(string projectFilePath, PackageRestoreInputs packageRestoreInputs)
        {
            // An argument was passed in. It might be a solution file or directory,
            // project file, project.json, or packages.config file
            var projectFileName = Path.GetFileName(projectFilePath);

            if (ProjectJsonPathUtilities.IsProjectConfig(projectFileName))
            {
                // project.json or projName.project.json
                packageRestoreInputs.RestoreV3Context.Inputs.Add(projectFilePath);
            }
            else if (IsPackagesConfig(projectFileName))
            {
                // restoring from packages.config or packages.projectname.config file
                packageRestoreInputs.PackagesConfigFiles.Add(projectFilePath);
            }
            else if (projectFileName.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            {
                // For msbuild files find the project.json or packages.config file,
                // if neither exist skip it
                var projectName = Path.GetFileNameWithoutExtension(projectFileName);
                var dir = Path.GetDirectoryName(projectFilePath);

                var projectJsonPath = ProjectJsonPathUtilities.GetProjectConfigPath(dir, projectName);
                var packagesConfigPath = GetPackageReferenceFile(projectFilePath);

                // Check for project.json
                if (File.Exists(projectJsonPath))
                {
                    if (MsBuildUtility.IsMsBuildBasedProject(projectFilePath))
                    {
                        // Add the project file path if it allows p2ps
                        packageRestoreInputs.RestoreV3Context.Inputs.Add(projectFilePath);
                    }
                    else
                    {
                        // Unknown project type, add the project.json by itself
                        packageRestoreInputs.RestoreV3Context.Inputs.Add(projectJsonPath);
                    }
                }
                else if (File.Exists(packagesConfigPath))
                {
                    // Check for packages.config, if it exists add it directly
                    packageRestoreInputs.PackagesConfigFiles.Add(packagesConfigPath);
                }
            }
            else if (projectFileName.EndsWith(".dg", StringComparison.OrdinalIgnoreCase))
            {
                packageRestoreInputs.RestoreV3Context.Inputs.Add(projectFilePath);
            }
            else if (projectFileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                ProcessSolutionFile(projectFilePath, packageRestoreInputs);
            }
            else
            {
                // Not a file we know about. Try to be helpful without response.
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, RestoreRunner.GetInvalidInputErrorMessage(projectFileName), projectFileName));
            }
        }

        /// <summary>
        /// Search a directory for v2 or v3 inputs, only the first type is taken.
        /// </summary>
        private void GetInputsFromDirectory(string directory, PackageRestoreInputs packageRestoreInputs)
        {
            var topLevelFiles = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);

            //  Solution files
            var solutionFiles = topLevelFiles.Where(file =>
                file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            if (solutionFiles.Length > 0)
            {
                if (solutionFiles.Length != 1)
                {
                    throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_MultipleSolutions"));
                }

                var file = solutionFiles[0];

                if (Verbosity == Verbosity.Detailed)
                {
                    Console.WriteLine(
                        LocalizedResourceManager.GetString("RestoreCommandRestoringPackagesForSolution"),
                        file);
                }

                ProcessSolutionFile(file, packageRestoreInputs);

                return;
            }

            // Packages.config
            var packagesConfigFile = Path.Combine(directory, Constants.PackageReferenceFile);

            if (File.Exists(packagesConfigFile))
            {
                if (Verbosity == Verbosity.Detailed)
                {
                    Console.WriteLine(
                            LocalizedResourceManager.GetString(
                                "RestoreCommandRestoringPackagesFromPackagesConfigFile"));
                }

                // packages.confg with no solution file
                packageRestoreInputs.PackagesConfigFiles.Add(packagesConfigFile);

                return;
            }

            // Project.json is any directory
            if (Directory.GetFiles(
                directory,
                $"*{ProjectJsonPathUtilities.ProjectConfigFileName}",
                SearchOption.AllDirectories).Length > 0)
            {
                // V3 recursive project.json search
                packageRestoreInputs.RestoreV3Context.Inputs.Add(directory);

                return;
            }

            // The directory did not contain a valid target, fail!
            var noInputs = string.Format(
                CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString(
                    "Error_UnableToLocateRestoreTarget"),
                    directory);

            throw new InvalidOperationException(noInputs);
        }

        private static bool IsSolutionOrProjectFile(string fileName)
        {
            if (!String.IsNullOrEmpty(fileName))
            {
                var extension = Path.GetExtension(fileName);
                var lastFourCharacters = string.Empty;
                var length = extension.Length;

                if (length >= 4)
                {
                    lastFourCharacters = extension.Substring(length - 4);
                }

                return (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(lastFourCharacters, "proj", StringComparison.OrdinalIgnoreCase));
            }
            return false;
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
            //Check if the string passed is a file
            //If it is a file, then check if it a solution or project file
            //For other file types and directories it will fall out of these checks and fail later for invalid inputs
            if (File.Exists(solutionFileOrDirectory) && IsSolutionOrProjectFile(solutionFileOrDirectory))
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

        /// <summary>
        /// True if the filename is a packages.config file
        /// </summary>
        private static bool IsPackagesConfig(string projectFileName)
        {
            return string.Equals(projectFileName, Constants.PackageReferenceFile)
                || (projectFileName.StartsWith("packages.", StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    Path.GetExtension(projectFileName),
                    Path.GetExtension(Constants.PackageReferenceFile), StringComparison.OrdinalIgnoreCase));
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

            // restore packages for the solution
            var solutionLevelPackagesConfig = Path.Combine(
                restoreInputs.DirectoryOfSolutionFile,
                NuGetConstants.NuGetSolutionSettingsFolder,
                Constants.PackageReferenceFile);

            if (File.Exists(solutionLevelPackagesConfig))
            {
                restoreInputs.PackagesConfigFiles.Add(solutionLevelPackagesConfig);
            }

            var projectFiles = MsBuildUtility.GetAllProjectFileNames(solutionFileFullPath, _msbuildDirectory.Value)
                .Select(Path.GetFullPath); // normalize path
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
                var projectJsonPath = ProjectJsonPathUtilities.GetProjectConfigPath(dir, projectName);

                // project.json overrides packages.config
                if (File.Exists(projectJsonPath))
                {
                    // project.json inputs are resolved again against the p2p file
                    // and are matched with the solution there
                    // For known msbuild project types use the project
                    if (MsBuildUtility.IsMsBuildBasedProject(projectFile))
                    {
                        restoreInputs.RestoreV3Context.Inputs.Add(projectFile);
                    }
                    else
                    {
                        // For unknown types restore the project.json file without p2ps
                        restoreInputs.RestoreV3Context.Inputs.Add(projectJsonPath);
                    }
                }
                else if (File.Exists(packagesConfigFilePath))
                {
                    restoreInputs.PackagesConfigFiles.Add(packagesConfigFilePath);
                }
            }
        }

        private class PackageRestoreInputs
        {
            public PackageRestoreInputs()
            {
                ProjectReferenceLookup = new MSBuildProjectReferenceProvider(Enumerable.Empty<string>());
            }

            public bool RestoringWithSolutionFile => !string.IsNullOrEmpty(DirectoryOfSolutionFile);

            public string DirectoryOfSolutionFile { get; set; }

            public List<string> PackagesConfigFiles { get; } = new List<string>();

            public MSBuildProjectReferenceProvider ProjectReferenceLookup { get; set; }

            public RestoreArgs RestoreV3Context { get; set; } = new RestoreArgs();
        }
    }
}
