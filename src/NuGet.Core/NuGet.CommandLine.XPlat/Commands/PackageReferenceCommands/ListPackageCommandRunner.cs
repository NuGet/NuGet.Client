// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat.Utility;


/// <summary>
/// A struct to simplify holding all of the information
/// about a package reference when using list
/// </summary>
public struct PRPackage
{
    public string package;
    public string requestedVer;
    public string resolvedVer;
    public string suggestedVer;
    public bool deprecated;
    public bool autoRef;
}

namespace NuGet.CommandLine.XPlat
{
    public class ListPackageCommandRunner : IListPackageCommandRunner
    {
        private bool _autoReferenceFound = false;

        public void ExecuteCommand(ListPackageArgs listPackageArgs, MSBuildAPIUtility msBuild)
        {

            //If the given file is a solution, get the list of projects
            //If not, then it's a project, which is put in a list
            var projectsPaths = Path.GetExtension(listPackageArgs.Path).Equals(".sln")
                           ?
                           MSBuildAPIUtility.GetProjectsFromSolution(listPackageArgs.Path)
                           .Where(f => File.Exists(f))
                           :
                           new List<string>(new string[] { listPackageArgs.Path });

            //Loop through all of the project paths
            foreach (var projectPath in projectsPaths)
            {
                //Open project to evaluate properties
                var project = MSBuildAPIUtility.GetProject(projectPath);
                var projectName = project.GetPropertyValue("MSBuildProjectName");

                //Get all the packages that are referenced in a project
                var packages = msBuild.GetPackageReferencesForList(project, projectPath, listPackageArgs.Frameworks, listPackageArgs.Transitive);

                //A null return means that reading the assets file failed
                //or that no package references at all were found 
                if (packages != null)
                {
                    //The count is not 0 means that a package reference was found
                    if (packages.Count() != 0)
                    {
                        PrintProjectPackages(packages, projectName, listPackageArgs.Transitive);
                    }
                    else
                    {
                        Console.WriteLine(listPackageArgs.Frameworks.Count() == 0 ? string.Format(Strings.ListPkg_NoPackagesFound, projectName) : string.Format(Strings.ListPkg_NoPackagesFoundForFrameworks, projectName));
                    }
                    
                }

                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }

            //If any auto-references were found, a line is printed
            //explaining what (A) means
            if (_autoReferenceFound)
            {
                Console.WriteLine(Strings.ListPkg_AutoReferenceDescription);
            }

        }

        /// <summary>
        /// A function that prints all the information about the packages
        /// references by framework
        /// </summary>
        /// <param name="packages">A dictionary that maps a framework name to a tuple of 2
        /// enumerables of packages top-level and transitive in order</param>
        /// <param name="projectName">The project name</param>
        /// <param name="transitive">Whether include-transitive flag exists or not</param>
        private void PrintProjectPackages(Dictionary<string, Tuple<IEnumerable<PRPackage>, IEnumerable<PRPackage>>> packages,
           string projectName, bool transitive)
        {
            Console.WriteLine(string.Format(Strings.ListPkgProjectHeaderLog, projectName));

            foreach (var frameworkPackages in packages)
            {
                if (frameworkPackages.Value.Item1.Count() == 0)
                {
                    Console.WriteLine(string.Format("    '{0}': " + Strings.ListPkg_NoPackagesForFramework, frameworkPackages.Key));
                    continue;
                }
                Console.WriteLine(string.Format("    '{0}'", frameworkPackages.Key));

                Console.WriteLine(PackagesTable(frameworkPackages.Value.Item1, false));

                if (transitive)
                {
                    Console.WriteLine(PackagesTable(frameworkPackages.Value.Item2, true));
                }

            }

            
        }

        /// <summary>
        /// Given a list of packages, this function will print them in a table
        /// </summary>
        /// <param name="packages">The list of packages</param>
        /// <param name="printingTransitive">Whether the function is printing transitive
        /// packages table or not</param>
        /// <returns>The table as a string</returns>
        private string PackagesTable(IEnumerable<PRPackage> packages, bool printingTransitive)
        {
            if (packages.Count() == 0) return "";
            var sb = new StringBuilder();

            var padLeft = "  ";

            if (printingTransitive)
            {
                sb.Append(packages.ToStringTable(
                new[] { padLeft, "Transitive Packages", "", "Resolved" },
                p => "", p => p.package, p => "   ", p => p.resolvedVer
            ));
            }
            else
            {
                sb.Append(packages.ToStringTable(
                new[] { padLeft, "Top-level Package", "", "Requested", "Resolved" },
                p => "", p => p.package, p => {
                    if (p.autoRef)
                    {
                        _autoReferenceFound = true;
                        return "(A)";
                    }
                    return "   ";
                }, p => p.requestedVer, p => p.resolvedVer
            ));
            }

            return sb.ToString();
        }
    }
}