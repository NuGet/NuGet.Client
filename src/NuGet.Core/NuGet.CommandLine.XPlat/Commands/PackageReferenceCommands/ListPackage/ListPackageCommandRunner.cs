// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    public class ListPackageCommandRunner : IListPackageCommandRunner
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";
        private const string ProjectName = "MSBuildProjectName";

        public async Task ExecuteCommandAsync(ListPackageArgs listPackageArgs)
        {
            if (!File.Exists(listPackageArgs.Path))
            {
                Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture,
                        Strings.ListPkg_ErrorFileNotFound,
                        listPackageArgs.Path));
                return;
            }
            //If the given file is a solution, get the list of projects
            //If not, then it's a project, which is put in a list
            var projectsPaths = Path.GetExtension(listPackageArgs.Path).Equals(".sln") ?
                           MSBuildAPIUtility.GetProjectsFromSolution(listPackageArgs.Path) :
                           new List<string>(new string[] { listPackageArgs.Path });

            var msBuildUtility = new MSBuildAPIUtility(listPackageArgs.Logger);

            //Print sources
            if (listPackageArgs.IncludeOutdated)
            {
                Console.WriteLine();
                Console.WriteLine(Strings.ListPkg_SourcesUsedDescription);
                ProjectPackagesPrintUtility.PrintSources(listPackageArgs.PackageSources);
                Console.WriteLine();
            }

            List<ProjectInfo> projectInfos = new List<ProjectInfo>();
            var packageVersions = new Dictionary<string, HashSet<NuGetVersion>>();

            foreach (var projectPath in projectsPaths)
            {
                var projectInfo = await ProcessProject(projectPath, listPackageArgs, msBuildUtility);
                projectInfos.Add(projectInfo);

                foreach (var targetFrameworkInfo in projectInfo.TargetFrameworkInfos)
                {
                    foreach (var package in targetFrameworkInfo.TopLevelPackages)
                    {
                        CollectAllVersionsOfAllPackages(packageVersions, package);
                    }

                    foreach (var package in targetFrameworkInfo.TransitivePackages)
                    {
                        CollectAllVersionsOfAllPackages(packageVersions, package);
                    }
                }

                // TODO: do we need to unload the project anymore???
                //ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }

            if (listPackageArgs.IncludeOutdated)
            {
                await RetrievePackageVersionsForPossibleUpdates(packageVersions, listPackageArgs);
            }

            foreach (var projectInfo in projectInfos)
            {
                if (listPackageArgs.IncludeOutdated)
                {
                    StorePackageUpdateInformationInTargetFrameworkInfo(projectInfo.TargetFrameworkInfos, packageVersions, listPackageArgs);
                }

                OutputProject(projectInfo, listPackageArgs);
            }
        }

        private static void CollectAllVersionsOfAllPackages(Dictionary<string, HashSet<NuGetVersion>> packageVersions, PackageReferenceInfo packageReferenceInfo)
        {
            HashSet<NuGetVersion> versions;
            if (!packageVersions.ContainsKey(packageReferenceInfo.Id))
            {
                versions = new HashSet<NuGetVersion>();
                packageVersions.Add(packageReferenceInfo.Id, versions);
                versions.Add(packageReferenceInfo.ResolvedVersion);
            }
            else
            {
                versions = packageVersions[packageReferenceInfo.Id];
                if (!versions.Contains(packageReferenceInfo.ResolvedVersion))
                {
                    versions.Add(packageReferenceInfo.ResolvedVersion);
                }
            }
        }

        private async Task<ProjectInfo> ProcessProject(string projectPath, ListPackageArgs listPackageArgs, MSBuildAPIUtility msBuildUtility)
        {
            ProjectInfo projectInfo = null;
            Project project = null;

            if (File.Exists(projectPath))
            {
                try
                {
                    Console.WriteLine("loading: " + projectPath);
                    //Open project to evaluate properties for the assets
                    //file and the name of the project
                    project = MSBuildAPIUtility.GetProject(projectPath);
                }
                catch (InvalidProjectFileException)
                {
                    var assetsPath = Path.Combine(Path.GetDirectoryName(projectPath), "obj", "project.assets.json");

                    if (File.Exists(assetsPath))
                    {
                        projectInfo = ProcessPRBasedProject(projectPath, assetsPath, listPackageArgs, msBuildUtility);
                    }
                    else
                    {
                        projectInfo = new ProjectInfo(projectPath, null, ProjectStyle.Unknown);
                    }
                }
            }

            if (projectInfo == null)
            {
                if (project != null && MSBuildAPIUtility.IsPackageReferenceProject(project))
                {
                    var assetsPath = project.GetPropertyValue(ProjectAssetsFile);

                    projectInfo = ProcessPRBasedProject(projectPath, assetsPath, listPackageArgs, msBuildUtility);
                }
                else
                {
                    var packagesConfigPath = GetPackagesConfigFile(projectPath);
                    if (packagesConfigPath != null)
                    {
                        if (project != null)
                        {
                            var targetFrameworkMoniker = project.GetProperty("TargetFrameworkMoniker").EvaluatedValue;
                            projectInfo = await ProcessPCBasedProject(projectPath, targetFrameworkMoniker, packagesConfigPath, listPackageArgs);
                        }
                        else  // Project-less PC projects (like website projects)
                        {
                            projectInfo = await ProcessPCBasedProject(projectPath, targetFrameworkMoniker:null, packagesConfigPath, listPackageArgs);
                        }
                    }
                }
            }

            return projectInfo;
        }

        private void OutputProject(ProjectInfo projectInfo, ListPackageArgs listPackageArgs)
        {
            var autoReferenceFound = false;
            var projectPath = projectInfo.ProjectPath;
            var projectName = projectInfo.ProjectName;

            //No packages means that no package references at all were found 
            if (!projectInfo.TargetFrameworkInfos.Any())
            {
                // TODO: work on string...and make it a string table
                Console.WriteLine("Project '" + projectName + "' was not able to be loaded with .NET Core MsBuild - ******");

                //Console.WriteLine(string.Format(Strings.ListPkg_NoPackagesFoundForFrameworks,
                //                                projectInfo.ProjectPath));
                Console.WriteLine();
            }
            else
            {
                var printPackages = true;

                //Handle outdated
                if (listPackageArgs.IncludeOutdated)
                {
                    var noPackagesLeft = true;

                    //Filter the packages for outdated
                    foreach (var frameworkPackages in projectInfo.TargetFrameworkInfos)
                    {
                        frameworkPackages.TopLevelPackages = frameworkPackages.TopLevelPackages.Where(p => !p.AutoReference && (p.LatestVersion == null || p.ResolvedVersion < p.LatestVersion));
                        frameworkPackages.TopLevelPackages = frameworkPackages.TopLevelPackages.Where(p => p.LatestVersion == null || p.ResolvedVersion < p.LatestVersion);
                        if (frameworkPackages.TopLevelPackages.Any() || frameworkPackages.TopLevelPackages.Any())
                        {
                            noPackagesLeft = false;
                        }
                    }

                    // If after filtering, all packages were found up to date, inform the user
                    // and do not print anything
                    if (noPackagesLeft)
                    {
                        Console.WriteLine(string.Format(Strings.ListPkg_NoUpdatesForProject, projectName));
                        printPackages = false;
                    }
                }

                // Make sure print is still needed, which may be changed in case
                // outdated filtered all packages out
                if (printPackages)
                {
                    //Printing packages of a single project and keeping track if
                    //an auto-referenced package was printed
                    ProjectPackagesPrintUtility.PrintPackages(projectInfo.TargetFrameworkInfos,
                                                              projectName,
                                                              listPackageArgs.IncludeTransitive,
                                                              listPackageArgs.IncludeOutdated,
                                                              out var autoRefFoundWithinProject);
                    autoReferenceFound = autoReferenceFound || autoRefFoundWithinProject;
                }
            }

            //If any auto-references were found, a line is printed
            //explaining what (A) means
            if (autoReferenceFound)
            {
                Console.WriteLine(Strings.ListPkg_AutoReferenceDescription);
            }

            //TODO: don't hard code
            bool likelyTransitiveFound = true;
            if (likelyTransitiveFound)
            {
                //TODO: string table.
                //TDDO: description.
                //Console.WriteLine("(LT) : Likely Transitive - in a packages config project **** ");

                //Console.WriteLine(Strings.ListPkg_AutoReferenceDescription);
            }
        }

        // TODO: copied from RestoreCommand.cs (refactor as helper) - renamed function name. 
        // returns the package.config file associated with the project
        private static string GetPackagesConfigFile(string projectFile)
        {
            string projectName;
            string pathWithProjectName;
            string projectDirectory;

            // Some project types don't have a project file, but instead have a "folder" - like website projects.
            if (projectFile.EndsWith("\\"))
            {
                var dirInfo = new DirectoryInfo(projectFile);
                projectName = dirInfo.Name;
                projectDirectory = projectFile;

                pathWithProjectName = Path.Combine(
                    projectFile,
                    ConstructPackagesConfigFromProjectName(projectName));
            }
            else
            {
                projectName = Path.GetFileNameWithoutExtension(projectFile);
                projectDirectory = Path.GetDirectoryName(projectFile);
                pathWithProjectName = Path.Combine(
                    Path.GetDirectoryName(projectFile),
                    ConstructPackagesConfigFromProjectName(projectName));
            }

            if (File.Exists(pathWithProjectName))
            {
                return pathWithProjectName;
            }

            return Path.Combine(
                projectDirectory,
                CommandConstants.PackagesConfigFile);
        }

        // TODO: copied from RestoreCommand.cs (refactor as helper)
        private static string ConstructPackagesConfigFromProjectName(string projectName)
        {
            // we look for packages.<project name>.config file
            // but we don't want any space in the file name, so convert it to underscore.
            return "packages." + projectName.Replace(' ', '_') + ".config";
        }


        //New Utility Method: copied and refactored from NuGetPackageManager.cs (ui layer)
        private async Task<IEnumerable<PackageDependencyInfo>> GetDependencyInfoFromPackagesFolderAsync(IEnumerable<PackageIdentity> packageIdentities,
            NuGetFramework nuGetFramework,
            ISettings settings,
            string solutionFolderPath,
            bool includeUnresolved = false)
        {
            try
            {
                var results = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);
                var repositoryPath = PackagesFolderPathUtility.GetPackagesFolderPath(solutionFolderPath, settings);
                var sourceRepository = new SourceRepository(new PackageSource(repositoryPath, "solutionFolder"), Repository.Provider.GetCoreV3());
                var logger = NullLogger.Instance;
                var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();

                foreach (var packageIdentity in packageIdentities)
                {
                    var packageDependencyInfo = await dependencyInfoResource.ResolvePackage(packageIdentity, nuGetFramework, NullSourceCacheContext.Instance,
                        Common.NullLogger.Instance, CancellationToken.None);
                    if (packageDependencyInfo != null)
                    {
                        results.Add(packageDependencyInfo);
                    }
                    else if (includeUnresolved)
                    {
                        results.Add(new PackageDependencyInfo(packageIdentity, null));
                    }
                }
                return results;
            }
            catch (NuGetProtocolException)
            {
                return null;
            }
        }


        private async Task<ProjectInfo> ProcessPCBasedProject(string projectPath, string targetFrameworkMoniker, string packagesConfigPath, ListPackageArgs listPackageArgs)
        {
            var pcXml = XDocument.Load(packagesConfigPath);

            var packagesConfigReader = new PackagesConfigReader(pcXml);

            var projectName = projectPath.EndsWith("\\") ? (new DirectoryInfo(projectPath).Name) : Path.GetFileNameWithoutExtension(projectPath);

            var pcPackages = packagesConfigReader.GetPackages();

            var topLevelPackages = new List<PackageReferenceInfo>();
            var transitivePackages = new List<PackageReferenceInfo>();
            List<PackageIdentity> packageIdentities = new List<PackageIdentity>();

            foreach (var pcPackage in pcPackages)
            {
                PackageIdentity packageIdentity = new PackageIdentity(pcPackage.PackageIdentity.Id,
                    pcPackage.PackageIdentity.Version);
                packageIdentities.Add(packageIdentity);
            }

            Dictionary<string, int> mapOfDependencies = new Dictionary<string, int>();

            // When TFM isn't set, harvest it from the the first package in the Packages.Config file
            var targetFramework = targetFrameworkMoniker != null ? NuGetFramework.Parse(targetFrameworkMoniker)
                : (pcPackages.Count<PackageReference>() > 0 ? pcPackages.First<PackageReference>().TargetFramework : NuGetFramework.UnsupportedFramework);

            string solutionFolderPath = null;
            if (Path.GetExtension(listPackageArgs.Path).Equals(".sln"))
            {
                // If we understand the solution location, we can walk all the dependencyInfo in the packages
                solutionFolderPath = Path.GetDirectoryName(listPackageArgs.Path);
                var dependencyInfos = await GetDependencyInfoFromPackagesFolderAsync(packageIdentities, targetFramework,
                                                                     listPackageArgs.Settings, solutionFolderPath, includeUnresolved: true);
                foreach (var depInfo in dependencyInfos)
                {
                    foreach (var dep in depInfo.Dependencies)
                    {
                        if (!mapOfDependencies.ContainsKey(dep.Id))
                        {
                            mapOfDependencies.Add(dep.Id, 1);
                        }
                        else
                        {
                            mapOfDependencies[dep.Id] = mapOfDependencies[dep.Id] + 1;
                        }
                    }
                }

                foreach (var depInfo in dependencyInfos)
                {
                    var packageReferenceInfo = new PackageReferenceInfo(depInfo.Id)
                    {
                        ResolvedVersion = depInfo.Version,
                        OriginalRequestedVersion = depInfo.Version.ToString(),
                    };

                    topLevelPackages.Add(packageReferenceInfo);
                    if (mapOfDependencies.ContainsKey(depInfo.Id))
                    {
                        packageReferenceInfo.LikelyTransitive = true;
                    }
                }
            }
            else
            {
                foreach (var packageIdentity in packageIdentities)
                {
                    var packageReferenceInfo = new PackageReferenceInfo(packageIdentity.Id)
                    {
                        ResolvedVersion = packageIdentity.Version,
                        OriginalRequestedVersion = packageIdentity.Version.ToString(),
                    };

                    topLevelPackages.Add(packageReferenceInfo);
                }
            }

            var targetFrameworkInfo = new TargetFrameworkInfo(targetFramework,
                                                              topLevelPackages,
                                                              transitivePackages);

            var projectInfo = new ProjectInfo(projectName, projectPath, ProjectStyle.PackagesConfig);
            projectInfo.AddTargetFrameworkInfo(targetFrameworkInfo);
            return projectInfo;
        }

        private ProjectInfo ProcessPRBasedProject(string projectFilePath, string assetsPath, ListPackageArgs listPackageArgs, MSBuildAPIUtility msBuildUtility)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
            ProjectInfo projectInfo = null;

            //If the file was not found, print an error message and continue to next project
            if (!File.Exists(assetsPath))
            {
                Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture,
                    Strings.Error_AssetsFileNotFound,
                    projectFilePath));
                Console.WriteLine();
            }
            else
            {
                var lockFileFormat = new LockFileFormat();
                var assetsFile = lockFileFormat.Read(assetsPath);

                // Assets file validation
                if (assetsFile.PackageSpec != null &&
                    assetsFile.Targets != null &&
                    assetsFile.Targets.Count != 0)
                {
                    //Get all the packages that are referenced in a project
                    var targetFrameworkInfos = msBuildUtility.GetResolvedVersions(projectFilePath, listPackageArgs.Frameworks, assetsFile, listPackageArgs.IncludeTransitive);

                    projectInfo = new ProjectInfo(projectName, projectFilePath, ProjectStyle.PackageReference);
                    projectInfo.AddTargetFrameworkInfos(targetFrameworkInfos);
                }
                else
                {
                    Console.WriteLine(string.Format(Strings.ListPkg_ErrorReadingAssetsFile, assetsPath));
                }
            }

            return projectInfo;
        }

        /// <summary>
        /// Fetches the latest versions for all of the packages that are
        /// to be listed
        /// </summary>
        /// <param name="packageVersions">Map of packageId to a hashset of NuGetVersion</param>
        /// <param name="listPackageArgs">List args for the token and source provider</param>
        /// <returns>A data structure like packages, but includes the latest versions</returns>
        private async Task RetrievePackageVersionsForPossibleUpdates(
                Dictionary<string, HashSet<NuGetVersion>> packageVersions,
                ListPackageArgs listPackageArgs)
        {
            var providers = Repository.Provider.GetCoreV3();

            //Handling concurrency and throttling variables
            var maxTasks = Environment.ProcessorCount;
            var contactSourcesRunningTasks = new List<Task>();
            var latestVersionsRequests = new List<Task>();

            //Prepare requests for each of the packages
            foreach (var packageVersion in packageVersions)
            {
                latestVersionsRequests.AddRange(PrepareLatestVersionsRequests(packageVersion.Key, listPackageArgs, providers, packageVersions));
            }

            //Make the calls to the sources
            foreach (var latestVersionRequest in latestVersionsRequests)
            {
                contactSourcesRunningTasks.Add(Task.Run(() => latestVersionRequest));
                //Throttle if needed
                if (maxTasks <= contactSourcesRunningTasks.Count)
                {
                    var finishedTask = await Task.WhenAny(contactSourcesRunningTasks);
                    contactSourcesRunningTasks.Remove(finishedTask);
                }
            }
            await Task.WhenAll(contactSourcesRunningTasks);
        }

        /// <summary>
        /// Get last versions for every package from the unqiue packages
        /// </summary>
        /// <param name="targetFrameworkInfos"> Project packages to get filled with latest versions </param>
        /// <param name="packagesVersionsDict"> Unique packages that are mapped to latest versions
        /// from different sources </param>
        /// <param name="listPackageArgs">Arguments for list package to get the right latest version</param>
        private void StorePackageUpdateInformationInTargetFrameworkInfo(
            IEnumerable<TargetFrameworkInfo> targetFrameworkInfos,
            Dictionary<string, HashSet<NuGetVersion>> packagesVersionsDict,
            ListPackageArgs listPackageArgs)
        {
            foreach (var targetFrameworkInfo in targetFrameworkInfos)
            {
                foreach (var topLevelPackage in targetFrameworkInfo.TopLevelPackages)
                {
                    var matchingPackage = packagesVersionsDict.Where(p => p.Key.Equals(topLevelPackage.Id, StringComparison.OrdinalIgnoreCase)).First();
                    topLevelPackage.LatestVersion = matchingPackage.Value.Where(newVersion => MeetsConstraints(newVersion, topLevelPackage, listPackageArgs)).Max();
                    topLevelPackage.UpdateLevel = GetUpdateLevel(topLevelPackage.ResolvedVersion, topLevelPackage.LatestVersion);
                }

                foreach (var transitivePackage in targetFrameworkInfo.TransitivePackages)
                {
                    var matchingPackage = packagesVersionsDict.Where(p => p.Key.Equals(transitivePackage.Id, StringComparison.OrdinalIgnoreCase)).First();
                    transitivePackage.LatestVersion = matchingPackage.Value.Where(newVersion => MeetsConstraints(newVersion, transitivePackage, listPackageArgs)).Max();
                    transitivePackage.UpdateLevel = GetUpdateLevel(transitivePackage.ResolvedVersion, transitivePackage.LatestVersion);
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
        /// <param name="packageId">The package id to get the latest version for</param>
        /// <param name="listPackageArgs">List args for the token and source provider></param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <param name="packageVersions">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>A list of tasks for all latest versions for packages from all sources</returns>
        private IList<Task> PrepareLatestVersionsRequests(
            string packageId,
            ListPackageArgs listPackageArgs,
            IEnumerable<Lazy<INuGetResourceProvider>> providers,
            Dictionary<string, HashSet<NuGetVersion>> packageVersions)
        {
            var latestVersionsRequests = new List<Task>();
            var sources = listPackageArgs.PackageSources;
            foreach (var packageSource in sources)
            {
                latestVersionsRequests.Add(GetLatestVersionPerSourceAsync(packageSource, listPackageArgs, packageId, providers, packageVersions));
            }
            return latestVersionsRequests;
        }

        /// <summary>
        /// Gets the highest version of a package from a specific source
        /// </summary>
        /// <param name="packageSource">The source to look for pacakges at</param>
        /// <param name="listPackageArgs">The list args for the cancellation token</param>
        /// <param name="packageId">Package id to look for updates for</param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <param name="packageVersions">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>An updated pacakge with the highest version at a single source</returns>
        private async Task GetLatestVersionPerSourceAsync(
            PackageSource packageSource,
            ListPackageArgs listPackageArgs,
            string packageId,
            IEnumerable<Lazy<INuGetResourceProvider>> providers,
            Dictionary<string, HashSet<NuGetVersion>> packageVersions)
        {
            var sourceRepository = Repository.CreateSource(providers, packageSource, FeedType.Undefined);
            var dependencyInfoResource = await sourceRepository.GetResourceAsync<MetadataResource>(listPackageArgs.CancellationToken);

            var packages = (await dependencyInfoResource.GetVersions(packageId, true, false, new SourceCacheContext(), NullLogger.Instance, listPackageArgs.CancellationToken));

            var latestVersionsForPackage = packageVersions.Where(p => p.Key.Equals(packageId, StringComparison.OrdinalIgnoreCase)).Single().Value;
            latestVersionsForPackage.AddRange(packages);

        }

        /// <summary>
        /// Given a found version from a source and the current version and the args
        /// of list package, this function checks if the found version meets the required
        /// highest-patch, highest-minor or prerelease
        /// </summary>
        /// <param name="newVersion">Version from a source</param>
        /// <param name="packageReferenceInfo">The required package with its current version</param>
        /// <param name="listPackageArgs">Used to get the constraints</param>
        /// <returns>Whether the new version meets the constraints or not</returns>
        private bool MeetsConstraints(NuGetVersion newVersion, PackageReferenceInfo packageReferenceInfo, ListPackageArgs listPackageArgs)
        {
            var result = !newVersion.IsPrerelease || listPackageArgs.Prerelease || packageReferenceInfo.ResolvedVersion.IsPrerelease;

            if (listPackageArgs.HighestPatch)
            {
                result = newVersion.Minor.Equals(packageReferenceInfo.ResolvedVersion.Minor) && newVersion.Major.Equals(packageReferenceInfo.ResolvedVersion.Major) && result;
            }

            if (listPackageArgs.HighestMinor)
            {
                result = newVersion.Major.Equals(packageReferenceInfo.ResolvedVersion.Major) && result;
            }

            return result;
        }
    }
}