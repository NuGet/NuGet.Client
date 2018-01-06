// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    public class AddPackageReferenceCommandRunner : IPackageReferenceCommandRunner
    {
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

                var libraryDependency = new LibraryDependency
                {
                    LibraryRange = new LibraryRange(
                        name: packageReferenceArgs.PackageDependency.Id,
                        versionRange: packageReferenceArgs.PackageDependency.VersionRange,
                        typeConstraint: LibraryDependencyTarget.Package)
                };

                msBuild.AddPackageReference(packageReferenceArgs.ProjectPath, libraryDependency);
                return 0;
            }

            // 1. Get project dg file
            packageReferenceArgs.Logger.LogDebug("Reading project Dependency Graph");
            var dgSpec = ReadProjectDependencyGraph(packageReferenceArgs);
            if (dgSpec == null)
            {
                // Logging non localized error on debug stream.
                packageReferenceArgs.Logger.LogDebug(Strings.Error_NoDgSpec);

                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_NoDgSpec));
            }
            packageReferenceArgs.Logger.LogDebug("Project Dependency Graph Read");

            var projectFullPath = Path.GetFullPath(packageReferenceArgs.ProjectPath);

            var matchingPackageSpecs = dgSpec
                .Projects
                .Where(p => p.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference && 
                PathUtility.GetStringComparerBasedOnOS().Equals(Path.GetFullPath(p.RestoreMetadata.ProjectPath), projectFullPath))
                .ToArray();

            // This ensures that the DG specs generated in previous steps contain exactly 1 project with the same path as the project requesting add package.
            // Throw otherwise since we cannot proceed further.
            if (matchingPackageSpecs.Length != 1)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_UnsupportedProject,
                    packageReferenceArgs.PackageDependency.Id,
                    packageReferenceArgs.ProjectPath));
            }

            // Parse the user specified frameworks once to avoid re-do's
            var userSpecifiedFrameworks = Enumerable.Empty<NuGetFramework>();
            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                userSpecifiedFrameworks = packageReferenceArgs
                    .Frameworks
                    .Select(f => NuGetFramework.Parse(f));
            }


            var originalPackageSpec = matchingPackageSpecs.FirstOrDefault();

            // Create a copy to avoid modifying the original spec which may be shared.
            var updatedPackageSpec = originalPackageSpec.Clone();
            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                // If user specified frameworks then just use them to add the dependency
                PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, 
                    packageReferenceArgs.PackageDependency,
                    userSpecifiedFrameworks);
            }
            else
            {
                // If the user has not specified a framework, then just add it to all frameworks
                PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, packageReferenceArgs.PackageDependency, updatedPackageSpec.TargetFrameworks.Select(e => e.FrameworkName));
            }


            var updatedDgSpec = dgSpec.WithReplacedSpec(updatedPackageSpec).WithoutRestores();
            updatedDgSpec.AddRestore(updatedPackageSpec.RestoreMetadata.ProjectUniqueName);

            // 2. Run Restore Preview
            packageReferenceArgs.Logger.LogDebug("Running Restore preview");

            var restorePreviewResult = await PreviewAddPackageReferenceAsync(packageReferenceArgs,
                updatedDgSpec);

            packageReferenceArgs.Logger.LogDebug("Restore Review completed");

            // 3. Process Restore Result
            var compatibleFrameworks = new HashSet<NuGetFramework>(
                restorePreviewResult
                .Result
                .CompatibilityCheckResults
                .Where(t => t.Success)
                .Select(t => t.Graph.Framework), new NuGetFrameworkFullComparer());

            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                // If the user has specified frameworks then we intersect that with the compatible frameworks.
                var userSpecifiedFrameworkSet = new HashSet<NuGetFramework>(
                    userSpecifiedFrameworks, 
                    new NuGetFrameworkFullComparer());

                compatibleFrameworks.IntersectWith(userSpecifiedFrameworkSet);
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
            // Ignore the graphs with RID
            else if (compatibleFrameworks.Count == 
                restorePreviewResult.Result.CompatibilityCheckResults.Where(r => string.IsNullOrEmpty(r.Graph.RuntimeIdentifier)).Count())
            {
                // Package is compatible with all the project TFMs
                // Add an unconditional package reference to the project
                packageReferenceArgs.Logger.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.Info_AddPkgCompatibleWithAllFrameworks,
                    packageReferenceArgs.PackageDependency.Id,
                    packageReferenceArgs.ProjectPath));

                // generate a library dependency with all the metadata like Include, Exlude and SuppressParent
                var libraryDependency = GenerateLibraryDependency(updatedPackageSpec, packageReferenceArgs, restorePreviewResult, userSpecifiedFrameworks);

                msBuild.AddPackageReference(packageReferenceArgs.ProjectPath, libraryDependency);
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

                // generate a library dependency with all the metadata like Include, Exlude and SuppressParent
                var libraryDependency = GenerateLibraryDependency(updatedPackageSpec, packageReferenceArgs, restorePreviewResult, userSpecifiedFrameworks);

                msBuild.AddPackageReferencePerTFM(packageReferenceArgs.ProjectPath,
                    libraryDependency,
                    compatibleOriginalFrameworks);
            }

            return 0;
        }

        private static LibraryDependency GenerateLibraryDependency(
            PackageSpec project,
            PackageReferenceArgs packageReferenceArgs,
            RestoreResultPair restorePreviewResult,
            IEnumerable<NuGetFramework> UserSpecifiedFrameworks)
        {
            // get the package resolved version from restore preview result
            var resolvedVersion = GetPackageVersionFromRestoreResult(restorePreviewResult, packageReferenceArgs, UserSpecifiedFrameworks);

            // calculate correct package version to write in project file
            var version = packageReferenceArgs.PackageDependency.VersionRange;

            // If the user did not specify a version then write the exact resolved version
            if (packageReferenceArgs.NoVersion)
            {
                version = new VersionRange(resolvedVersion);
            }

            // update default packages path if user specified custom package directory
            var packagesPath = project.RestoreMetadata.PackagesPath;

            if (!string.IsNullOrEmpty(packageReferenceArgs.PackageDirectory))
            {
                packagesPath = packageReferenceArgs.PackageDirectory;
            }

            // create a path resolver to get nuspec file of the package
            var pathResolver = new FallbackPackagePathResolver(
                packagesPath,
                project.RestoreMetadata.FallbackFolders);
            var info = pathResolver.GetPackageInfo(packageReferenceArgs.PackageDependency.Id, resolvedVersion);
            var packageDirectory = info?.PathResolver.GetInstallPath(packageReferenceArgs.PackageDependency.Id, resolvedVersion);
            var nuspecFile = info?.PathResolver.GetManifestFileName(packageReferenceArgs.PackageDependency.Id, resolvedVersion);

            var nuspecFilePath = Path.GetFullPath(Path.Combine(packageDirectory, nuspecFile));

            // read development dependency from nuspec file
            var developmentDependency = new NuspecReader(nuspecFilePath).GetDevelopmentDependency();

            if (developmentDependency)
            {
                foreach (var frameworkInfo in project.TargetFrameworks
                    .OrderBy(framework => framework.FrameworkName.ToString(),
                        StringComparer.Ordinal))
                {
                    var dependency = frameworkInfo.Dependencies.First(
                        dep => dep.Name.Equals(packageReferenceArgs.PackageDependency.Id, StringComparison.OrdinalIgnoreCase));

                    // if suppressParent and IncludeType aren't set by user, then only update those as per dev dependency
                    if (dependency?.SuppressParent == LibraryIncludeFlagUtils.DefaultSuppressParent &&
                        dependency?.IncludeType == LibraryIncludeFlags.All)
                    {
                        dependency.SuppressParent = LibraryIncludeFlags.All;
                        dependency.IncludeType = LibraryIncludeFlags.All & ~LibraryIncludeFlags.Compile;
                    }

                    if (dependency != null)
                    {
                        dependency.LibraryRange.VersionRange = version;
                        return dependency;
                    }
                }
            }

            return new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: packageReferenceArgs.PackageDependency.Id,
                    versionRange: version,
                    typeConstraint: LibraryDependencyTarget.Package)
            };
        }

        private static async Task<RestoreResultPair> PreviewAddPackageReferenceAsync(PackageReferenceArgs packageReferenceArgs,
            DependencyGraphSpec dgSpec)
        {
            // Set user agent and connection settings.
            XPlatUtility.ConfigureProtocol();

            var providerCache = new RestoreCommandProvidersCache();

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = false;
                cacheContext.IgnoreFailedSources = false;

                // Pre-loaded request provider containing the graph file
                var providers = new List<IPreLoadedRestoreRequestProvider>
                {
                    new DependencyGraphSpecRequestProvider(providerCache, dgSpec)
                };

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

        private static NuGetVersion GetPackageVersionFromRestoreResult(RestoreResultPair restorePreviewResult,
            PackageReferenceArgs packageReferenceArgs,
            IEnumerable<NuGetFramework> UserSpecifiedFrameworks)
        {
            // Get the restore graphs from the restore result
            var restoreGraphs = restorePreviewResult
                .Result
                .RestoreGraphs;

            if (packageReferenceArgs.Frameworks?.Any() == true)
            {
                // If the user specified frameworks then we get the flattened graphs  only from the compatible frameworks.
                var userSpecifiedFrameworkSet = new HashSet<NuGetFramework>(
                    UserSpecifiedFrameworks, 
                    new NuGetFrameworkFullComparer());

                restoreGraphs = restoreGraphs
                    .Where(r => userSpecifiedFrameworkSet.Contains(r.Framework));
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