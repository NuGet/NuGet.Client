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
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
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
            var projectsPaths = Path.GetExtension(listPackageArgs.Path).Equals(".sln")?
                           MSBuildAPIUtility.GetProjectsFromSolution(listPackageArgs.Path).Where(f => File.Exists(f)):
                           new List<string>(new string[] { listPackageArgs.Path });

            var autoReferenceFound = false;

            var msBuild = new MSBuildAPIUtility(listPackageArgs.Logger);

            //Print sources
            if(listPackageArgs.IncludeOutdated)
            {
                Console.WriteLine();
                Console.WriteLine(Strings.ListPkg_SourcesUsedDescription);
                ProjectPackagesPrintUtility.PrintSources(listPackageArgs.PackageSources);
                Console.WriteLine();
            }

            //Loop through all of the project paths
            foreach (var projectPath in projectsPaths)
            {
                //Open project to evaluate properties for the assets
                //file and the name of the project
                var project = MSBuildAPIUtility.GetProject(projectPath);

                if (!MSBuildAPIUtility.IsPackageReferenceProject(project))
                {
                    Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture,
                        Strings.Error_NotPRProject,
                        projectPath));
                    Console.WriteLine();
                    continue;
                }

                var projectName = project.GetPropertyValue(ProjectName);

                var assetsPath = project.GetPropertyValue(ProjectAssetsFile);

                //If the file was not found, print an error message and continue to next project
                if (!File.Exists(assetsPath))
                {
                    Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture,
                        Strings.Error_AssetsFileNotFound,
                        projectPath));
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
                        var packages = msBuild.GetResolvedVersions(project.FullPath, listPackageArgs.Frameworks, assetsFile, listPackageArgs.IncludeTransitive);

                        // If packages equals null, it means something wrong happened
                        // with reading the packages and it was handled and message printed
                        // in MSBuildAPIUtility function, but we need to move to the next project
                        if (packages != null)
                        {
                            //No packages means that no package references at all were found 
                            if (!packages.Any())
                            {
                                Console.WriteLine(string.Format(Strings.ListPkg_NoPackagesFoundForFrameworks, projectName));
                            }
                            else
                            {
                                var printPackages = true;

                                //Handle outdated
                                if (listPackageArgs.IncludeOutdated)
                                {
                                    await AddLatestVersionsAsync(packages, listPackageArgs);
                                    var noPackagesLeft = true;

                                    //Filter the packages for outdated
                                    foreach (var frameworkPackages in packages)
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
                                    ProjectPackagesPrintUtility.PrintPackages(packages, projectName, listPackageArgs.IncludeTransitive, listPackageArgs.IncludeOutdated, out var autoRefFoundWithinProject);
                                    autoReferenceFound = autoReferenceFound || autoRefFoundWithinProject;
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(string.Format(Strings.ListPkg_ErrorReadingAssetsFile, assetsPath));
                    }

                    //Unload project
                    ProjectCollection.GlobalProjectCollection.UnloadProject(project);
                }
            }

            //If any auto-references were found, a line is printed
            //explaining what (A) means
            if (autoReferenceFound)
            {
                Console.WriteLine(Strings.ListPkg_AutoReferenceDescription);
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
            IEnumerable<FrameworkPackages> packages, ListPackageArgs listPackageArgs)
        {
            var providers = Repository.Provider.GetCoreV3();

            //Handling concurrency and throttling variables
            var maxTasks = Environment.ProcessorCount;
            var contactSourcesRunningTasks = new List<Task>();
            var latestVersionsRequests = new List<Task>();

            //Unique Dictionary for packages and list of latest versions to handle different sources
            var packagesVersionsDict = new Dictionary<string, IList<NuGetVersion>>();
            AddPackagesToDict(packages, packagesVersionsDict);

            //Prepare requests for each of the packages
            foreach (var package in packagesVersionsDict)
            {
                latestVersionsRequests.AddRange(PrepareLatestVersionsRequests(package.Key, listPackageArgs, providers, packagesVersionsDict));
            }

            //Make the calls to the sources
            foreach (var latestVersionTask in latestVersionsRequests)
            {
                contactSourcesRunningTasks.Add(Task.Run(() => latestVersionTask));
                //Throttle if needed
                if (maxTasks <= contactSourcesRunningTasks.Count)
                {
                    var finishedTask = await Task.WhenAny(contactSourcesRunningTasks);
                    contactSourcesRunningTasks.Remove(finishedTask);
                }
            }
            await Task.WhenAll(contactSourcesRunningTasks);

            //Save latest versions within the InstalledPackageReference
            GetVersionsFromDict(packages, packagesVersionsDict, listPackageArgs);
        }

        /// <summary>
        /// Adding the packages to a unique set to avoid attempting
        /// to get the latest versions for the same package multiple
        /// times
        /// </summary>
        /// <param name="packages"> Packages found in the project </param>
        /// <param name="packagesVersionsDict"> An empty dictionary to be filled with packages </param>
        private void AddPackagesToDict(IEnumerable<FrameworkPackages> packages,
            Dictionary<string, IList<NuGetVersion>> packagesVersionsDict)
        {
            foreach (var frameworkPackages in packages)
            {
                foreach (var topLevelPackage in frameworkPackages.TopLevelPackages)
                {
                    if (!packagesVersionsDict.ContainsKey(topLevelPackage.Name))
                    {
                        packagesVersionsDict.Add(topLevelPackage.Name, new List<NuGetVersion>());
                    }
                }

                foreach (var transitivePackage in frameworkPackages.TransitivePackages)
                {
                    if (!packagesVersionsDict.ContainsKey(transitivePackage.Name))
                    {
                        packagesVersionsDict.Add(transitivePackage.Name, new List<NuGetVersion>());
                    }
                }
            }
        }

        /// <summary>
        /// Get last versions for every package from the unqiue packages
        /// </summary>
        /// <param name="packages"> Project packages to get filled with latest versions </param>
        /// <param name="packagesVersionsDict"> Unique packages that are mapped to latest versions
        /// from different sources </param>
        /// <param name="listPackageArgs">Arguments for list package to get the right latest version</param>
        private void GetVersionsFromDict(IEnumerable<FrameworkPackages> packages,
            Dictionary<string, IList<NuGetVersion>> packagesVersionsDict,
            ListPackageArgs listPackageArgs)
        {
            foreach (var frameworkPackages in packages)
            {
                foreach (var topLevelPackage in frameworkPackages.TopLevelPackages)
                {
                    var matchingPackage = packagesVersionsDict.Where(p => p.Key.Equals(topLevelPackage.Name, StringComparison.OrdinalIgnoreCase)).First();
                    topLevelPackage.LatestVersion = matchingPackage.Value.Where(newVersion => MeetsConstraints(newVersion, topLevelPackage, listPackageArgs)).Max();
                    topLevelPackage.UpdateLevel = GetUpdateLevel(topLevelPackage.ResolvedVersion, topLevelPackage.LatestVersion);
                }

                foreach (var transitivePackage in frameworkPackages.TransitivePackages)
                {
                    var matchingPackage = packagesVersionsDict.Where(p => p.Key.Equals(transitivePackage.Name, StringComparison.OrdinalIgnoreCase)).First();
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
        /// <param name="package">The package to get the latest version for</param>
        /// <param name="listPackageArgs">List args for the token and source provider></param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <param name="packagesVersionsDict">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>A list of tasks for all latest versions for packages from all sources</returns>
        private IList<Task> PrepareLatestVersionsRequests(
            string package,
            ListPackageArgs listPackageArgs,
            IEnumerable<Lazy<INuGetResourceProvider>> providers,
            Dictionary<string, IList<NuGetVersion>> packagesVersionsDict)
        {
            var latestVersionsRequests = new List<Task>();
            var sources = listPackageArgs.PackageSources;
            foreach (var packageSource in sources)
            {
                latestVersionsRequests.Add(GetLatestVersionPerSourceAsync(packageSource, listPackageArgs, package, providers, packagesVersionsDict));
            }
            return latestVersionsRequests;
        }

        /// <summary>
        /// Gets the highest version of a package from a specific source
        /// </summary>
        /// <param name="packageSource">The source to look for pacakges at</param>
        /// <param name="listPackageArgs">The list args for the cancellation token</param>
        /// <param name="package">Package to look for updates for</param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <param name="packagesVersionsDict">A reference to the unique packages in the project
        /// to be able to handle different sources having different latest versions</param>
        /// <returns>An updated pacakge with the highest version at a single source</returns>
        private async Task GetLatestVersionPerSourceAsync(
            PackageSource packageSource,
            ListPackageArgs listPackageArgs,
            string package,
            IEnumerable<Lazy<INuGetResourceProvider>> providers,
            Dictionary<string, IList<NuGetVersion>> packagesVersionsDict)
        {
            var sourceRepository = Repository.CreateSource(providers, packageSource, FeedType.Undefined);
            var dependencyInfoResource = await sourceRepository.GetResourceAsync<MetadataResource>(listPackageArgs.CancellationToken);

            var packages = (await dependencyInfoResource.GetVersions(package, true, false, new SourceCacheContext(), NullLogger.Instance, listPackageArgs.CancellationToken));

            var latestVersionsForPackage = packagesVersionsDict.Where(p => p.Key.Equals(package, StringComparison.OrdinalIgnoreCase)).Single().Value;
            latestVersionsForPackage.AddRange(packages);

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
            var result = !newVersion.IsPrerelease || listPackageArgs.Prerelease || package.ResolvedVersion.IsPrerelease;

            if (listPackageArgs.HighestPatch)
            {
                result = newVersion.Minor.Equals(package.ResolvedVersion.Minor) && newVersion.Major.Equals(package.ResolvedVersion.Major) && result;
            }

            if (listPackageArgs.HighestMinor)
            {
                result = newVersion.Major.Equals(package.ResolvedVersion.Major) && result;
            }

            return result;
        }
    }
}