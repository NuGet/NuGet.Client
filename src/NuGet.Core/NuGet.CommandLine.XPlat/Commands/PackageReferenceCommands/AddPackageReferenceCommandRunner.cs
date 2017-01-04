// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    public class AddPackageReferenceCommandRunner : IAddPackageReferenceCommandRunner
    {
        private const string NUGET_RESTORE_MSBUILD_VERBOSITY = "NUGET_RESTORE_MSBUILD_VERBOSITY";
        private const int MSBUILD_WAIT_TIME = 2 * 60 * 1000; // 2 minutes in milliseconds

        public async Task<int> ExecuteCommand(PackageReferenceArgs packageReferenceArgs, MSBuildAPIUtility msBuild)
        {
            packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.Info_AddPkgAddingReference,
                packageReferenceArgs.PackageDependency.Id,
                packageReferenceArgs.ProjectPath));

            if (packageReferenceArgs.NoRestore)
            {
                packageReferenceArgs.Logger.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.Warn_AddPkgWithoutRestore));

                msBuild.AddPackageReference(packageReferenceArgs.ProjectPath, packageReferenceArgs.PackageDependency);
                return 0;
            }

            // 1. Get project dg file
            packageReferenceArgs.Logger.LogDebug("Reading project Dependency Graph");
            var dgSpec = ReadProjectDependencyGraph(packageReferenceArgs);
            if (dgSpec == null)
            {
                throw new Exception(Strings.Error_NoDgSpec);
            }
            packageReferenceArgs.Logger.LogDebug("Project Dependency Graph Read");

            var projectName = dgSpec.Restore.FirstOrDefault();
            var originalPackageSpec = dgSpec.GetProjectSpec(projectName);

            // Create a copy to avoid modifying the original spec which may be shared.
            var updatedPackageSpec = originalPackageSpec.Clone();
            PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, packageReferenceArgs.PackageDependency);

            var updatedDgSpec = dgSpec.WithReplacedSpec(updatedPackageSpec).WithoutRestores();
            updatedDgSpec.AddRestore(updatedPackageSpec.RestoreMetadata.ProjectUniqueName);

            // 2. Run Restore Preview
            packageReferenceArgs.Logger.LogDebug("Running Restore preview");
            var restorePreviewResult = await PreviewAddPackageReference(packageReferenceArgs,
                updatedDgSpec,
                updatedPackageSpec);
            packageReferenceArgs.Logger.LogDebug("Restore Review completed");

            // 3. Process Restore Result
            var compatibleFrameworks = new HashSet<NuGetFramework>(
                restorePreviewResult
                .Result
                .CompatibilityCheckResults
                .Where(t => t.Success)
                .Select(t => t.Graph.Framework));

            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                // If the user has specified frameworks then we intersect that with the compatible frameworks.
                var userSpecifiedFrameworks = new HashSet<NuGetFramework>(
                    packageReferenceArgs
                    .Frameworks
                    .Select(f => NuGetFramework.Parse(f)));

                compatibleFrameworks.IntersectWith(userSpecifiedFrameworks);
            }

            // 4. Write to Project
            if (compatibleFrameworks.Count == 0)
            {
                // Package is compatible with none of the project TFMs
                // Do not add a package reference, throw appropriate error
                packageReferenceArgs.Logger.LogError(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_AddPkgIncompatibleWithAllFrameworks,
                    packageReferenceArgs.PackageDependency.Id,
                    packageReferenceArgs.Frameworks?.Any() == true ? Strings.AddPkg_UserSpecified : Strings.AddPkg_All,
                    packageReferenceArgs.ProjectPath));

                return 1;
            }
            else if (compatibleFrameworks.Count == restorePreviewResult.Result.CompatibilityCheckResults.Count())
            {
                // Package is compatible with all the project TFMs
                // Add an unconditional package reference to the project
                packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Info_AddPkgCompatibleWithAllFrameworks,
                    packageReferenceArgs.PackageDependency.Id,
                    packageReferenceArgs.ProjectPath));

                // If the user did not specify a version then update the version to resolved version
                UpdatePackageVersionIfNeeded(restorePreviewResult, packageReferenceArgs);

                msBuild.AddPackageReference(packageReferenceArgs.ProjectPath,
                    packageReferenceArgs.PackageDependency);
            }
            else
            {
                // Package is compatible with some of the project TFMs
                // Add conditional package references to the project for the compatible TFMs
                packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Info_AddPkgCompatibleWithSubsetFrameworks,
                    packageReferenceArgs.PackageDependency.Id,
                    packageReferenceArgs.ProjectPath));

                var compatibleOriginalFrameworks = originalPackageSpec.RestoreMetadata
                    .OriginalTargetFrameworks
                    .Where(s => compatibleFrameworks.Contains(NuGetFramework.Parse(s)));

                // If the user did not specify a version then update the version to resolved version
                UpdatePackageVersionIfNeeded(restorePreviewResult, packageReferenceArgs);

                msBuild.AddPackageReferencePerTFM(packageReferenceArgs.ProjectPath,
                    packageReferenceArgs.PackageDependency,
                    compatibleOriginalFrameworks);
            }

            return 0;
        }

        private static async Task<RestoreResultPair> PreviewAddPackageReference(PackageReferenceArgs packageReferenceArgs,
            DependencyGraphSpec dgSpec,
            PackageSpec originalPackageSpec)
        {
            // Set user agent and connection settings.
            XPlatUtility.ConfigureProtocol();

            var providerCache = new RestoreCommandProvidersCache();

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = false;
                cacheContext.IgnoreFailedSources = false;

                // Pre-loaded request provider containing the graph file
                var providers = new List<IPreLoadedRestoreRequestProvider>();

                // Create a copy to avoid modifying the original spec which may be shared.
                var updatedPackageSpec = originalPackageSpec.Clone();

                PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, packageReferenceArgs.PackageDependency);

                providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dgSpec));

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = cacheContext,
                    LockFileVersion = LockFileFormat.Version,
                    Log = packageReferenceArgs.Logger,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    GlobalPackagesFolder = packageReferenceArgs.PackageDirectory,
                    PreLoadedRequestProviders = providers,
                    Sources = packageReferenceArgs.Sources?.ToList()
                };

                // Generate Restore Requests. There will always be 1 request here since we are restoring for 1 project.
                var restoreRequests = await RestoreRunner.GetRequests(restoreContext);

                // Run restore without commit. This will always return 1 Result pair since we are restoring for 1 request.
                var restoreResult = await RestoreRunner.RunWithoutCommit(restoreRequests, restoreContext);

                return restoreResult.Single();
            }
        }

        private static DependencyGraphSpec ReadProjectDependencyGraph(PackageReferenceArgs packageReferenceArgs)
        {
            DependencyGraphSpec spec = null;

            if (File.Exists(packageReferenceArgs.DgFilePath))
            {
                spec = DependencyGraphSpec.Load(packageReferenceArgs.DgFilePath);
            }

            return spec;
        }

        private static void UpdatePackageVersionIfNeeded(RestoreResultPair restorePreviewResult,
            PackageReferenceArgs packageReferenceArgs)
        {
            // If the user did not specify a version then write the exact resolved version
            if (packageReferenceArgs.NoVersion)
            {
                // Get the package version from the graph
                var resolvedVersion = GetPackageVersionFromRestoreResult(restorePreviewResult, packageReferenceArgs);

                if (resolvedVersion != null)
                {
                    //Update the packagedependency with the new version
                    packageReferenceArgs.PackageDependency = new PackageDependency(packageReferenceArgs.PackageDependency.Id,
                        new VersionRange(resolvedVersion));
                }
            }
        }

        private static NuGetVersion GetPackageVersionFromRestoreResult(RestoreResultPair restorePreviewResult,
            PackageReferenceArgs packageReferenceArgs)
        {
            // Get the restore graphs from the restore result
            var restoreGraphs = restorePreviewResult
                .Result
                .RestoreGraphs;

            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                // If the user specified frameworks then we get the flattened graphs  only from the compatible frameworks.
                var userSpecifiedFrameworks = new HashSet<NuGetFramework>(
                    packageReferenceArgs
                    .Frameworks
                    .Select(f => NuGetFramework.Parse(f)));

                restoreGraphs = restoreGraphs
                    .Where(r => userSpecifiedFrameworks.Contains(r.Framework));
            }

            foreach (var restoreGraph in restoreGraphs)
            {
                var matchingPackageEntries = restoreGraph
                    .Flattened
                    .Select(p => p)
                    .Where(p => p.Key.Name.Equals(packageReferenceArgs.PackageDependency.Id, StringComparison.OrdinalIgnoreCase));

                if (matchingPackageEntries.Any())
                {
                    return matchingPackageEntries
                        .First()
                        .Key
                        .Version;
                }
            }
            return null;
        }
    }
}