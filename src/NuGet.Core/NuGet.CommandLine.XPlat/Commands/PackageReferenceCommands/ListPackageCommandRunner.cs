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
using NuGet.Frameworks;
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
                    continue;
                }


                var lockFileFormat = new LockFileFormat();
                var assetsFile = lockFileFormat.Read(assetsPath);

                //Get all the packages that are referenced in a project
                var packages = msBuild.GetResolvedVersions(listPackageArgs.Frameworks, assetsFile, listPackageArgs.IncludeTransitive);

                //No packages means that no package references at all were found 
                if (!packages.Any())
                {
                    Console.WriteLine(listPackageArgs.Frameworks.Count() == 0 ? string.Format(Strings.ListPkg_NoPackagesFound, projectName) : string.Format(Strings.ListPkg_NoPackagesFoundForFrameworks, projectName));
                }
                else
                {
                    //Handle outdated
                    if (listPackageArgs.IncludeOutdated)
                    {
                        packages = await AddLatestVersions(packages, listPackageArgs);
                    }

                    //Printing packages of a single project and keeping track if
                    //an auto-referenced package was printed
                    var autoRefFoundWithinProject = false;
                    PrintProjectPackages(packages, projectName, listPackageArgs.IncludeTransitive, listPackageArgs.IncludeOutdated, out autoRefFoundWithinProject);
                    autoReferenceFound = autoReferenceFound || autoRefFoundWithinProject;
                }

                //Unload project
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
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
        private async Task<IEnumerable<FrameworkPackages>> AddLatestVersions(
            IEnumerable<FrameworkPackages> packages, ListPackageArgs listPackageArgs)
        {
            var resultPackages = new List<FrameworkPackages>();

            var providers = Repository.Provider.GetCoreV3();

            foreach (var frameworkPackages in packages)
            {
                var updatedTopLevel = frameworkPackages.topLevelPackages.Select(async p =>
                        await GetLatestVersion(p, NuGetFramework.Parse(frameworkPackages.framework), listPackageArgs, providers));

                var updatedTransitive = frameworkPackages.transitivePacakges.Select(async p =>
                        await GetLatestVersion(p, NuGetFramework.Parse(frameworkPackages.framework), listPackageArgs, providers));


                var resolvedTopLevelPackages = await Task.WhenAll(updatedTopLevel);
                var resolvedTransitivePackages = await Task.WhenAll(updatedTransitive);

                var updatedPackages = new FrameworkPackages
                {
                    framework = frameworkPackages.framework,
                    topLevelPackages = resolvedTopLevelPackages,
                    transitivePacakges = resolvedTransitivePackages
                };
                resultPackages.Add(updatedPackages);

            }

            return resultPackages;
        }

        /// <summary>
        /// Fetches the latest version of the given package
        /// </summary>
        /// <param name="package">The package to get the latest version for</param>
        /// <param name="framework">The framework for the given package</param>
        /// <param name="listPackageArgs">List args for the token and source provider></param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <returns>An updated pacakge with the highest version at sources</returns>
        private async Task<PRPackage> GetLatestVersion(
            PRPackage package,
            NuGetFramework framework,
            ListPackageArgs listPackageArgs,
            IEnumerable<Lazy<INuGetResourceProvider>> providers)
        {
            var sources = listPackageArgs.PackageSources;
            var tasks = new List<Task<PRPackage>>();

            foreach (var packageSource in sources)
            {
                tasks.Add(GetLatestVersionPerSourceAsync(packageSource, listPackageArgs, package, framework, providers));
            }

            var packages = await Task.WhenAll(tasks);
            package.suggestedVersion = packages.Max(p => p.suggestedVersion);

            return package;
        }

        /// <summary>
        /// Gets the highest version of a package from a specific source
        /// </summary>
        /// <param name="packageSource">The source to look for pacakges at</param>
        /// <param name="listPackageArgs">The list args for the cancellation token</param>
        /// <param name="package">Package to look for updates for</param>
        /// <param name="framework">The framework for the given package</param>
        /// <param name="providers">The providers to use when looking at sources</param>
        /// <returns>An updated pacakge with the highest version at a single source</returns>
        private async Task<PRPackage> GetLatestVersionPerSourceAsync(
            PackageSource packageSource,
            ListPackageArgs listPackageArgs,
            PRPackage package,
            NuGetFramework framework,
            IEnumerable<Lazy<INuGetResourceProvider>> providers)
        {
            var sourceRepository = Repository.CreateSource(providers, packageSource, FeedType.Undefined);
            var dependencyInfoResource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>(listPackageArgs.CancellationToken);

            var packages = (await dependencyInfoResource.GetAllVersionsAsync(package.name, new SourceCacheContext(), NullLogger.Instance, listPackageArgs.CancellationToken)).ToList();
            
            var latestVersionAtSource = packages.Where(version => MeetsConstraints(version, package, listPackageArgs))
            .OrderByDescending(version => version, VersionComparer.Default)
            .FirstOrDefault();

            package.suggestedVersion = latestVersionAtSource;
            return package;
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
        private bool MeetsConstraints(NuGetVersion newVersion, PRPackage package, ListPackageArgs listPackageArgs)
        {
            NuGetVersion currentVersion;
            if (package.requestedVersion == null)
            {
                currentVersion = package.resolvedVersion;
            }
            else
            {
                currentVersion = package.requestedVersion.MaxVersion != null ?
                                 package.resolvedVersion : package.requestedVersion.MinVersion;
            }
                
            var result = !newVersion.IsPrerelease || listPackageArgs.Prerelease || package.resolvedVersion.IsPrerelease;

            if (listPackageArgs.HighestPatch)
            {
                result = newVersion.Minor.Equals(currentVersion.Minor) && newVersion.Major.Equals(currentVersion.Major) && result;
            }

            if (listPackageArgs.HighestMinor)
            {
                result = newVersion.Major.Equals(currentVersion.Major) && result;
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

            Console.WriteLine(string.Format(Strings.ListPkgProjectHeaderLog, projectName));

            foreach (var frameworkPackages in packages)
            {
                var frameworkTopLevelPackages = frameworkPackages.topLevelPackages;
                var frameworkTransitivePackages = frameworkPackages.transitivePacakges;

                //Filter the packages for outdated
                if (outdated)
                {
                    frameworkTopLevelPackages = frameworkTopLevelPackages.Where(p => !p.autoReference && p.resolvedVersion < p.suggestedVersion);
                    frameworkTransitivePackages = frameworkTransitivePackages.Where(p => p.resolvedVersion < p.suggestedVersion);
                }

                //If no packages exist for this framework, print the
                //appropriate message
                if (!frameworkTopLevelPackages.Any() && !frameworkTransitivePackages.Any())
                {
                    frameworkMessage.Append(": ");
                    Console.WriteLine(string.Format(frameworkMessage.ToString() + Strings.ListPkg_NoPackagesForFramework, frameworkPackages.framework));
                }
                else
                {
                    //Print name of the framework
                    Console.WriteLine(string.Format(frameworkMessage.ToString(), frameworkPackages.framework));

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
        private void PackagesTable(IEnumerable<PRPackage> packages, bool printingTransitive, bool outdated, out bool autoRefWithinPackagesList)
        {
            var autoReferenceFound = false;

            if (!packages.Any())
            {
                autoRefWithinPackagesList = false;
                return;
            }

            List<Tuple<string, ConsoleColor>> tableToPrint;
            var headers = BuildTableHeaders(printingTransitive, outdated);

            if (outdated && printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => "",
                       p => p.name,
                       p => "", p => p.resolvedVersion.ToString(),
                       p => p.suggestedVersion == null ? "Not found at sources" : p.suggestedVersion.ToString());
            }
            else if (outdated && !printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => "",
                       p => p.name,
                       p =>
                       {
                           if (p.autoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           return "";
                       },
                       p => p.printableRequestedVersion,
                       p => p.resolvedVersion.ToString(),
                       p => p.suggestedVersion == null ? "Not found at sources" : p.suggestedVersion.ToString());
            }
            else if (!outdated && printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                        headers,
                        p => "",
                        p => p.name,
                        p => "",
                        p => p.resolvedVersion.ToString());
            }
            else
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => "",
                       p => p.name,
                       p => {
                           if (p.autoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           return "";
                       },
                       p => p.printableRequestedVersion,
                       p => p.resolvedVersion.ToString());
            }

            //Handle printing with colors
            foreach (var line in tableToPrint)
            {
                if (line.Item2 != ConsoleColor.White)
                {
                    Console.ForegroundColor = line.Item2;
                }

                Console.WriteLine(line.Item1);
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
                result.Add("Transitive Package");
                result.Add("");
                result.Add("Resolved");
            }
            else
            {
                result.Add("Top-level Package");
                result.Add("");
                result.Add("Requested");
                result.Add("Resolved");
            }

            if (outdated)
            {
                result.Add("Latest");
            }

            return result.ToArray();
        }
    }

    internal struct FrameworkPackages
    {
        public string framework;
        public IEnumerable<PRPackage> topLevelPackages;
        public IEnumerable<PRPackage> transitivePacakges;
    }

    /// <summary>
    /// A struct to simplify holding all of the information
    /// about a package reference when using list
    /// </summary>
    internal struct PRPackage
    {
        public string name;
        public VersionRange requestedVersion;
        public string printableRequestedVersion;
        public NuGetVersion resolvedVersion;
        public NuGetVersion suggestedVersion;
        public bool autoReference;
    }
}