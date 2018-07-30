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
            
            var projectsPaths = Path.GetExtension(listPackageArgs.Path).Equals(".sln")
                           ?
                           MSBuildAPIUtility.GetProjectsFromSolution(listPackageArgs.Path)
                           .Where(f => File.Exists(f))
                           :
                           new List<string>(new string[] { listPackageArgs.Path });

            foreach (var projectPath in projectsPaths)
            {

                var project = MSBuildAPIUtility.GetProject(projectPath);
               
                var packages = msBuild.GetPackageReferencesFromAssets(project, projectPath, listPackageArgs.Frameworks, listPackageArgs.Transitive);

                if (packages != null)
                {
                    PrintProjectPackages(packages, project.GetPropertyValue("MSBuildProjectName"), listPackageArgs.Transitive);
                }

                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }
            if (_autoReferenceFound)
            {
                Console.WriteLine(Strings.ListPkg_AutoReferenceDescription);
            }

        }

        private void PrintProjectPackages(Dictionary<string, Tuple<IEnumerable<PRPackage>, IEnumerable<PRPackage>>> packages,
           string projectName, bool transitive)
        {

            Console.WriteLine(string.Format(Strings.ListPkgProjectHeaderLog, projectName));

            foreach (var frameworkPackages in packages)
            {
                Console.WriteLine(string.Format("    '{0}'", frameworkPackages.Key));

                Console.WriteLine(PackagesTable(frameworkPackages.Value.Item1, false));

                if (transitive)
                {
                    Console.WriteLine(PackagesTable(frameworkPackages.Value.Item2, true));
                }

            }

            
        }

        private string PackagesTable(IEnumerable<PRPackage> packages, bool transitive)
        {
            if (packages.Count() == 0) return "";
            var sb = new StringBuilder();

            var padLeft = "  ";

            if (transitive)
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