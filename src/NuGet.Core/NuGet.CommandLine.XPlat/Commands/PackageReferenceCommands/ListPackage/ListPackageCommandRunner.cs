// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Common;
using NuGet.Configuration;
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
        private const string LeftPadding = "   ";

        public async Task ExecuteCommandAsync(ListPackageArgs listPackageArgs)
        {
            //If the given file is a solution, get the list of projects
            //If not, then it's a project, which is put in a list
            var projectsPaths = Path.GetExtension(listPackageArgs.Path).Equals(".sln")?
                           MSBuildAPIUtility.GetProjectsFromSolution(listPackageArgs.Path).Where(f => File.Exists(f)):
                           new List<string>(new string[] { listPackageArgs.Path });

            var autoReferenceFound = false;

            var msBuild = new MSBuildAPIUtility(listPackageArgs.Logger);

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
                        var packages = msBuild.GetResolvedVersions(project, listPackageArgs.Frameworks, assetsFile, listPackageArgs.IncludeTransitive);

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
                                //Handle outdated
                                if (listPackageArgs.IncludeOutdated)
                                {
                                    await AddLatestVersionsAsync(packages, listPackageArgs);
                                }

                                //Printing packages of a single project and keeping track if
                                //an auto-referenced package was printed
                                PrintProjectPackages(packages, projectName, listPackageArgs.IncludeTransitive, listPackageArgs.IncludeOutdated, out var autoRefFoundWithinProject);
                                autoReferenceFound = autoReferenceFound || autoRefFoundWithinProject;
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
            var packagesTasks = new List<Task>();
            var uniquePackages = new HashSet<InstalledPackageReference>(new PackageReferenceComparerByName());

            AddPackagesToUniqueSet(packages, uniquePackages);

            foreach (var package in uniquePackages)
            {
                packagesTasks.Add(UpdatePackageLatestVersionAsync(package, listPackageArgs, providers));
            }
            await Task.WhenAll(packagesTasks);

            GetVersionsFromUniqueSet(packages, uniquePackages);
        }

        /// <summary>
        /// Adding the packages to a unique set to avoid attempting
        /// to get the latest versions for the same package multiple
        /// times
        /// </summary>
        /// <param name="packages"> Packages found in the project </param>
        /// <param name="uniquePackages"> An empty set to be filled with packages </param>
        private void AddPackagesToUniqueSet(IEnumerable<FrameworkPackages> packages, HashSet<InstalledPackageReference> uniquePackages)
        {
            foreach (var frameworkPackages in packages)
            {
                foreach (var topLevelPackage in frameworkPackages.TopLevelPackages)
                {
                    if (!uniquePackages.Contains(topLevelPackage))
                    {
                        uniquePackages.Add(topLevelPackage);
                    }
                }

                foreach (var transitivePackage in frameworkPackages.TransitivePacakges)
                {
                    if (!uniquePackages.Contains(transitivePackage))
                    {
                        uniquePackages.Add(transitivePackage);
                    }
                }
            }
        }

        /// <summary>
        /// Get last versions for every package from the unqiue packages
        /// </summary>
        /// <param name="packages"> Project packages to get filled with latest versions </param>
        /// <param name="uniquePackages"> Unique packages that have latest versions </param>
        private void GetVersionsFromUniqueSet(IEnumerable<FrameworkPackages> packages, HashSet<InstalledPackageReference> uniquePackages)
        {
            foreach (var frameworkPackages in packages)
            {
                foreach (var topLevelPackage in frameworkPackages.TopLevelPackages)
                {
                    var matchingPackage = uniquePackages.Where(p => p.Name.Equals(topLevelPackage.Name, StringComparison.OrdinalIgnoreCase)).Single();
                    topLevelPackage.LatestVersion = matchingPackage.LatestVersion;
                    topLevelPackage.UpdateLevel = GetUpdateLevel(topLevelPackage.ResolvedVersion, topLevelPackage.LatestVersion);
                }

                foreach (var transitivePackage in frameworkPackages.TransitivePacakges)
                {
                    var matchingPackage = uniquePackages.Where(p => p.Name.Equals(transitivePackage.Name, StringComparison.OrdinalIgnoreCase)).Single();
                    transitivePackage.LatestVersion = matchingPackage.LatestVersion;
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
            if (resolvedVersion.Major != latestVersion.Major)
            {
                return UpdateLevel.Major;
            }
            else if (resolvedVersion.Minor != latestVersion.Minor)
            {
                return UpdateLevel.Minor;
            }
            else if (resolvedVersion.Patch != latestVersion.Patch)
            {
                return UpdateLevel.Patch;
            }
            return UpdateLevel.NoUpdate;
        }

        /// <summary>
        /// Fetches the latest version of the given package
        /// </summary>
        /// <param name="package">The package to get the latest version for</param>
        /// <param name="listPackageArgs">List args for the token and source provider></param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <returns>An updated pacakge with the highest version at sources</returns>
        private async Task UpdatePackageLatestVersionAsync(
            InstalledPackageReference package,
            ListPackageArgs listPackageArgs,
            IEnumerable<Lazy<INuGetResourceProvider>> providers)
        {
            var sources = listPackageArgs.PackageSources;
            var tasks = new List<Task<NuGetVersion>>();

            foreach (var packageSource in sources)
            {
                tasks.Add(GetLatestVersionPerSourceAsync(packageSource, listPackageArgs, package, providers));
            }

            var versions = await Task.WhenAll(tasks);
            package.LatestVersion = versions.Max();
        }

        /// <summary>
        /// Gets the highest version of a package from a specific source
        /// </summary>
        /// <param name="packageSource">The source to look for pacakges at</param>
        /// <param name="listPackageArgs">The list args for the cancellation token</param>
        /// <param name="package">Package to look for updates for</param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <returns>An updated pacakge with the highest version at a single source</returns>
        private async Task<NuGetVersion> GetLatestVersionPerSourceAsync(
            PackageSource packageSource,
            ListPackageArgs listPackageArgs,
            InstalledPackageReference package,
            IEnumerable<Lazy<INuGetResourceProvider>> providers)
        {
            var sourceRepository = Repository.CreateSource(providers, packageSource, FeedType.Undefined);
            var dependencyInfoResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>(listPackageArgs.CancellationToken);

            var packages = (await dependencyInfoResource.GetAllVersionsAsync(package.Name, new SourceCacheContext(), NullLogger.Instance, listPackageArgs.CancellationToken));
            
            var latestVersionAtSource = packages.Where(version => MeetsConstraints(version, package, listPackageArgs))
            .OrderByDescending(version => version, VersionComparer.Default)
            .FirstOrDefault();

            return latestVersionAtSource;

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


        /// <summary>
        /// A function that prints all the package references of a project
        /// </summary>
        /// <param name="packages">A list of framework packages. Check <see cref="FrameworkPackages"/></param>
        /// <param name="projectName">The project name</param>
        /// <param name="transitive">Whether include-transitive flag exists or not</param>
        /// <param name="outdated">Whether outdated flag exists or not</param>
        /// <param name="autoReferenceFound">An out to return whether autoreference was found</param>
        private void PrintProjectPackages(IEnumerable<FrameworkPackages> packages,
           string projectName, bool transitive, bool outdated, out bool autoReferenceFound)
        {
            autoReferenceFound = false;

            var frameworkMessage = new StringBuilder(LeftPadding);
            frameworkMessage.Append("'{0}'");

            Console.WriteLine(string.Format(Strings.ListPkg_ProjectHeaderLog, projectName));

            foreach (var frameworkPackages in packages)
            {
                var frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;
                var frameworkTransitivePackages = frameworkPackages.TransitivePacakges;

                //Filter the packages for outdated
                if (outdated)
                {
                    frameworkTopLevelPackages = frameworkTopLevelPackages.Where(p => !p.AutoReference && p.ResolvedVersion < p.LatestVersion);
                    frameworkTransitivePackages = frameworkTransitivePackages.Where(p => p.ResolvedVersion < p.LatestVersion);
                }

                //If no packages exist for this framework, print the
                //appropriate message
                if (!frameworkTopLevelPackages.Any() && !frameworkTransitivePackages.Any())
                {
                    frameworkMessage.Append(": ");
                    Console.WriteLine(string.Format(frameworkMessage.ToString() + Strings.ListPkg_NoPackagesForFramework, frameworkPackages.Framework));
                }
                else
                {
                    //Print name of the framework
                    Console.WriteLine(string.Format(frameworkMessage.ToString(), frameworkPackages.Framework));

                    //Print top-level packages
                    if (frameworkTopLevelPackages.Any())
                    {
                        var autoRefWithinPackagesList = false;
                        PackagesTable(frameworkTopLevelPackages, false, outdated, out autoRefWithinPackagesList);
                        autoReferenceFound = autoReferenceFound || autoRefWithinPackagesList;
                    }

                    //Print transitive pacakges
                    if (transitive && frameworkTransitivePackages.Any())
                    {
                        var autoRefWithinPackagesList = false;
                        PackagesTable(frameworkTransitivePackages, true, outdated, out autoRefWithinPackagesList);
                        autoReferenceFound = autoReferenceFound || autoRefWithinPackagesList;
                    }
                }
            }            
        }

        /// <summary>
        /// Given a list of packages, this function will print them in a table
        /// </summary>
        /// <param name="packages">The list of packages</param>
        /// <param name="printingTransitive">Whether the function is printing transitive
        /// packages table or not</param>
        /// <param name="outdated"></param>
        /// <param name="autoRefWithinPackagesList"></param>
        /// <returns>The table as a string</returns>
        private void PackagesTable(IEnumerable<InstalledPackageReference> packages, bool printingTransitive, bool outdated, out bool autoRefWithinPackagesList)
        {
            var autoReferenceFound = false;

            if (!packages.Any())
            {
                autoRefWithinPackagesList = false;
                return;
            }

            packages = packages.OrderBy(p => p.Name);

            //To enable coloring only the latest version as appropriate
            //we need to map every string in the table to a color, which
            //this is used for
            IEnumerable<Tuple<string, ConsoleColor>> tableToPrint;

            var headers = BuildTableHeaders(printingTransitive, outdated);

            if (outdated && printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Name,
                       p => "", p => p.ResolvedVersion.ToString(),
                       p => p.LatestVersion == null ? Strings.ListPkg_NotFoundAtSources : p.LatestVersion.ToString());
            }
            else if (outdated && !printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Name,
                       p =>
                       {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           return "";
                       },
                       p => p.OriginalRequestedVersion,
                       p => p.ResolvedVersion.ToString(),
                       p => p.LatestVersion == null ? Strings.ListPkg_NotFoundAtSources : p.LatestVersion.ToString());
            }
            else if (!outdated && printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                        headers,
                        p => p.UpdateLevel,
                        p => "",
                        p => p.Name,
                        p => "",
                        p => p.ResolvedVersion.ToString());
            }
            else
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Name,
                       p => {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           return "";
                       },
                       p => p.OriginalRequestedVersion,
                       p => p.ResolvedVersion.ToString());
            }

            //Handle printing with colors
            foreach (var text in tableToPrint)
            {
                if (text.Item2 != ConsoleColor.White)
                {
                    Console.ForegroundColor = text.Item2;
                }
                
                Console.Write(text.Item1);
                Console.ResetColor();
            }

            Console.WriteLine();
            autoRefWithinPackagesList = autoReferenceFound;
        }

        /// <summary>
        /// Prepares the headers for the tables that will be printed
        /// </summary>
        /// <param name="printingTransitive">Whether the table is for transitive or not</param>
        /// <param name="outdated">Whether we have an outdated/latest column or not</param>
        /// <returns></returns>
        private string[] BuildTableHeaders(bool printingTransitive, bool outdated)
        {
            var result = new List<string> { LeftPadding };
            if (printingTransitive)
            {
                result.Add(Strings.ListPkg_TransitiveHeader);
                result.Add("");
                result.Add(Strings.ListPkg_Resolved);
            }
            else
            {
                result.Add(Strings.ListPkg_TopLevelHeader);
                result.Add("");
                result.Add(Strings.ListPkg_Requested);
                result.Add(Strings.ListPkg_Resolved);
            }

            if (outdated)
            {
                result.Add(Strings.ListPkg_Latest);
            }

            return result.ToArray();
        }
    }
}