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

        // Dependency graph console output symbols
        private const string ChildNodeSymbol = "├─── ";
        private const string LastChildNodeSymbol = "└─── ";

        private const string ChildPrefixSymbol = "│    ";
        private const string LastChildPrefixSymbol = "     ";

        private const string DuplicateTreeSymbol = "└─── (*)";

        /// <summary>
        /// Execute 'why' command.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments for the 'why' command.</param>
        /// <returns></returns>
        public Task ExecuteCommand(WhyCommandArgs whyCommandArgs)
        {
            var projectPaths = Path.GetExtension(whyCommandArgs.Path).Equals(".sln")
                                    ? MSBuildAPIUtility.GetProjectsFromSolution(whyCommandArgs.Path).Where(f => File.Exists(f))
                                    : new List<string>() { whyCommandArgs.Path };

            string targetPackage = whyCommandArgs.Package;

            var msBuild = new MSBuildAPIUtility(whyCommandArgs.Logger);

            foreach (var projectPath in projectPaths)
            {
                Project project = MSBuildAPIUtility.GetProject(projectPath);

                // if the current project is not a PackageReference project, print an error message and continue to the next project
                if (!MSBuildAPIUtility.IsPackageReferenceProject(project))
                {
                    Console.Error.WriteLine(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Error_NotPRProject,
                            projectPath));

                    ProjectCollection.GlobalProjectCollection.UnloadProject(project);
                    continue;
                }

                string projectName = project.GetPropertyValue(ProjectName);
                string assetsPath = project.GetPropertyValue(ProjectAssetsFile);

                // if the assets file was not found, print an error message and continue to the next project
                if (!File.Exists(assetsPath))
                {
                    Console.Error.WriteLine(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Error_AssetsFileNotFound,
                            projectPath));

                    ProjectCollection.GlobalProjectCollection.UnloadProject(project);
                    continue;
                }

                var lockFileFormat = new LockFileFormat();
                LockFile assetsFile = lockFileFormat.Read(assetsPath);

                // assets file validation
                if (assetsFile.PackageSpec == null
                    || assetsFile.Targets == null
                    || assetsFile.Targets.Count == 0)
                {
                    Console.Error.WriteLine(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.WhyCommand_Error_CannotReadAssetsFile,
                            assetsPath));

                    ProjectCollection.GlobalProjectCollection.UnloadProject(project);
                    continue;
                }

                // get all resolved package references for a project
                List<FrameworkPackages> packages = msBuild.GetResolvedVersions(project, whyCommandArgs.Frameworks, assetsFile, transitive: true);

                if (packages?.Count > 0)
                {
                    FindAllDependencyGraphs(packages, assetsFile.Targets, targetPackage, projectName);
                }
                else
                {
                    Console.WriteLine(
                        string.Format(
                            Strings.WhyCommand_Message_NoPackagesFoundForFramework,
                            projectName));
                }

                // unload project
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Run the 'why' command, and print out output to console.
        /// </summary>
        /// <param name="packages">All packages in the project, split up by top-level packages and transitive packages.</param>
        /// <param name="targetFrameworks">All target frameworks in the project.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <param name="projectName">The name of the current project.</param>
        private void FindAllDependencyGraphs(
            IEnumerable<FrameworkPackages> packages,
            IList<LockFileTarget> targetFrameworks,
            string targetPackage,
            string projectName)
        {
            var dependencyGraphPerFramework = new Dictionary<string, List<DependencyNode>>(targetFrameworks.Count);
            bool doesProjectHaveDependencyOnPackage = false;

            foreach (var frameworkPackages in packages)
            {
                LockFileTarget target = targetFrameworks.FirstOrDefault(i => i.TargetFramework.GetShortFolderName() == frameworkPackages.Framework);

                if (target != null)
                {
                    // get all the top-level packages for the framework
                    IEnumerable<InstalledPackageReference> frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;

                    // get all package libraries for the framework
                    IList<LockFileTargetLibrary> libraries = target.Libraries;

                    List<DependencyNode> dependencyGraph = GetDependencyGraphPerFramework(frameworkTopLevelPackages, libraries, frameworkPackages, targetPackage);

                    if (dependencyGraph != null)
                    {
                        doesProjectHaveDependencyOnPackage = true;
                    }

                    dependencyGraphPerFramework.Add(frameworkPackages.Framework, dependencyGraph);
                }
            }

            if (!doesProjectHaveDependencyOnPackage)
            {
                Console.WriteLine(
                    string.Format(
                        Strings.WhyCommand_Message_NoDependencyGraphsFoundInProject,
                        projectName,
                        targetPackage));
            }
            else
            {
                Console.WriteLine(
                    string.Format(
                        Strings.WhyCommand_Message_DependencyGraphsFoundInProject,
                        projectName,
                        targetPackage));

                PrintAllDependencyGraphs(dependencyGraphPerFramework);
            }
        }

        /// <summary>
        /// Returns a list of all top-level packages that have a dependency on the target package
        /// </summary>
        /// <param name="topLevelPackages">All top-level packages for a given framework.</param>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        /// <param name="frameworkPackages">All resolved package references for a given framework. Used to get a dependency's resolved version for the graph.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns></returns>
        private List<DependencyNode> GetDependencyGraphPerFramework(
            IEnumerable<InstalledPackageReference> topLevelPackages,
            IList<LockFileTargetLibrary> packageLibraries,
            FrameworkPackages frameworkPackages,
            string targetPackage)
        {
            List<DependencyNode> dependencyGraph = null;

            // hashset tracking every package node that we've traversed
            var visited = new HashSet<string>();
            // dictionary tracking all package nodes that have been added to the graph, mapped to their resolved versions
            var addedToGraph = new Dictionary<string, string>();

            foreach (var topLevelPackage in topLevelPackages)
            {
                // use depth-first search to find dependency paths to the target package
                DependencyNode dependencyNode = FindDependencyPath(topLevelPackage.Name, packageLibraries, frameworkPackages, visited, addedToGraph, targetPackage);

                if (dependencyNode != null)
                {
                    dependencyGraph ??= [];
                    dependencyGraph.Add(dependencyNode);
                }
            }

            return dependencyGraph;
        }

        /// <summary>
        /// Recursive method that traverses the current node looking for a path to the target node.
        /// Returns null if no path was found.
        /// </summary>
        /// <param name="currentPackage">Current 'root' package.</param>
        /// <param name="packageLibraries">All libraries in the target framework.</param>
        /// <param name="frameworkPackages">All resolved package references for a given framework, used to get packages' resolved versions.</param>
        /// <param name="visited">HashSet tracking every package node that we've traversed.</param>
        /// <param name="addedToGraph">Dictionary tracking all packageIds that were added to the graph, mapped to their resolved versions.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns></returns>
        private DependencyNode FindDependencyPath(
            string currentPackage,
            IList<LockFileTargetLibrary> packageLibraries,
            FrameworkPackages frameworkPackages,
            HashSet<string> visited,
            Dictionary<string, string> addedToGraph,
            string targetPackage)
        {
            // if we reach the target node, return the current node without any children
            if (currentPackage == targetPackage)
            {
                if (!addedToGraph.ContainsKey(currentPackage))
                {
                    addedToGraph.Add(currentPackage, GetResolvedVersion(currentPackage, frameworkPackages));
                }

                var currentNode = new DependencyNode
                {
                    Id = currentPackage,
                    Version = addedToGraph[currentPackage]
                };

                return currentNode;
            }

            // if we have already traversed this node's children and found dependency paths, mark it as a duplicate node and return
            if (addedToGraph.ContainsKey(currentPackage))
            {
                var currentNode = new DependencyNode
                {
                    Id = currentPackage,
                    Version = addedToGraph[currentPackage],
                    IsDuplicate = true
                };

                return currentNode;
            }

            // if we have already traversed this node's children and found no dependency paths, return null
            if (visited.Contains(currentPackage))
            {
                return null;
            }
            else
            {
                visited.Add(currentPackage);
            }

            // find the library that matches the root package's ID, and get all its dependencies
            var dependencies = packageLibraries?.FirstOrDefault(i => i.Name == currentPackage)?.Dependencies;

            if (dependencies?.Count != 0)
            {
                List<DependencyNode> paths = null;

                // recurse on the package's dependencies
                foreach (var dependency in dependencies)
                {
                    var dependencyNode = FindDependencyPath(dependency.Id, packageLibraries, frameworkPackages, visited, addedToGraph, targetPackage);

                    // if the dependency has a path to the target, add it to the list of paths
                    if (dependencyNode != null)
                    {
                        paths ??= [];
                        paths.Add(dependencyNode);
                    }
                }

                // if there are any paths leading to the target, return the current node with its children
                if (paths?.Count > 0)
                {
                    if (!addedToGraph.ContainsKey(currentPackage))
                    {
                        addedToGraph.Add(currentPackage, GetResolvedVersion(currentPackage, frameworkPackages));
                    }

                    var currentNode = new DependencyNode
                    {
                        Id = currentPackage,
                        Version = addedToGraph[currentPackage],
                        Children = paths
                    };

                    return currentNode;
                }
            }

            // if we found no paths leading to the target, return null
            return null;
        }

        /// <summary>
        /// Gets the resolved version of a given packageId in the current target framework's graph.
        /// </summary>
        /// <param name="packageId">The package we want the version for.</param>
        /// <param name="frameworkPackages">All resolved package references for a given framework.</param>
        /// <returns></returns>
        private string GetResolvedVersion(string packageId, FrameworkPackages frameworkPackages)
        {
            var packageReference = frameworkPackages.TopLevelPackages.FirstOrDefault(i => i.Name == packageId)
                                        ?? frameworkPackages.TransitivePackages.FirstOrDefault(i => i.Name == packageId);

            return packageReference.ResolvedPackageMetadata.Identity.Version.ToNormalizedString();
        }

        /// <summary>
        /// Prints the dependency graphs for all target frameworks.
        /// </summary>
        /// <param name="dependencyGraphPerFramework">A dictionary mapping target frameworks to their dependency graphs.</param>
        private void PrintAllDependencyGraphs(Dictionary<string, List<DependencyNode>> dependencyGraphPerFramework)
        {
            Console.WriteLine();

            // deduplicate the dependency graphs
            List<List<string>> deduplicatedFrameworks = GetDeduplicatedFrameworks(dependencyGraphPerFramework);

            foreach (var frameworks in deduplicatedFrameworks)
            {
                PrintDependencyGraphPerFramework(frameworks, dependencyGraphPerFramework[frameworks.FirstOrDefault()]);
            }
        }

        /// <summary>
        /// Prints the dependency graph for a given framework/list of frameworks.
        /// </summary>
        /// <param name="frameworks">The list of frameworks that share this dependency graph.</param>
        /// <param name="topLevelNodes">The top-level package nodes of the dependency graph.</param>
        private void PrintDependencyGraphPerFramework(List<string> frameworks, List<DependencyNode> topLevelNodes)
        {
            foreach (var framework in frameworks)
            {
                Console.WriteLine($"\t[{framework}]");
            }

            Console.WriteLine($"\t {ChildPrefixSymbol}");

            if (topLevelNodes == null || topLevelNodes.Count == 0)
            {
                Console.WriteLine($"\t {LastChildNodeSymbol}No dependency graph(s) found\n");
                return;
            }

            for (int i = 0; i < topLevelNodes.Count; i++)
            {
                var node = topLevelNodes[i];
                if (i == topLevelNodes.Count - 1)
                {
                    PrintDependencyNode(node, prefix: "\t ", isLastChild: true);
                }
                else
                {
                    PrintDependencyNode(node, prefix: "\t ", isLastChild: false);
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Prints a single dependency node on a line.
        /// </summary>
        /// <param name="node">The current package node.</param>
        /// <param name="prefix">The prefix we need to print before the current node.</param>
        /// <param name="isLastChild">Specifies whether the current node is the last child of its parent.</param>
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
            Console.WriteLine($"{currentPrefix}{node.Id} (v{node.Version})");

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

        /// <summary>
        /// Deduplicates dependency graphs, and returns groups of frameworks that share the same graph.
        /// </summary>
        /// <param name="dependencyGraphPerFramework">A dictionary mapping target frameworks to their dependency graphs.</param>
        /// <returns>
        /// eg. { { "net6.0", "netcoreapp3.1" }, { "net472" } }
        /// </returns>
        private List<List<string>> GetDeduplicatedFrameworks(Dictionary<string, List<DependencyNode>> dependencyGraphPerFramework)
        {
            List<string> frameworksWithoutGraphs = null;
            var dependencyGraphHashes = new Dictionary<int, List<string>>(dependencyGraphPerFramework.Count);

            foreach (var framework in dependencyGraphPerFramework.Keys)
            {
                if (dependencyGraphPerFramework[framework] == null)
                {
                    frameworksWithoutGraphs ??= [];
                    frameworksWithoutGraphs.Add(framework);
                    continue;
                }

                int hash = GetDependencyGraphHashCode(dependencyGraphPerFramework[framework]);
                if (dependencyGraphHashes.ContainsKey(hash))
                {
                    dependencyGraphHashes[hash].Add(framework);
                }
                else
                {
                    dependencyGraphHashes.Add(hash, [framework]);
                }
            }

            var deduplicatedFrameworks = dependencyGraphHashes.Values.ToList();

            if (frameworksWithoutGraphs != null)
            {
                deduplicatedFrameworks.Add(frameworksWithoutGraphs);
            }

            return deduplicatedFrameworks;
        }

        /// <summary>
        /// Returns a hash for a given dependency graph. Used to deduplicate dependency graphs for different frameworks.
        /// </summary>
        /// <param name="graph">The dependency graph for a given framework.</param>
        /// <returns></returns>
        private int GetDependencyGraphHashCode(IList<DependencyNode> graph)
        {
            var hashCodeCombiner = new HashCodeCombiner();
            hashCodeCombiner.AddSequence(graph);
            return hashCodeCombiner.CombinedHash;
        }

        class DependencyNode
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public bool IsDuplicate { get; set; } // When a particular Node is a duplicate, we don't want to print its tree again
            public IList<DependencyNode> Children { get; set; }

            public override int GetHashCode()
            {
                var hashCodeCombiner = new HashCodeCombiner();
                hashCodeCombiner.AddObject(Id);
                hashCodeCombiner.AddObject(Version);
                hashCodeCombiner.AddObject(IsDuplicate);
                hashCodeCombiner.AddSequence(Children);
                return hashCodeCombiner.CombinedHash;
            }
        }
    }
}
