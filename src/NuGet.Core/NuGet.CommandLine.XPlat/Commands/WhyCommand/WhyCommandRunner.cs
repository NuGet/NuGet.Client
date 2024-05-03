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
    internal static class WhyCommandRunner
    {
        private const string ProjectName = "MSBuildProjectName";
        private const string ProjectAssetsFile = "ProjectAssetsFile";

        // Dependency graph console output symbols
        private const string ChildNodeSymbol = "├─── ";
        private const string LastChildNodeSymbol = "└─── ";

        private const string ChildPrefixSymbol = "│    ";
        private const string LastChildPrefixSymbol = "     ";

        private const string BoldTextEscapeChars = "\u001b[1m";
        private const string ResetTextEscapeChars = "\u001b[0m";

        /// <summary>
        /// Executes the 'why' command.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments for the 'why' command.</param>
        /// <returns></returns>
        public static Task ExecuteCommand(WhyCommandArgs whyCommandArgs)
        {
            var msBuild = new MSBuildAPIUtility(whyCommandArgs.Logger);

            string targetPackage = whyCommandArgs.Package;

            IEnumerable<string> projectPaths = Path.GetExtension(whyCommandArgs.Path).Equals(".sln")
                                                    ? MSBuildAPIUtility.GetProjectsFromSolution(whyCommandArgs.Path).Where(f => File.Exists(f))
                                                    : new List<string>() { whyCommandArgs.Path };

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
                            Strings.WhyCommand_Message_NoPackagesFoundForGivenFrameworks,
                            projectName));
                }

                // unload project
                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Runs the 'why' command, and prints output to the console.
        /// </summary>
        /// <param name="frameworkPackages">All frameworks in the project, with their top-level packages and transitive packages.</param>
        /// <param name="targets">All lock file targets in the project.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <param name="projectName">The name of the current project.</param>
        private static void FindAllDependencyGraphs(
            IEnumerable<FrameworkPackages> frameworkPackages,
            IList<LockFileTarget> targets,
            string targetPackage,
            string projectName)
        {
            bool doesProjectHaveDependencyOnPackage = false;
            var dependencyGraphPerFramework = new Dictionary<string, List<DependencyNode>>(targets.Count);

            foreach (var frameworkPackage in frameworkPackages)
            {
                LockFileTarget target = targets.FirstOrDefault(f => f.TargetFramework.GetShortFolderName() == frameworkPackage.Framework);

                if (target != default)
                {
                    // get all package libraries for the framework
                    IList<LockFileTargetLibrary> packageLibraries = target.Libraries;

                    // if the project has a dependency on the target package, get the dependency graph
                    if (packageLibraries.Any(l => l.Name == targetPackage))
                    {
                        doesProjectHaveDependencyOnPackage = true;
                        dependencyGraphPerFramework.Add(frameworkPackage.Framework,
                                                        GetDependencyGraphPerFramework(frameworkPackage.TopLevelPackages, packageLibraries, targetPackage));
                    }
                    else
                    {
                        dependencyGraphPerFramework.Add(frameworkPackage.Framework, null);
                    }
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

                PrintAllDependencyGraphs(dependencyGraphPerFramework, targetPackage);
            }
        }

        /// <summary>
        /// Finds all dependency paths from the top-level packages to the target package for a given framework.
        /// Returns a set of all top-level package nodes in the dependency graph.
        /// </summary>
        /// <param name="topLevelPackages">All top-level packages for the framework.</param>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns></returns>
        private static List<DependencyNode> GetDependencyGraphPerFramework(
            IEnumerable<InstalledPackageReference> topLevelPackages,
            IList<LockFileTargetLibrary> packageLibraries,
            string targetPackage)
        {
            List<DependencyNode> dependencyGraph = null;

            // hashset tracking every package node that we've traversed
            var visited = new HashSet<string>();
            // dictionary tracking all package nodes that have been added to the graph, mapped to their DependencyNode objects
            var dependencyNodes = new Dictionary<string, DependencyNode>();
            // dictionary mapping all packageIds to their resolved version
            Dictionary<string, string> versions = GetAllResolvedVersions(packageLibraries);

            foreach (var topLevelPackage in topLevelPackages)
            {
                // use depth-first search to find dependency paths from the top-level package to the target package
                DependencyNode topLevelNode = FindDependencyPath(topLevelPackage.Name, packageLibraries, visited, dependencyNodes, versions, targetPackage);

                if (topLevelNode != null)
                {
                    dependencyGraph ??= [];
                    dependencyGraph.Add(topLevelNode);
                }
            }

            return dependencyGraph;
        }


        /// <summary>
        /// Adds all resolved versions of packages to a dictionary.
        /// </summary>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        /// <returns></returns>
        private static Dictionary<string, string> GetAllResolvedVersions(IList<LockFileTargetLibrary> packageLibraries)
        {
            var versions = new Dictionary<string, string>();

            foreach (var package in packageLibraries)
            {
                versions.Add(package.Name, package.Version.ToNormalizedString());
            }

            return versions;
        }

        /// <summary>
        /// Traverses the dependency graph for a given top-level package, looking for a path to the target package.
        /// Returns the top-level package node if a path was found, and null if no path was found.
        /// </summary>
        /// <param name="topLevelPackage">Top-level package.</param>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        /// <param name="visited">HashSet tracking every package node that we've traversed.</param>
        /// <param name="dependencyNodes">Dictionary tracking all packageIds that were added to the graph, mapped to their DependencyNode objects.</param>
        /// <param name="versions">Dictionary mapping packageIds to their resolved versions.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns></returns>
        private static DependencyNode FindDependencyPath(
            string topLevelPackage,
            IList<LockFileTargetLibrary> packageLibraries,
            HashSet<string> visited,
            Dictionary<string, DependencyNode> dependencyNodes,
            Dictionary<string, string> versions,
            string targetPackage)
        {
            var stack = new Stack<StackDependencyData>();
            stack.Push(new StackDependencyData(topLevelPackage, null));

            while (stack.Count > 0)
            {
                var currentPackageData = stack.Pop();
                var currentPackageId = currentPackageData.Id;

                // if we reach the target node, or if we've already traversed this node and found dependency paths, add it to the graph
                if (currentPackageId == targetPackage
                    || dependencyNodes.ContainsKey(currentPackageId))
                {
                    AddToGraph(currentPackageData, dependencyNodes, versions);
                    continue;
                }

                // if we have already traversed this node's children, continue
                if (visited.Contains(currentPackageId))
                {
                    continue;
                }

                visited.Add(currentPackageId);

                // get all dependencies for the current package
                var dependencies = packageLibraries?.FirstOrDefault(i => i.Name == currentPackageId)?.Dependencies;

                if (dependencies?.Count > 0)
                {
                    // push all the dependencies onto the stack
                    foreach (var dependency in dependencies)
                    {
                        stack.Push(new StackDependencyData(dependency.Id, currentPackageData));
                    }
                }
            }

            if (dependencyNodes.ContainsKey(topLevelPackage))
            {
                return dependencyNodes[topLevelPackage];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds a dependency path to the graph, starting from the target package and traversing up to the top-level package.
        /// </summary>
        /// <param name="targetPackageData">Target node data. This stores parent references, so it can be used to construct the dependency graph
        /// up to the top-level package.</param>
        /// <param name="dependencyNodes">Dictionary tracking all packageIds that were added to the graph, mapped to their DependencyNode objects.</param>
        /// <param name="versions">Dictionary mapping packageIds to their resolved versions.</param>
        private static void AddToGraph(
            StackDependencyData targetPackageData,
            Dictionary<string, DependencyNode> dependencyNodes,
            Dictionary<string, string> versions)
        {
            // first, we traverse the target's parents, listing the packages in the path from the target to the top-level package
            var dependencyPath = new List<string> { targetPackageData.Id };
            StackDependencyData current = targetPackageData.Parent;

            while (current != null)
            {
                dependencyPath.Add(current.Id);
                current = current.Parent;
            }

            // then, we traverse this list from the target package to the top-level package, initializing/updating their dependency nodes as needed
            for (int i = 0; i < dependencyPath.Count; i++)
            {
                string currentPackageId = dependencyPath[i];

                if (!dependencyNodes.ContainsKey(currentPackageId))
                {
                    dependencyNodes.Add(currentPackageId, new DependencyNode(currentPackageId, versions[currentPackageId]));
                }

                if (i > 0)
                {
                    var childNode = dependencyNodes[dependencyPath[i - 1]];

                    if (dependencyNodes[currentPackageId].Children.Any(p => p.Id == childNode.Id))
                    {
                        continue;
                    }

                    dependencyNodes[currentPackageId].Children.Add(childNode);
                }
            }
        }

        /// <summary>
        /// Prints the dependency graphs for all target frameworks.
        /// </summary>
        /// <param name="dependencyGraphPerFramework">A dictionary mapping target frameworks to their dependency graphs.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        private static void PrintAllDependencyGraphs(Dictionary<string, List<DependencyNode>> dependencyGraphPerFramework, string targetPackage)
        {
            Console.WriteLine();

            // deduplicate the dependency graphs
            List<List<string>> deduplicatedFrameworks = GetDeduplicatedFrameworks(dependencyGraphPerFramework);

            foreach (var frameworks in deduplicatedFrameworks)
            {
                PrintDependencyGraphPerFramework(frameworks, dependencyGraphPerFramework[frameworks.FirstOrDefault()], targetPackage);
            }
        }

        /// <summary>
        /// Deduplicates dependency graphs, and returns groups of frameworks that share the same graph.
        /// </summary>
        /// <param name="dependencyGraphPerFramework">A dictionary mapping target frameworks to their dependency graphs.</param>
        /// <returns>
        /// eg. { { "net6.0", "netcoreapp3.1" }, { "net472" } }
        /// </returns>
        private static List<List<string>> GetDeduplicatedFrameworks(Dictionary<string, List<DependencyNode>> dependencyGraphPerFramework)
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
        /// Prints the dependency graph for a given framework/list of frameworks.
        /// </summary>
        /// <param name="frameworks">The list of frameworks that share this dependency graph.</param>
        /// <param name="topLevelNodes">The top-level package nodes of the dependency graph.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        private static void PrintDependencyGraphPerFramework(List<string> frameworks, List<DependencyNode> topLevelNodes, string targetPackage)
        {
            // print framework header
            foreach (var framework in frameworks)
            {
                Console.WriteLine($"\t[{framework}]");
            }

            Console.WriteLine($"\t {ChildPrefixSymbol}");

            if (topLevelNodes == null || topLevelNodes.Count == 0)
            {
                Console.WriteLine($"\t {LastChildNodeSymbol}{Strings.WhyCommand_Message_NoDependencyGraphsFoundForFramework}\n");
                return;
            }

            var stack = new Stack<StackOutputData>();

            // initialize the stack with all top-level nodes
            int counter = 0;
            foreach (var node in topLevelNodes)
            {
                stack.Push(new StackOutputData(node, prefix: "\t ", isLastChild: counter++ == 0));
            }

            // print the dependency graph
            while (stack.Count > 0)
            {
                var current = stack.Pop();

                string currentPrefix, childPrefix;
                if (current.IsLastChild)
                {
                    currentPrefix = current.Prefix + LastChildNodeSymbol;
                    childPrefix = current.Prefix + LastChildPrefixSymbol;
                }
                else
                {
                    currentPrefix = current.Prefix + ChildNodeSymbol;
                    childPrefix = current.Prefix + ChildPrefixSymbol;
                }

                // print current node
                if (current.Node.Id == targetPackage)
                {
                    /*
                    Console.Write($"{currentPrefix}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{current.Node.Id} (v{current.Node.Version})");
                    Console.ResetColor();
                    */

                    Console.WriteLine($"{currentPrefix}{BoldTextEscapeChars}{current.Node.Id} (v{current.Node.Version}){ResetTextEscapeChars}");
                }
                else
                {
                    Console.WriteLine($"{currentPrefix}{current.Node.Id} (v{current.Node.Version})");
                }

                if (current.Node.Children?.Count > 0)
                {
                    // push all the node's children onto the stack
                    counter = 0;
                    foreach (var child in current.Node.Children)
                    {
                        stack.Push(new StackOutputData(child, childPrefix, isLastChild: counter++ == 0));
                    }
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Returns a hash for a given dependency graph. Used to deduplicate dependency graphs for different frameworks.
        /// </summary>
        /// <param name="graph">The dependency graph for a given framework.</param>
        /// <returns></returns>
        private static int GetDependencyGraphHashCode(List<DependencyNode> graph)
        {
            var hashCodeCombiner = new HashCodeCombiner();
            hashCodeCombiner.AddUnorderedSequence(graph);
            return hashCodeCombiner.CombinedHash;
        }

        /// <summary>
        /// Represents a node in the package dependency graph.
        /// </summary>
        private class DependencyNode
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public HashSet<DependencyNode> Children { get; set; }

            public DependencyNode(string id, string version)
            {
                Id = id;
                Version = version;
                Children = new HashSet<DependencyNode>();
            }

            public override int GetHashCode()
            {
                var hashCodeCombiner = new HashCodeCombiner();
                hashCodeCombiner.AddObject(Id);
                hashCodeCombiner.AddObject(Version);
                hashCodeCombiner.AddUnorderedSequence(Children);
                return hashCodeCombiner.CombinedHash;
            }
        }

        private class StackDependencyData
        {
            public string Id { get; set; }
            public StackDependencyData Parent { get; set; }

            public StackDependencyData(string currentId, StackDependencyData parentDependencyData)
            {
                Id = currentId;
                Parent = parentDependencyData;
            }
        }

        private class StackOutputData
        {
            public DependencyNode Node { get; set; }
            public string Prefix { get; set; }
            public bool IsLastChild { get; set; }

            public StackOutputData(DependencyNode node, string prefix, bool isLastChild)
            {
                Node = node;
                Prefix = prefix;
                IsLastChild = isLastChild;
            }
        }
    }
}
