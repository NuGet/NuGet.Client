// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "install", "InstallCommandDescription",
        MinArgs = 0, MaxArgs = 1, UsageSummaryResourceName = "InstallCommandUsageSummary",
        UsageDescriptionResourceName = "InstallCommandUsageDescription",
        UsageExampleResourceName = "InstallCommandUsageExamples")]
    public class InstallCommand : DownloadCommandBase
    {
        [Option(typeof(NuGetCommand), "InstallCommandOutputDirDescription")]
        public string OutputDirectory { get; set; }

        [Option(typeof(NuGetCommand), "InstallCommandVersionDescription")]
        public string Version { get; set; }

        [Option(typeof(NuGetCommand), "InstallCommandExcludeVersionDescription", AltName = "x")]
        public bool ExcludeVersion { get; set; }

        [Option(typeof(NuGetCommand), "InstallCommandPrerelease")]
        public bool Prerelease { get; set; }

        [Option(typeof(NuGetCommand), "InstallCommandRequireConsent")]
        public bool RequireConsent { get; set; }

        [Option(typeof(NuGetCommand), "InstallCommandSolutionDirectory")]
        public string SolutionDirectory { get; set; }

        [ImportingConstructor]
        public InstallCommand()
            : this(MachineCache.Default)
        {
        }

        protected internal InstallCommand(IPackageRepository cacheRepository) :
            base(cacheRepository)
        {
            // On mono, parallel builds are broken for some reason. See https://gist.github.com/4201936 for the errors
            // That are thrown.
            DisableParallelProcessing = EnvironmentUtility.IsMonoRuntime;
        }

        public override Task ExecuteCommandAsync()
        {
            if (DisableParallelProcessing)
            {
                HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
            }

            CalculateEffectivePackageSaveMode();
            CalculateEffectiveSettings();
            string installPath = ResolveInstallPath();

            string configFilePath = Path.GetFullPath(Arguments.Count == 0 ? Constants.PackageReferenceFile : Arguments[0]);
            string configFileName = Path.GetFileName(configFilePath);

            // If the first argument is a packages.xxx.config file, install everything it lists
            // Otherwise, treat the first argument as a package Id
            if (PackageReferenceFile.IsValidConfigFileName(configFileName))
            {
                Prerelease = true;

                // display opt-out message if needed
                if (Console != null && RequireConsent &&
                    new PackageRestoreConsent(new SettingsToLegacySettings(Settings)).IsGranted)
                {
                    string message = String.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("RestoreCommandPackageRestoreOptOutMessage"),
                        NuGet.Resources.NuGetResources.PackageRestoreConsentCheckBoxText.Replace("&", ""));
                    Console.WriteLine(message);
                }

                return PerformV2Restore(configFilePath, installPath);
            }
            else
            {
                var packageId = Arguments[0];
                var version = Version != null ? new NuGetVersion(Version) : null;
                return InstallPackage(packageId, version, installPath);
            }
        }

        private void CalculateEffectiveSettings()
        {
            // If the SolutionDir is specified, use the .nuget directory under it to determine the solution-level settings
            if (!String.IsNullOrEmpty(SolutionDirectory))
            {
                var path = Path.Combine(SolutionDirectory.TrimEnd(Path.DirectorySeparatorChar), NuGetConstants.NuGetSolutionSettingsFolder);

                var solutionSettingsFile = Path.GetFullPath(path);

                Settings = Configuration.Settings.LoadDefaultSettings(
                    solutionSettingsFile,
                    configFileName: null,
                    machineWideSettings: MachineWideSettings);

                // Recreate the source provider and credential provider
                SourceProvider = PackageSourceBuilder.CreateSourceProvider(Settings);
                SetDefaultCredentialProvider();
            }
        }

        internal string ResolveInstallPath()
        {
            if (!String.IsNullOrEmpty(OutputDirectory))
            {
                // Use the OutputDirectory if specified.
                return OutputDirectory;
            }

            string installPath = SettingsUtility.GetRepositoryPath(Settings);
            if (!String.IsNullOrEmpty(installPath))
            {
                // If a value is specified in config, use that.
                return installPath;
            }

            if (!String.IsNullOrEmpty(SolutionDirectory))
            {
                // For package restore scenarios, deduce the path of the packages directory from the solution directory.
                return Path.Combine(SolutionDirectory, CommandLineConstants.PackagesDirectoryName);
            }

            // Use the current directory as output.
            return CurrentDirectory;
        }

        private Task PerformV2Restore(string packagesConfigFilePath, string installPath)
        {
            var sourceRepositoryProvider = GetSourceRepositoryProvider();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, Settings, installPath, ExcludeVersion);

            var installedPackageReferences = GetInstalledPackageReferences(
                packagesConfigFilePath,
                allowDuplicatePackageIds: true);

            var packageRestoreData = installedPackageReferences.Select(reference =>
                new PackageRestoreData(
                    reference,
                    new[] { packagesConfigFilePath },
                    isMissing: true));

            var packageSources = GetPackageSources(Settings);
            
            Console.PrintPackageSources(packageSources);

            var packageRestoreContext = new PackageRestoreContext(
                nuGetPackageManager,
                packageRestoreData,
                CancellationToken.None,
                packageRestoredEvent: null,
                packageRestoreFailedEvent: null,
                sourceRepositories: packageSources.Select(sourceRepositoryProvider.CreateRepository),
                maxNumberOfParallelTasks: DisableParallelProcessing ? 1 : PackageManagementConstants.DefaultMaxDegreeOfParallelism);

            var missingPackageReferences = installedPackageReferences.Where(reference =>
                !nuGetPackageManager.PackageExistsInPackagesFolder(reference.PackageIdentity)).Any();

            if (!missingPackageReferences)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("InstallCommandNothingToInstall"),
                    packagesConfigFilePath);

                Console.LogMinimal(message);
            }

            Task<PackageRestoreResult> packageRestoreTask = PackageRestoreManager.RestoreMissingPackagesAsync(packageRestoreContext, new ConsoleProjectContext(Console), new SourceCacheContext());
            return packageRestoreTask;
        }

        private CommandLineSourceRepositoryProvider GetSourceRepositoryProvider()
        {
            return new CommandLineSourceRepositoryProvider(SourceProvider);
        }

        private async Task InstallPackage(
            string packageId,
            NuGetVersion version,
            string installPath)
        {
            if (version == null)
            {
                NoCache = true;
            }

            var folderProject = new FolderNuGetProject(
                installPath,
                new PackagePathResolver(installPath, !ExcludeVersion));

            var sourceRepositoryProvider = GetSourceRepositoryProvider();
            var packageManager = new NuGetPackageManager(sourceRepositoryProvider, Settings, installPath);

            var packageSources = GetPackageSources(Settings);

            Console.PrintPackageSources(packageSources);

            var primaryRepositories = packageSources.Select(sourceRepositoryProvider.CreateRepository);

            var allowPrerelease = Prerelease || (version != null && version.IsPrerelease);

            var resolutionContext = new ResolutionContext(
                DependencyBehavior.Lowest,
                includePrelease: allowPrerelease,
                includeUnlisted: true,
                versionConstraints: VersionConstraints.None);

            if (version == null)
            {
                // Find the latest version using NuGetPackageManager
                version = await NuGetPackageManager.GetLatestVersionAsync(
                    packageId,
                    folderProject,
                    resolutionContext,
                    primaryRepositories,
                    Console,
                    CancellationToken.None);

                if (version == null)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString("InstallCommandUnableToFindPackage"),
                        packageId);

                    throw new CommandLineException(message);
                }
            }

            var packageIdentity = new PackageIdentity(packageId, version);

            if (folderProject.PackageExists(packageIdentity))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString("InstallCommandPackageAlreadyExists"),
                    packageIdentity);

                Console.LogMinimal(message);
            }
            else
            {
                var projectContext = new ConsoleProjectContext(Console)
                {
                    PackageExtractionContext = new PackageExtractionContext(Console)
                };

                if (EffectivePackageSaveMode != Packaging.PackageSaveMode.None)
                {
                    projectContext.PackageExtractionContext.PackageSaveMode = EffectivePackageSaveMode;
                }

                var cacheContext = new SourceCacheContext();
                cacheContext.NoCache = DirectDownload;

                await packageManager.InstallPackageAsync(
                    folderProject,
                    packageIdentity,
                    resolutionContext,
                    projectContext,
                    cacheContext,
                    primaryRepositories,
                    Enumerable.Empty<SourceRepository>(),
                    CancellationToken.None);
            }
        }
    }
}
