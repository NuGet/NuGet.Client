// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using NuGet.ProjectModel;
using NuGet.Shared;

namespace NuGet.CommandLine.XPlat
{
    internal class WhyCommandRunner : IWhyCommandRunner
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";
        private const string ProjectName = "MSBuildProjectName";

        private const string ChildNodeSymbol = "├─── ";
        private const string LastChildNodeSymbol = "└─── ";

        private const string ChildPrefixSymbol = "│    ";
        private const string LastChildPrefixSymbol = "     ";

        private const string DuplicateTreeSymbol = "└─── (*)";

        /// <summary>
        /// Use CLI arguments to execute why command.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments.</param>
        /// <returns></returns>
        public Task ExecuteCommandAsync(WhyCommandArgs whyCommandArgs)
        {
            //TestMethod();

            // TODO: figure out how to use current directory if path is not passed in
            var projectPaths = Path.GetExtension(whyCommandArgs.Path).Equals(".sln")
                                    ? MSBuildAPIUtility.GetProjectsFromSolution(whyCommandArgs.Path).Where(f => File.Exists(f))
                                    : new List<string>() { whyCommandArgs.Path };

            // the package you want to print the dependency paths for
            string package = whyCommandArgs.Package;

            var msBuild = new MSBuildAPIUtility(whyCommandArgs.Logger);

            foreach (var projectPath in projectPaths)
            {
                // Open project to evaluate properties for the assets
                // file and the name of the project
                Project project = MSBuildAPIUtility.GetProject(projectPath);

                if (!MSBuildAPIUtility.IsPackageReferenceProject(project))
                {
                    Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture,
                        Strings.Error_NotPRProject,
                        projectPath));
                    Console.WriteLine();
                    continue;
                }

                string projectName = project.GetPropertyValue(ProjectName);
                string assetsPath = project.GetPropertyValue(ProjectAssetsFile);

                // If the file was not found, print an error message and continue to next project
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
                    LockFile assetsFile = lockFileFormat.Read(assetsPath);

                    // Assets file validation
                    if (assetsFile.PackageSpec != null &&
                        assetsFile.Targets != null &&
                        assetsFile.Targets.Count != 0)
                    {

                        // Get all the packages that are referenced in a project
                        List<FrameworkPackages> packages = msBuild.GetResolvedVersions(project, whyCommandArgs.Frameworks, assetsFile, transitive: true);

                        // If packages equals null, it means something wrong happened
                        // with reading the packages and it was handled and message printed
                        // in MSBuildAPIUtility function, but we need to move to the next project
                        if (packages != null)
                        {
                            // No packages means that no package references at all were found in the current framework
                            if (!packages.Any())
                            {
                                Console.WriteLine(string.Format(Strings.WhyCommand_Error_NoPackagesFoundForFrameworks, projectName));
                            }
                            else
                            {
                                Console.Write($"Project '{projectName}' has the following dependency graph(s) for '{package}'\n");
                                FindDependencyGraphsForPackage(packages, assetsFile.Targets, package);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine(string.Format(Strings.ListPkg_ErrorReadingAssetsFile, assetsPath));
                    }

                    // Unload project
                    ProjectCollection.GlobalProjectCollection.UnloadProject(project);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Run the why command, print out output to console.
        /// </summary>
        /// <param name="packages">All packages in the project. Split up by top level packages and transitive packages.</param>
        /// <param name="targetFrameworks">All target frameworks in project and corresponding info about frameworks.</param>
        /// <param name="package">Package passed in as CLI argument.</param>
        private void FindDependencyGraphsForPackage(IEnumerable<FrameworkPackages> packages, IList<LockFileTarget> targetFrameworks, string package)
        {
            foreach (var frameworkPackages in packages)
            {
                LockFileTarget target = targetFrameworks.FirstOrDefault(i => i.TargetFramework.GetShortFolderName() == frameworkPackages.Framework);

                if (target != null)
                {
                    // print the framework name
                    Console.Write($"\n\t[{frameworkPackages.Framework}]:\n\n");

                    // get all the top level packages for the framework
                    IEnumerable<InstalledPackageReference> frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;

                    // get all package libraries for the framework
                    IList<LockFileTargetLibrary> libraries = target.Libraries;

                    List<List<DependencyNode>> dependencyGraph = OLDGetDependencyGraphPerFramework(frameworkTopLevelPackages, libraries, package);
                    //PrintDependencyGraphPerFramework(dependencyGraph);
                    AlternativePrintDependencyPaths(dependencyGraph);

                    List<DependencyNode> dependencyGraph2 = GetDependencyGraphsPerFramework(frameworkTopLevelPackages, libraries, frameworkPackages, package);
                    PrintDependencyGraphsPerFramework(dependencyGraph2);
                }
            }
        }

        /// <summary>
        /// Find all dependency paths.
        /// </summary>
        /// <param name="topLevelPackages">"root nodes" of the graph.</param>
        /// <param name="libraries">All libraries in a given project. </param>
        /// <param name="destination">The package name CLI argument.</param>
        /// <returns></returns>
        private List<List<DependencyNode>> OLDGetDependencyGraphPerFramework(IEnumerable<InstalledPackageReference> topLevelPackages, IList<LockFileTargetLibrary> libraries, string destination)
        {
            var dependencyGraph = new List<List<DependencyNode>>();

            foreach (var package in topLevelPackages)
            {
                List<DependencyNode> path = new List<DependencyNode>
                {
                    new DependencyNode()
                    {
                        Id = package.Name,
                        Version = package.OriginalRequestedVersion
                    }
                };

                List<List<DependencyNode>> dependencyPathsInFramework = OLDDfsTraversal(package.Name, libraries, visited: new HashSet<DependencyNode>(), path, listOfPaths: new List<List<DependencyNode>>(), destination);
                dependencyGraph.AddRange(dependencyPathsInFramework);
            }
            return dependencyGraph;
        }

        /// </summary>
        /// Returns a list of all top-level packages that have a transitive dependency on the given package
        /// <param name="topLevelPackages">All top-level packages for a given framework.</param>
        /// <param name="libraries">All package libraries for a given framework.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns></returns>
        private List<DependencyNode> GetDependencyGraphsPerFramework(
            IEnumerable<InstalledPackageReference> topLevelPackages,
            IList<LockFileTargetLibrary> packageLibraries,
            FrameworkPackages frameworkPackages,
            string targetPackage)
        {
            List<DependencyNode> dependencyGraph = null;
            var visitedIdToVersion = new Dictionary<string, string>();

            foreach (var topLevelPackage in topLevelPackages)
            {
                DependencyNode dependencyNode = FindDependencyPath(topLevelPackage.Name, packageLibraries, frameworkPackages, visitedIdToVersion, targetPackage);

                if (dependencyNode != null)
                {
                    dependencyGraph ??= new List<DependencyNode>();
                    dependencyGraph.Add(dependencyNode);
                }
            }

            return dependencyGraph;
        }

        /// <summary>
        /// DFS from root node until destination is found (if destination ID exists)
        /// </summary>
        /// <param name="rootPackage">Top level package.</param>
        /// <param name="libraries">All libraries in the target framework.</param>
        /// <param name="visited">A set to keep track of all nodes that have been visited.</param>
        /// <param name="path">Keep track of path as DFS happens.</param>
        /// <param name="listOfPaths">List of all dependency paths that lead to destination.</param>
        /// <param name="destination">CLI argument with the package that is passed in.</param>
        /// <returns></returns>
        private List<List<DependencyNode>> OLDDfsTraversal(string rootPackage, IList<LockFileTargetLibrary> libraries, HashSet<DependencyNode> visited, List<DependencyNode> path, List<List<DependencyNode>> listOfPaths, string destination)
        {
            if (rootPackage == destination)
            {
                // copy what is stored in list variable over to list that you allocate memory for
                List<DependencyNode> pathToAdd = new List<DependencyNode>();
                foreach (var p in path)
                {
                    pathToAdd.Add(p);
                }
                listOfPaths.Add(pathToAdd);
                return listOfPaths;
            }

            // Find the library that matches the root package's ID and get all its dependencies
            LockFileTargetLibrary library = libraries?.FirstOrDefault(i => i.Name == rootPackage);
            var listDependencies = library?.Dependencies;

            if (listDependencies?.Count != 0)
            {
                foreach (var dependency in listDependencies)
                {
                    var dep = new DependencyNode()
                    {
                        Id = dependency.Id,
                        Version = dependency.VersionRange.MinVersion.Version.ToString()
                    };

                    if (!visited.Contains(dep))
                    {
                        visited.Add(dep);
                        path.Add(dep);

                        // recurse
                        OLDDfsTraversal(dependency.Id, libraries, visited, path, listOfPaths, destination);

                        // backtrack
                        path.RemoveAt(path.Count - 1);
                        visited.Remove(dep);
                    }
                }
            }

            return listOfPaths;
        }

        /// <summary>
        /// DFS from root node until destination is found (if destination ID exists)
        /// </summary>
        /// <param name="currentPackage">Top level package.</param>
        /// <param name="libraries">All libraries in the target framework.</param>
        /// <param name="visitedIdToVersion">A set to keep track of all nodes that have been visitedIdToVersion.</param>
        /// <param name="destination">CLI argument with the package that is passed in.</param>
        /// <returns></returns>
        private DependencyNode FindDependencyPath(
            string currentPackage,
            IList<LockFileTargetLibrary> libraries,
            FrameworkPackages frameworkPackages,
            Dictionary<string, string> visitedIdToVersion,
            string targetPackage)
        {
            // If we find the target node, return the current node without any children
            if (currentPackage == targetPackage)
            {
                if (!visitedIdToVersion.ContainsKey(currentPackage))
                {
                    visitedIdToVersion.Add(currentPackage, GetResolvedVersion(currentPackage, frameworkPackages));
                }

                var currentNode = new DependencyNode
                {
                    Id = currentPackage,
                    Version = visitedIdToVersion[currentPackage]
                };

                return currentNode;
            }

            // If we have already traversed this node's children and found paths, we don't want to traverse it again
            if (visitedIdToVersion.ContainsKey(currentPackage))
            {
                var currentNode = new DependencyNode
                {
                    Id = currentPackage,
                    Version = visitedIdToVersion[currentPackage],
                    IsDuplicate = true
                };

                return currentNode;
            }

            // Find the library that matches the root package's ID, and get all its dependencies
            var dependencies = libraries?.FirstOrDefault(i => i.Name == currentPackage)?.Dependencies;

            if (dependencies?.Count != 0)
            {
                List<DependencyNode> paths = null;

                foreach (var dependency in dependencies)
                {
                    var dependencyNode = FindDependencyPath(dependency.Id, libraries, frameworkPackages, visitedIdToVersion, targetPackage);

                    // If the dependency has a path to the target, add it to the list of paths
                    if (dependencyNode != null)
                    {
                        paths ??= new List<DependencyNode>();
                        paths.Add(dependencyNode);
                    }
                }

                // If there are any paths leading to the target, return the current node with its children
                if (paths?.Count > 0)
                {
                    if (!visitedIdToVersion.ContainsKey(currentPackage))
                    {
                        visitedIdToVersion.Add(currentPackage, GetResolvedVersion(currentPackage, frameworkPackages));
                    }

                    var currentNode = new DependencyNode
                    {
                        Id = currentPackage,
                        Version = visitedIdToVersion[currentPackage],
                        Children = paths
                    };
                    return currentNode;
                }
            }

            // If we found no paths leading to the target, return null
            return null;
        }

        private void AlternativePrintDependencyPaths(List<List<DependencyNode>> listOfPaths)
        {
            if (listOfPaths.Count == 0)
            {
                Console.Write("\t\tNo dependency paths found.\n");
            }

            Console.OutputEncoding = System.Text.Encoding.UTF8; // Set UTF-8 encoding

            foreach (var path in listOfPaths)
            {
                int iteration = 0;

                foreach (var package in path)
                {
                    if (iteration == 0)
                    {
                        Console.Write($"{new string(' ', 10)}{LastChildNodeSymbol} ");
                    }
                    else
                    {
                        string indentation = new string(' ', iteration * 5);

                        Console.Write($"\t  {indentation}{LastChildNodeSymbol} ");
                    }

                    Console.Write($"{package.Id} ({package.Version})\n");

                    iteration++;
                }

                Console.Write("\n");
            }
        }

        private string GetResolvedVersion(string packageId, FrameworkPackages frameworkPackages)
        {
            var packageReference = frameworkPackages.TopLevelPackages.FirstOrDefault(i => i.Name == packageId)
                                        ?? frameworkPackages.TransitivePackages.FirstOrDefault(i => i.Name == packageId);

            return packageReference.ResolvedPackageMetadata.Identity.Version.ToNormalizedString();
        }


        class DependencyNode
        {
            public string Id { get; set; }
            public string Version { get; set; }
            // When a particular Node is a duplicate, we don't want to print its tree again,
            // so we will print something else like a "(*)" instead
            // See https://doc.rust-lang.org/cargo/commands/cargo-tree.html
            // TODO: Are we doing this?
            public bool IsDuplicate { get; set; }
            public IList<DependencyNode> Children { get; set; }

            public override int GetHashCode()
            {
                var hashCodeCombiner = new HashCodeCombiner();
                hashCodeCombiner.AddObject(Id);
                hashCodeCombiner.AddSequence(Children);
                return hashCodeCombiner.CombinedHash;
            }
        }

        private void TestMethod()
        {
            var A = new DependencyNode { Id = "A", Children = new List<DependencyNode>() };
            var B = new DependencyNode { Id = "B", Children = new List<DependencyNode>() };
            var C = new DependencyNode { Id = "C", Children = new List<DependencyNode>() };
            var D = new DependencyNode { Id = "D", Children = new List<DependencyNode>() };
            var E = new DependencyNode { Id = "E", Children = new List<DependencyNode>() };
            var F = new DependencyNode { Id = "F", Children = new List<DependencyNode>() };
            var G = new DependencyNode { Id = "G", Children = new List<DependencyNode>() };
            var H = new DependencyNode { Id = "H", Children = new List<DependencyNode>() };
            var I = new DependencyNode { Id = "I", Children = new List<DependencyNode>() };
            var J = new DependencyNode { Id = "J", Children = new List<DependencyNode>() };
            var K = new DependencyNode { Id = "L", Children = new List<DependencyNode>() };
            var L = new DependencyNode { Id = "L", Children = new List<DependencyNode>() };

            /*
            A.Children.Add(B);
            B.Children.Add(C);

            A.Children.Add(D);

            E.Children.Add(F);

            var list = new List<DependencyNode>();
            list.Add(A);
            list.Add(E);
            */

            A.Children.Add(B);
            B.Children.Add(C);
            B.Children.Add(G);
            G.Children.Add(H);

            A.Children.Add(D);

            E.Children.Add(F);

            var list = new List<DependencyNode>();
            list.Add(A);
            list.Add(E);



            Console.Write("\n\n");
            PrintDependencyGraphsPerFramework(list);
            Console.Write("\n\n");
        }

        private void PrintDependencyGraphsPerFramework(List<DependencyNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (i == nodes.Count - 1)
                {
                    PrintDependencyNode(node, "", true);
                }
                else
                {
                    PrintDependencyNode(node, "", false);
                }
            }
        }

        private void PrintDependencyNode(DependencyNode node, string prefix, bool isLastChild)
        {
            string currentPrefix, nextPrefix;
            if (isLastChild)
            {
                currentPrefix = prefix + LastChildNodeSymbol;
                nextPrefix = prefix + LastChildPrefixSymbol;
            }
            else
            {
                currentPrefix = prefix + ChildNodeSymbol;
                nextPrefix = prefix + ChildPrefixSymbol;
            }

            // print current node
            Console.WriteLine($"{currentPrefix}{node.Id} v{node.Version}");

            // if it is a duplicate, we do not print its tree again
            if (node.IsDuplicate)
            {
                Console.WriteLine($"{nextPrefix}{DuplicateTreeSymbol}");
                return;
            }

            if (node.Children?.Count > 0)
            {
                // recurse on the node's children
                for (int i = 0; i < node.Children.Count; i++)
                {
                    PrintDependencyNode(node.Children[i], nextPrefix, i == node.Children.Count - 1);
                }
            }
        }
    }
}
