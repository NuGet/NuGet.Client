// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    internal class ListPackageCommandRunner : IListPackageCommandRunner
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";
        private Dictionary<PackageSource, SourceRepository> _sourceRepositoryCache;

        public ListPackageCommandRunner()
        {
            _sourceRepositoryCache = new Dictionary<PackageSource, SourceRepository>();
        }

        public async Task ExecuteCommandAsync(ListPackageArgs listPackageArgs)
        {
            IReportRenderer reportRenderer = listPackageArgs.Renderer;
            ListPackageReportModel reportModel = await GetReportDataAsync(listPackageArgs);
            // set renderer data
            reportRenderer.End(reportModel);
        }

        private async Task<ListPackageReportModel> GetReportDataAsync(ListPackageArgs listPackageArgs)
        {
            // It's important not to print anything to console from below methods and sub method calls, because it'll affect both json/console outputs.
            var listPackageReportModel = new ListPackageReportModel(listPackageArgs);
            if (!File.Exists(listPackageArgs.Path))
            {
                listPackageArgs.Renderer.AddProblem(errorText: string.Format(CultureInfo.CurrentCulture,
                        Strings.ListPkg_ErrorFileNotFound,
                        listPackageArgs.Path),
                        problemType: ProblemType.Error);
                return listPackageReportModel;
            }
            //If the given file is a solution, get the list of projects
            //If not, then it's a project, which is put in a list
            var projectsPaths = Path.GetExtension(listPackageArgs.Path).Equals(".sln") ?
                           MSBuildAPIUtility.GetProjectsFromSolution(listPackageArgs.Path).Where(f => File.Exists(f)) :
                           new List<string>(new string[] { listPackageArgs.Path });

            var autoReferenceFound = false;
            MSBuildAPIUtility msBuild = listPackageReportModel.MSBuildAPIUtility;

            foreach (string projectPath in projectsPaths)
            {
                // Project specific data stored in below variable
                ListPackageProjectModel projectModel = listPackageReportModel.CreateProjectReportData(projectPath: projectPath);
                Project project = projectModel.Project;

                if (!MSBuildAPIUtility.IsPackageReferenceProject(project))
                {
                    projectModel.AddProjectProblem(error: string.Format(CultureInfo.CurrentCulture,
                        Strings.Error_NotPRProject, projectPath),
                        problemType: ProblemType.Error);
                    continue;
                }

                var assetsPath = project.GetPropertyValue(ProjectAssetsFile);

                // If the file was not found, print an error message and continue to next project
                if (!File.Exists(assetsPath))
                {
                    projectModel.AddProjectProblem(error: string.Format(CultureInfo.CurrentCulture,
                        Strings.Error_AssetsFileNotFound, projectPath),
                        problemType: ProblemType.Error);
                }
                else
                {
                    var lockFileFormat = new LockFileFormat();
                    LockFile assetsFile = lockFileFormat.Read(assetsPath);

                    // Assets file validation
                    if (assetsFile.PackageSpec != null &&
                        assetsFile.Targets != null &&
                        assetsFile.Targets.Count != 0)
                    {
                        // Get all the packages that are referenced in a project
                        IEnumerable<FrameworkPackages> packages = projectModel.GetAssetFilePackages(assetsFile);

                        // If packages equals null, it means something wrong happened
                        // with reading the packages and it was handled and message printed
                        // in MSBuildAPIUtility function, but we need to move to the next project
                        if (packages != null)
                        {
                            // No packages means that no package references at all were found in the current framework
                            if (!packages.Any())
                            {
                                projectModel.AddProjectProblem(error: string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoPackagesFoundForFrameworks, projectModel.ProjectName), problemType: ProblemType.Information);
                            }
                            else
                            {
                                if (listPackageArgs.ReportType != ReportType.Default)  // generic list package is offline -- no server lookups
                                {
                                    PopulateSourceRepositoryCache(listPackageArgs);
                                    WarnForHttpSources(listPackageArgs, projectModel);
                                    await GetRegistrationMetadataAsync(packages, listPackageArgs);
                                    await AddLatestVersionsAsync(packages, listPackageArgs);
                                }

                                bool printPackages = projectModel.PrintPackagesFlag;

                                // Filter packages for dedicated reports, inform user if none
                                if (listPackageArgs.ReportType != ReportType.Default && !printPackages)
                                {
                                    switch (listPackageArgs.ReportType)
                                    {
                                        case ReportType.Outdated:
                                            projectModel.AddProjectProblem(string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoUpdatesForProject, projectModel.ProjectName), ProblemType.Information);
                                            break;
                                        case ReportType.Deprecated:
                                            projectModel.AddProjectProblem(string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoDeprecatedPackagesForProject, projectModel.ProjectName), ProblemType.Information);
                                            break;
                                        case ReportType.Vulnerable:
                                            projectModel.AddProjectProblem(string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoVulnerablePackagesForProject, projectModel.ProjectName), ProblemType.Information);
                                            break;
                                    }
                                }

                                printPackages = printPackages || ReportType.Default == listPackageArgs.ReportType;
                                if (printPackages)
                                {
                                    var hasAutoReference = false;
                                    List<ListPackageReportFrameworkPackage> projectFrameworkPackages = ProjectPackagesPrintUtility.GetPackagesMetaData(packages, listPackageArgs, ref hasAutoReference);
                                    projectModel.SetFrameworkPackageMetaData(projectFrameworkPackages);
                                    autoReferenceFound = autoReferenceFound || hasAutoReference;
                                }
                            }
                        }
                    }
                    else
                    {
                        projectModel.AddProjectProblem(error: string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_ErrorReadingAssetsFile, assetsPath),
                            problemType: ProblemType.Error);
                    }

                    // Unload project
                    ProjectCollection.GlobalProjectCollection.UnloadProject(project);
                }
            }

            if (autoReferenceFound)
            {
                listPackageReportModel.AutoReferenceFound = true;
            }

            return listPackageReportModel;
        }

        private static void WarnForHttpSources(ListPackageArgs listPackageArgs, ListPackageProjectModel projectModel)
        {
            List<PackageSource> httpPackageSources = null;
            foreach (PackageSource packageSource in listPackageArgs.PackageSources)
            {
                if (packageSource.IsHttp && !packageSource.IsHttps)
                {
                    if (httpPackageSources == null)
                    {
                        httpPackageSources = new();
                    }
                    httpPackageSources.Add(packageSource);
                }
            }

            if (httpPackageSources != null && httpPackageSources.Count != 0)
            {
                if (httpPackageSources.Count == 1)
                {
                    projectModel.AddProjectProblem(
                        string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage,
                        "list package",
                        httpPackageSources[0]),
                        problemType: ProblemType.Warning);
                }
                else
                {
                    projectModel.AddProjectProblem(
                        string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage_MultipleSources,
                        "list package",
                        Environment.NewLine + string.Join(Environment.NewLine, httpPackageSources.Select(e => e.Name))),
                        problemType: ProblemType.Warning);
                }
            }

        }

        /// <summary>
        /// Fetches the latest versions for all of the packages that are
        /// to be listed
        /// </summary>
        /// <param name="packages">The packages found in a project</param>
        /// <param name="listPackageArgs">List args for the token and source provider</param>
        /// <returns>A data structure like packages, but includes the latest versions</returns>
        private async Task AddLatestVersionsAsync(
            IEnumerable<FrameworkPackages> packages,
            ListPackageArgs listPackageArgs)
        {
            //Unique Dictionary for packages and list of latest versions to handle different sources
            var packagesVersionsDict = new Dictionary<string, IList<IPackageSearchMetadata>>();
            AddPackagesToDict(packages, packagesVersionsDict);

            //Prepare requests for each of the packages
            var getLatestVersionsRequests = new List<Task>();
            foreach (var package in packagesVersionsDict)
            {
                getLatestVersionsRequests.AddRange(
                    PrepareLatestVersionsRequests(
                        package.Key,
                        listPackageArgs,
                        packagesVersionsDict));
            }

            // Make requests in parallel.
            await RequestNuGetResourcesInParallelAsync(getLatestVersionsRequests);

            //Save latest versions within the InstalledPackageReference
            await GetVersionsFromDictAsync(packages, packagesVersionsDict, listPackageArgs);
        }

        /// <summary>
        /// Fetches additional info (e.g. deprecation, vulnerability) for all of the packages found in a project.
        /// </summary>
        /// <param name="packages">The packages found in a project.</param>
        /// <param name="listPackageArgs">List args for the token and source provider</param>
        private async Task GetRegistrationMetadataAsync(
            IEnumerable<FrameworkPackages> packages,
            ListPackageArgs listPackageArgs)
        {
            // Unique dictionary for packages and list of versions to handle different sources
            var packagesVersionsDict = new Dictionary<string, IList<IPackageSearchMetadata>>();
            AddPackagesToDict(packages, packagesVersionsDict);

            // Clone and filter package versions to avoid duplicate requests
            // and to avoid collection being enumerated to be modified.
            var distinctPackageVersionsDict = GetUniqueResolvedPackages(packages);

            // Prepare requests for each of the packages
            var resourceRequestTasks = new List<Task>();
            foreach (var packageIdAndVersions in distinctPackageVersionsDict)
            {
                foreach (var packageVersion in packageIdAndVersions.Value)
                {
                    resourceRequestTasks.AddRange(
                        PrepareCurrentVersionsRequests(
                            packageIdAndVersions.Key,
                            packageVersion,
                            listPackageArgs,
                            packagesVersionsDict));
                }
            }

            // Make requests in parallel.
            await RequestNuGetResourcesInParallelAsync(resourceRequestTasks);

            // Save resolved versions within the InstalledPackageReference
            await GetVersionsFromDictAsync(packages, packagesVersionsDict, listPackageArgs);
        }

        /// <summary>
        /// Pre-populate _sourceRepositoryCache so source repository can be reused between different calls.
        /// </summary>
        /// <param name="listPackageArgs">List args for the token and source provider</param>
        private void PopulateSourceRepositoryCache(ListPackageArgs listPackageArgs)
        {
            IEnumerable<Lazy<INuGetResourceProvider>> providers = Repository.Provider.GetCoreV3();
            IEnumerable<PackageSource> sources = listPackageArgs.PackageSources;
            foreach (PackageSource source in sources)
            {
                SourceRepository sourceRepository = Repository.CreateSource(providers, source, FeedType.Undefined);
                _sourceRepositoryCache[source] = sourceRepository;
            }
        }

        /// <summary>
        /// Handles concurrency and throttling for a list of tasks that request NuGet resources.
        /// </summary>
        /// <param name="resourceRequestTasks"></param>
        /// <returns></returns>
        private static async Task RequestNuGetResourcesInParallelAsync(IReadOnlyList<Task> resourceRequestTasks)
        {
            // Handling concurrency and throttling variables
            var maxTasks = Environment.ProcessorCount;
            var contactSourcesRunningTasks = new List<Task>();

            // Make the calls to the sources
            foreach (var requestTask in resourceRequestTasks)
            {
                contactSourcesRunningTasks.Add(Task.Run(() => requestTask));

                // Throttle if needed
                if (maxTasks <= contactSourcesRunningTasks.Count)
                {
                    var finishedTask = await Task.WhenAny(contactSourcesRunningTasks);
                    contactSourcesRunningTasks.Remove(finishedTask);
                }
            }

            await Task.WhenAll(contactSourcesRunningTasks);
        }

        /// <summary>
        /// Adding the packages to a unique set to avoid attempting
        /// to get the latest versions for the same package multiple
        /// times
        /// </summary>
        /// <param name="packages"> Packages found in the project </param>
        /// <param name="packagesVersionsDict"> An empty dictionary to be filled with packages </param>
        private void AddPackagesToDict(IEnumerable<FrameworkPackages> packages,
            Dictionary<string, IList<IPackageSearchMetadata>> packagesVersionsDict)
        {
            foreach (var frameworkPackages in packages)
            {
                foreach (var topLevelPackage in frameworkPackages.TopLevelPackages)
                {
                    if (!packagesVersionsDict.ContainsKey(topLevelPackage.Name))
                    {
                        packagesVersionsDict.Add(topLevelPackage.Name, new List<IPackageSearchMetadata>());
                    }
                }

                foreach (var transitivePackage in frameworkPackages.TransitivePackages)
                {
                    if (!packagesVersionsDict.ContainsKey(transitivePackage.Name))
                    {
                        packagesVersionsDict.Add(transitivePackage.Name, new List<IPackageSearchMetadata>());
                    }
                }
            }
        }

        /// <summary>
        /// Returns the unique resolved packages to avoid duplicates.
        /// </summary>
        /// <param name="packages">Packages found in the project</param>
        private static IDictionary<string, IList<NuGetVersion>> GetUniqueResolvedPackages(IEnumerable<FrameworkPackages> packages)
        {
            var results = new Dictionary<string, IList<NuGetVersion>>();

            foreach (var frameworkPackages in packages)
            {
                foreach (var topLevelPackage in frameworkPackages.TopLevelPackages)
                {
                    if (!results.ContainsKey(topLevelPackage.Name))
                    {
                        results.Add(topLevelPackage.Name, new List<NuGetVersion>
                        {
                            topLevelPackage.ResolvedPackageMetadata.Identity.Version
                        });
                    }
                    else
                    {
                        var versions = results[topLevelPackage.Name];

                        if (!versions.Contains(topLevelPackage.ResolvedPackageMetadata.Identity.Version))
                        {
                            versions.Add(topLevelPackage.ResolvedPackageMetadata.Identity.Version);
                        }
                    }
                }

                foreach (var transitivePackage in frameworkPackages.TransitivePackages)
                {
                    if (!results.ContainsKey(transitivePackage.Name))
                    {
                        results.Add(transitivePackage.Name, new List<NuGetVersion>
                        {
                            transitivePackage.ResolvedPackageMetadata.Identity.Version
                        });
                    }
                    else
                    {
                        var versions = results[transitivePackage.Name];

                        if (!versions.Contains(transitivePackage.ResolvedPackageMetadata.Identity.Version))
                        {
                            versions.Add(transitivePackage.ResolvedPackageMetadata.Identity.Version);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Get last versions for every package from the unique packages
        /// </summary>
        /// <param name="packages"> Project packages to get filled with latest versions </param>
        /// <param name="packagesVersionsDict"> Unique packages that are mapped to latest versions
        /// from different sources </param>
        /// <param name="listPackageArgs">Arguments for list package to get the right latest version</param>
        private async Task GetVersionsFromDictAsync(
            IEnumerable<FrameworkPackages> packages,
            Dictionary<string, IList<IPackageSearchMetadata>> packagesVersionsDict,
            ListPackageArgs listPackageArgs)
        {
            foreach (var frameworkPackages in packages)
            {
                foreach (var topLevelPackage in frameworkPackages.TopLevelPackages)
                {
                    var matchingPackage = packagesVersionsDict.Where(p => p.Key.Equals(topLevelPackage.Name, StringComparison.OrdinalIgnoreCase)).First();

                    // Get latest metadata *only* if this is a report requiring "outdated" metadata
                    if (listPackageArgs.ReportType == ReportType.Outdated && matchingPackage.Value.Count > 0)
                    {
                        var latestVersion = matchingPackage.Value.Where(newVersion => MeetsConstraints(newVersion.Identity.Version, topLevelPackage, listPackageArgs)).Max(i => i.Identity.Version);

                        topLevelPackage.LatestPackageMetadata = matchingPackage.Value.First(p => p.Identity.Version == latestVersion);
                        topLevelPackage.UpdateLevel = GetUpdateLevel(topLevelPackage.ResolvedPackageMetadata.Identity.Version, topLevelPackage.LatestPackageMetadata.Identity.Version);
                    }

                    var matchingPackagesWithDeprecationMetadata = await Task.WhenAll(
                        matchingPackage.Value.Select(async v => new { SearchMetadata = v, DeprecationMetadata = await v.GetDeprecationMetadataAsync() }));

                    // Update resolved version with additional metadata information returned by the server.
                    var resolvedVersionFromServer = matchingPackagesWithDeprecationMetadata
                        .Where(v => v.SearchMetadata.Identity.Version == topLevelPackage.ResolvedPackageMetadata.Identity.Version &&
                                (v.DeprecationMetadata != null || v.SearchMetadata?.Vulnerabilities != null))
                        .FirstOrDefault();

                    if (resolvedVersionFromServer != null)
                    {
                        topLevelPackage.ResolvedPackageMetadata = resolvedVersionFromServer.SearchMetadata;
                    }
                }

                foreach (var transitivePackage in frameworkPackages.TransitivePackages)
                {
                    var matchingPackage = packagesVersionsDict.Where(p => p.Key.Equals(transitivePackage.Name, StringComparison.OrdinalIgnoreCase)).First();

                    // Get latest metadata *only* if this is a report requiring "outdated" metadata
                    if (listPackageArgs.ReportType == ReportType.Outdated && matchingPackage.Value.Count > 0)
                    {
                        var latestVersion = matchingPackage.Value.Where(newVersion => MeetsConstraints(newVersion.Identity.Version, transitivePackage, listPackageArgs)).Max(i => i.Identity.Version);

                        transitivePackage.LatestPackageMetadata = matchingPackage.Value.First(p => p.Identity.Version == latestVersion);
                        transitivePackage.UpdateLevel = GetUpdateLevel(transitivePackage.ResolvedPackageMetadata.Identity.Version, transitivePackage.LatestPackageMetadata.Identity.Version);
                    }

                    var matchingPackagesWithDeprecationMetadata = await Task.WhenAll(
                        matchingPackage.Value.Select(async v => new { SearchMetadata = v, DeprecationMetadata = await v.GetDeprecationMetadataAsync() }));

                    // Update resolved version with additional metadata information returned by the server.
                    var resolvedVersionFromServer = matchingPackagesWithDeprecationMetadata
                        .Where(v => v.SearchMetadata.Identity.Version == transitivePackage.ResolvedPackageMetadata.Identity.Version &&
                                (v.DeprecationMetadata != null || v.SearchMetadata?.Vulnerabilities != null))
                        .FirstOrDefault();

                    if (resolvedVersionFromServer != null)
                    {
                        transitivePackage.ResolvedPackageMetadata = resolvedVersionFromServer.SearchMetadata;
                    }
                }
            }
        }

        /// <summary>
        /// Update Level is used to determine the print color for the latest
        /// version, which depends on changing major, minor or patch
        /// </summary>
        /// <param name="resolvedVersion"> Package's resolved version </param>
        /// <param name="latestVersion"> Package's latest version </param>
        /// <returns></returns>
        private UpdateLevel GetUpdateLevel(NuGetVersion resolvedVersion, NuGetVersion latestVersion)
        {
            if (latestVersion == null) return UpdateLevel.NoUpdate;
            if (resolvedVersion.Major != latestVersion.Major)
            {
                return UpdateLevel.Major;
            }
            else if (resolvedVersion.Minor != latestVersion.Minor)
            {
                return UpdateLevel.Minor;
            }
            //Patch or less important version props are different
            else if (resolvedVersion != latestVersion)
            {
                return UpdateLevel.Patch;
            }
            return UpdateLevel.NoUpdate;
        }

        /// <summary>
        /// Prepares the calls to sources for latest versions and updates
        /// the list of tasks with the requests
        /// </summary>
        /// <param name="package">The package to get the latest version for</param>
        /// <param name="listPackageArgs">List args for the token and source provider></param>
        /// <param name="packagesVersionsDict">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>A list of tasks for all latest versions for packages from all sources</returns>
        private IList<Task> PrepareLatestVersionsRequests(
            string package,
            ListPackageArgs listPackageArgs,
            Dictionary<string, IList<IPackageSearchMetadata>> packagesVersionsDict)
        {
            var latestVersionsRequests = new List<Task>();
            var sources = listPackageArgs.PackageSources;
            foreach (var packageSource in sources)
            {
                latestVersionsRequests.Add(GetLatestVersionPerSourceAsync(packageSource, listPackageArgs, package, packagesVersionsDict));
            }
            return latestVersionsRequests;
        }

        /// <summary>
        /// Prepares the calls to sources for current versions and updates
        /// the list of tasks with the requests
        /// </summary>
        /// <param name="packageId">The package ID to get the current version metadata for</param>
        /// <param name="requestedVersion">The version of the requested package</param>
        /// <param name="listPackageArgs">List args for the token and source provider></param>
        /// <param name="packagesVersionsDict">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>A list of tasks for all current versions for packages from all sources</returns>
        private IList<Task> PrepareCurrentVersionsRequests(
            string packageId,
            NuGetVersion requestedVersion,
            ListPackageArgs listPackageArgs,
            Dictionary<string, IList<IPackageSearchMetadata>> packagesVersionsDict)
        {
            var requests = new List<Task>();
            var sources = listPackageArgs.PackageSources;

            foreach (var packageSource in sources)
            {
                requests.Add(
                    GetPackageMetadataFromSourceAsync(
                        packageSource,
                        listPackageArgs,
                        packageId,
                        requestedVersion,
                        packagesVersionsDict));
            }

            return requests;
        }

        /// <summary>
        /// Gets the highest version of a package from a specific source
        /// </summary>
        /// <param name="packageSource">The source to look for packages at</param>
        /// <param name="listPackageArgs">The list args for the cancellation token</param>
        /// <param name="package">Package to look for updates for</param>
        /// <param name="packagesVersionsDict">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>An updated package with the highest version at a single source</returns>
        private async Task GetLatestVersionPerSourceAsync(
            PackageSource packageSource,
            ListPackageArgs listPackageArgs,
            string package,
            Dictionary<string, IList<IPackageSearchMetadata>> packagesVersionsDict)
        {
            SourceRepository sourceRepository = _sourceRepositoryCache[packageSource];
            var packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(listPackageArgs.CancellationToken);

            using var sourceCacheContext = new SourceCacheContext();
            var packages = await packageMetadataResource.GetMetadataAsync(
                package,
                includePrerelease: listPackageArgs.Prerelease,
                includeUnlisted: false,
                sourceCacheContext: sourceCacheContext,
                log: listPackageArgs.Logger,
                token: listPackageArgs.CancellationToken);

            var latestVersionsForPackage = packagesVersionsDict.Where(p => p.Key.Equals(package, StringComparison.OrdinalIgnoreCase)).Single().Value;
            latestVersionsForPackage.AddRange(packages);
        }

        /// <summary>
        /// Gets the requested version of a package from a specific source
        /// </summary>
        /// <param name="packageSource">The source to look for packages at</param>
        /// <param name="listPackageArgs">The list args for the cancellation token</param>
        /// <param name="packageId">Package to look for</param>
        /// <param name="requestedVersion">Requested package version</param>
        /// <param name="packagesVersionsDict">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>An updated package with the resolved version metadata from a single source</returns>
        private async Task GetPackageMetadataFromSourceAsync(
            PackageSource packageSource,
            ListPackageArgs listPackageArgs,
            string packageId,
            NuGetVersion requestedVersion,
            Dictionary<string, IList<IPackageSearchMetadata>> packagesVersionsDict)
        {
            SourceRepository sourceRepository = _sourceRepositoryCache[packageSource];
            var packageMetadataResource = await sourceRepository
                .GetResourceAsync<PackageMetadataResource>(listPackageArgs.CancellationToken);

            using var sourceCacheContext = new SourceCacheContext();
            var packages = await packageMetadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: true, // Include unlisted because deprecated packages may be unlisted.
                sourceCacheContext: sourceCacheContext,
                log: listPackageArgs.Logger,
                token: listPackageArgs.CancellationToken);

            var resolvedVersionsForPackage = packagesVersionsDict
                .Where(p => p.Key.Equals(packageId, StringComparison.OrdinalIgnoreCase))
                .Single()
                .Value;

            var resolvedPackageVersionMetadata = packages.SingleOrDefault(p => p.Identity.Version.Equals(requestedVersion));
            if (resolvedPackageVersionMetadata != null)
            {
                // Package version metadata found on source
                resolvedVersionsForPackage.Add(resolvedPackageVersionMetadata);
            }
        }

        /// <summary>
        /// Given a found version from a source and the current version and the args
        /// of list package, this function checks if the found version meets the required
        /// highest-patch, highest-minor or prerelease
        /// </summary>
        /// <param name="newVersion">Version from a source</param>
        /// <param name="package">The required package with its current version</param>
        /// <param name="listPackageArgs">Used to get the constraints</param>
        /// <returns>Whether the new version meets the constraints or not</returns>
        private bool MeetsConstraints(NuGetVersion newVersion, InstalledPackageReference package, ListPackageArgs listPackageArgs)
        {
            var result = !newVersion.IsPrerelease || listPackageArgs.Prerelease || package.ResolvedPackageMetadata.Identity.Version.IsPrerelease;

            if (listPackageArgs.HighestPatch)
            {
                result = newVersion.Minor.Equals(package.ResolvedPackageMetadata.Identity.Version.Minor) && newVersion.Major.Equals(package.ResolvedPackageMetadata.Identity.Version.Major) && result;
            }

            if (listPackageArgs.HighestMinor)
            {
                result = newVersion.Major.Equals(package.ResolvedPackageMetadata.Identity.Version.Major) && result;
            }

            return result;
        }
    }
}
