// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Commands.Package.Update;

internal static class PackageUpdateCommandRunner
{
    // This overload sets static state, so should not be used in tests.
    internal static Task<int> Run(PackageUpdateArgs args, CancellationToken cancellationToken)
    {
        ILoggerWithColor logger = new CommandOutputLogger(args.LogLevel)
        {
            HidePrefixForInfoAndMinimal = true
        };

        XPlatUtility.ConfigureProtocol();
        DefaultCredentialServiceUtility.SetupDefaultCredentialService(logger, nonInteractive: !args.Interactive);

        // MSBuildAPIUtility's output is different to what we want for package update.
        // While it would probably be a good idea to align the output of all commands using MSBuildAPIUtility,
        // in order to meet deadlines, we'll suppress its output, and leave improvements for later.
        MSBuildAPIUtility msBuild = new(NullLogger.Instance);

        var restoreHelper = new PackageUpdateIO(msBuild, EnvironmentVariableWrapper.Instance);

        return Run(args, logger, restoreHelper, cancellationToken);
    }

    internal static async Task<int> Run(PackageUpdateArgs args, ILoggerWithColor logger, IPackageUpdateIO packageUpdateIO, CancellationToken cancellationToken)
    {
        // 1. Get DGSpec for project/solution
        // 2. Find suitable version of package(s) to update
        // 3. Preview restore to validate changes
        // 4. Update MSBuild files
        // 5. Commit restore if everything successful

        // 1. Get DGSpec for project/solution
        logger.LogVerbose(Strings.PackageUpdate_LoadingDGSpec);
        var dgSpec = packageUpdateIO.GetDependencyGraphSpec(args.Project);

        if (dgSpec is null || dgSpec.Restore is null || dgSpec.Restore.Count == 0)
        {
            logger.LogMinimal(
                string.Format(CultureInfo.CurrentCulture, Strings.Error_PathIsMissingOrInvalid, args.Project),
                ConsoleColor.Red);
            return ExitCodes.Error;
        }

        logger.LogInformation(Format.PackageUpdate_UpdatingOutdatedPackages(args.Project));

        if (dgSpec.Restore.Count > 1)
        {
            logger.LogMinimal(Strings.Unsupported_UpdatingMoreThanOneProject, ConsoleColor.Red);
            return ExitCodes.Error;
        }

        // 2. Find suitable version of package(s) to update
        // Source provider will be needed to find the package version and to restore, so create it here.
        logger.LogVerbose(Strings.PackageUpdate_FindingUpdateVersions);

        string settingsRoot = Directory.Exists(args.Project) ? args.Project : Path.GetDirectoryName(args.Project)!;
        ISettings settings = packageUpdateIO.LoadSettings(settingsRoot);

        CachingSourceProvider sourceProvider = new CachingSourceProvider(new PackageSourceProvider(settings));
        using SourceCacheContext sourceCacheContext = new();
        var versionChooser = new VersionChooser(sourceProvider, settings, sourceCacheContext);
        var packagesToUpdateResult = await GetPackagesToUpdateAsync(args.Packages, dgSpec.Projects.Single(), versionChooser, settings, logger, cancellationToken);

        if (!packagesToUpdateResult.Any())
        {
            // GetPackagesToUpdateAsync is responsible for outputting error messages.
            return ExitCodes.Error;
        }

        // 3. Preview restore to validate changes
        logger.LogDebug(Strings.PackageUpdate_PreviewRestore);
        var updatedDgSpec = GetUpdatedDependencyGraphSpec(dgSpec, packagesToUpdateResult);
        var restorePreviewResult = await packageUpdateIO.PreviewUpdatePackageReferenceAsync(updatedDgSpec, sourceCacheContext, logger, cancellationToken);

        if (!restorePreviewResult.Success)
        {
            logger.LogMinimal(Strings.PackageUpdate_PreviewRestoreFailed, ConsoleColor.Red);
            return ExitCodes.Error;
        }

        // 4. Update MSBuild files
        var projectName = Path.GetFileNameWithoutExtension(dgSpec.Projects[0].FilePath);
        logger.LogInformation($"  {projectName}:");

        var updatedPackageSpec = updatedDgSpec.Projects[0];
        int updatedCount = 0;

        foreach (var packageResult in packagesToUpdateResult)
        {
            logger.LogInformation($"    " + Format.PackageUpdate_UpdatedMessage(packageResult.Package.Id, packageResult.Package.CurrentVersion.ToShortString(), packageResult.Package.NewVersion.ToShortString()));
            packageUpdateIO.UpdatePackageReference(updatedPackageSpec, restorePreviewResult, packageResult.Frameworks, packageResult.Package, logger);
            updatedCount++;
        }

        logger.LogInformation("");

        // 5. Commit restore if everything successful
        await packageUpdateIO.CommitAsync(restorePreviewResult, CancellationToken.None);

        int scannedCount = packagesToUpdateResult.Count;
        logger.LogMinimal(Format.PackageUpdate_FinalSummary(updatedCount, scannedCount), ConsoleColor.Green);

        return ExitCodes.Success;
    }

    internal static async Task<List<PackageUpdateResult>> GetPackagesToUpdateAsync(
        IReadOnlyList<Package> packages,
        PackageSpec project,
        IVersionChooser versionChooser,
        ISettings settings,
        ILoggerWithColor logger,
        CancellationToken cancellationToken)
    {
        if (packages is null || packages.Count == 0)
        {
            logger.LogMinimal(Strings.Unsupported_UpgradeAllPackages, ConsoleColor.Red);
            return new List<PackageUpdateResult>();
        }

        var sourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
        if (sourceMapping.IsEnabled)
        {
            logger.LogMinimal(Strings.Unsupported_PackageSourceMapping, ConsoleColor.Red);
            return new List<PackageUpdateResult>();
        }

        var packagesToUpdate = new List<PackageUpdateResult>();
        bool hasErrors = false;

        foreach (var package in packages)
        {
            var result = await GetSinglePackageToUpdateAsync(package, project, versionChooser, settings, logger, cancellationToken);
            if (result != null)
            {
                packagesToUpdate.Add(result);
            }
            else
            {
                hasErrors = true;
            }
        }

        return hasErrors ? new List<PackageUpdateResult>() : packagesToUpdate;
    }

    private static async Task<PackageUpdateResult?> GetSinglePackageToUpdateAsync(
        Package package,
        PackageSpec project,
        IVersionChooser versionChooser,
        ISettings settings,
        ILoggerWithColor logger,
        CancellationToken cancellationToken)
    {
        VersionRange? existingVersion = null;
        List<NuGetFramework>? frameworks = null;

        foreach (var tfm in project.TargetFrameworks)
        {
            foreach (var dependency in tfm.Dependencies)
            {
                if (string.Equals(package.Id, dependency.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (frameworks is null)
                    {
                        frameworks = new List<NuGetFramework>();
                    }
                    frameworks.Add(tfm.FrameworkName);

                    VersionRange tfmVersionRange;
                    if (project.RestoreMetadata.CentralPackageFloatingVersionsEnabled)
                    {
                        if (!tfm.CentralPackageVersions.TryGetValue(
                            package.Id,
                            out CentralPackageVersion? centralVersion))
                        {
                            logger.LogMinimal(
                                Messages.Error_CouldNotFindPackageVersionForCpmPackage(package.Id),
                                ConsoleColor.Red);
                            return null;
                        }
                        tfmVersionRange = centralVersion.VersionRange;
                    }
                    else
                    {
                        tfmVersionRange = dependency.LibraryRange.VersionRange ?? VersionRange.All;
                    }

                    if (existingVersion == null)
                    {
                        existingVersion = tfmVersionRange;
                    }
                    else
                    {
                        if (tfmVersionRange != existingVersion)
                        {
                            logger.LogMinimal(
                                Messages.Unsupported_UpdatePackageWithDifferentPerTfmVersions(package.Id, project.FilePath),
                                ConsoleColor.Red);
                            return null;
                        }
                    }
                }
            }
        }

        if (existingVersion is null)
        {
            logger.LogMinimal(Messages.Error_PackageNotReferenced(package.Id, project.FilePath), ConsoleColor.Red);
            return null;
        }

        VersionRange newVersion;
        if (package.VersionRange is null)
        {
            // NuGet.Protocol outputs request and response info at normal verbosity, which update doesn't want.
            var protocolLogger = new RemappedLevelLogger(
                logger,
                new RemappedLevelLogger.Mapping
                {
                    Information = LogLevel.Verbose,
                });

            NuGetVersion? highestVersion = await versionChooser.GetLatestVersionAsync(
                package.Id,
                protocolLogger,
                cancellationToken);

            if (highestVersion is null)
            {
                logger.LogMinimal(Messages.Error_NoVersionsAvailable(package.Id), ConsoleColor.Red);
                return null;
            }

            if (existingVersion.MinVersion == highestVersion)
            {
                logger.LogMinimal(Messages.Warning_AlreadyHighestVersion(package.Id, highestVersion.OriginalVersion, project.FilePath), ConsoleColor.Yellow);
                return null;
            }

            newVersion = new VersionRange(highestVersion);
        }
        else
        {
            newVersion = package.VersionRange;
            if (newVersion == existingVersion)
            {
                logger.LogMinimal(Messages.Warning_AlreadyUsingSameVersion(package.Id, newVersion.OriginalString), ConsoleColor.Yellow);
                return null;
            }
        }

        var packageToUpdate = new PackageToUpdate
        {
            Id = package.Id,
            CurrentVersion = existingVersion,
            NewVersion = newVersion
        };

        return new PackageUpdateResult
        {
            Package = packageToUpdate,
            Frameworks = frameworks!
        };
    }

    private static DependencyGraphSpec GetUpdatedDependencyGraphSpec(DependencyGraphSpec currentDgSpec, List<PackageUpdateResult> packagesToUpdate)
    {
        var updatedPackageSpec = currentDgSpec.Projects[0].Clone();

        foreach (var packageResult in packagesToUpdate)
        {
            PackageDependency packageDependency = new PackageDependency(packageResult.Package.Id, packageResult.Package.NewVersion);
            PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, packageDependency);
        }

        var updatedDgSpec = currentDgSpec.WithReplacedSpec(updatedPackageSpec).WithoutRestores();
        updatedDgSpec.AddRestore(updatedPackageSpec.RestoreMetadata.ProjectUniqueName);

        return updatedDgSpec;
    }

    internal record PackageToUpdate
    {
        public required string Id { get; init; }
        public required VersionRange CurrentVersion { get; init; }
        public required VersionRange NewVersion { get; init; }
    }

    internal record PackageUpdateResult
    {
        public required PackageToUpdate Package { get; init; }
        public required List<NuGetFramework> Frameworks { get; init; }
    }

    private static class Format
    {
        internal static string PackageUpdate_UpdatingOutdatedPackages(string projectPath)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.PackageUpdate_UpdatingOutdatedPackages, projectPath);
        }

        internal static string PackageUpdate_UpdatedMessage(string packageId, string currentVersion, string newVersion)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.PackageUpdate_UpdatedMessage, packageId, currentVersion, newVersion);
        }

        internal static string PackageUpdate_FinalSummary(int updatedCount, int scannedCount)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.PackageUpdate_FinalSummary, updatedCount, scannedCount);
        }
    }
}
