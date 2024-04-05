// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
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
                                FindAllDependencyGraphs(packages, assetsFile.Targets, package, projectName);
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
        /// <param name="package">The package we want the dependency paths for.</param>
        /// <param name="projectName">The name of the current project.</param>
        private void FindAllDependencyGraphs(IEnumerable<FrameworkPackages> packages, IList<LockFileTarget> targetFrameworks, string package, string projectName)
        {
            var dependencyGraphPerFramework = new Dictionary<string, List<DependencyNode>>(targetFrameworks.Count);
            bool foundPathsToPackage = false;

            foreach (var frameworkPackages in packages)
            {
                LockFileTarget target = targetFrameworks.FirstOrDefault(i => i.TargetFramework.GetShortFolderName() == frameworkPackages.Framework);

                if (target != null)
                {
                    // get all the top level packages for the framework
                    IEnumerable<InstalledPackageReference> frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;

                    // get all package libraries for the framework
                    IList<LockFileTargetLibrary> libraries = target.Libraries;

                    List<DependencyNode> dependencyGraph = GetDependencyGraphPerFramework(frameworkTopLevelPackages, libraries, frameworkPackages, package);

                    if (dependencyGraph != null)
                    {
                        foundPathsToPackage = true;
                    }

                    dependencyGraphPerFramework.Add(frameworkPackages.Framework, dependencyGraph);
                }
            }

            if (!foundPathsToPackage)
            {
                Console.WriteLine($"Project '{projectName}' does not have any dependency graph(s) for '{package}'");
            }
            else
            {
                Console.WriteLine($"Project '{projectName}' has the following dependency graph(s) for '{package}':\n");
                PrintAllDependencyGraphs(dependencyGraphPerFramework);
            }
        }

        /// </summary>
        /// Returns a list of all top-level packages that have a dependency on the given package
        /// <param name="topLevelPackages">All top-level packages for a given framework.</param>
        /// <param name="libraries">All package libraries for a given framework.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns></returns>
        private List<DependencyNode> GetDependencyGraphPerFramework(
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
        /// Recursive method that traverses the current node looking for a path to the target node.
        /// Returns null if no path was found.
        /// </summary>
        /// <param name="currentPackage">Current 'root' package.</param>
        /// <param name="packageLibraries">All libraries in the target framework.</param>
        /// <param name="frameworkPackages">All resolved package references, used to get packages' resolved versions.</param>
        /// <param name="visitedIdToVersion">A dictionary mapping all visited packageIds to their resolved versions.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns></returns>
        private DependencyNode FindDependencyPath(
            string currentPackage,
            IList<LockFileTargetLibrary> packageLibraries,
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
            var dependencies = packageLibraries?.FirstOrDefault(i => i.Name == currentPackage)?.Dependencies;

            if (dependencies?.Count != 0)
            {
                List<DependencyNode> paths = null;

                foreach (var dependency in dependencies)
                {
                    var dependencyNode = FindDependencyPath(dependency.Id, packageLibraries, frameworkPackages, visitedIdToVersion, targetPackage);

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

        private string GetResolvedVersion(string packageId, FrameworkPackages frameworkPackages)
        {
            var packageReference = frameworkPackages.TopLevelPackages.FirstOrDefault(i => i.Name == packageId)
                                        ?? frameworkPackages.TransitivePackages.FirstOrDefault(i => i.Name == packageId);

            return packageReference.ResolvedPackageMetadata.Identity.Version.ToNormalizedString();
        }

        private void PrintAllDependencyGraphs(Dictionary<string, List<DependencyNode>> dependencyGraphPerFramework)
        {
            // If different frameworks have the same dependency graphs, we want to deduplicate them
            var printed = new HashSet<string>(dependencyGraphPerFramework.Count);

            var deduplicatedFrameworks = GetDeduplicatedFrameworks(dependencyGraphPerFramework);

            foreach (var frameworks in deduplicatedFrameworks)
            {
                PrintDependencyGraphPerFramework(frameworks, dependencyGraphPerFramework[frameworks.FirstOrDefault()]);
            }
        }

        private List<List<string>> GetDeduplicatedFrameworks(Dictionary<string, List<DependencyNode>> dependencyGraphPerFramework)
        {
            List<string> frameworksWithoutGraphs = null;
            var dependencyGraphHashes = new Dictionary<int, List<string>>(dependencyGraphPerFramework.Count);

            var dependencyGraphsToFrameworks = new Dictionary<List<DependencyNode>, List<string>>(dependencyGraphPerFramework.Count);

            foreach (var framework in dependencyGraphPerFramework.Keys)
            {
                if (dependencyGraphPerFramework[framework] == null)
                {
                    frameworksWithoutGraphs ??= new List<string>();
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
                    dependencyGraphHashes.Add(hash, new List<string> { framework });
                }

                List<DependencyNode> currentGraph = dependencyGraphPerFramework[framework];
                if (dependencyGraphsToFrameworks.TryGetValue(currentGraph, out var frameworkList))
                {
                    frameworkList.Add(framework);
                }
                else
                {
                    dependencyGraphsToFrameworks.Add(currentGraph, new List<string> { framework });
                }
            }

            var deduplicatedFrameworks = dependencyGraphHashes.Values.ToList();

            if (frameworksWithoutGraphs != null)
            {
                deduplicatedFrameworks.Add(frameworksWithoutGraphs);
            }

            return deduplicatedFrameworks;
        }

        private void PrintDependencyGraphPerFramework(List<string> frameworks, List<DependencyNode> nodes)
        {
            foreach (var framework in frameworks)
            {
                Console.WriteLine($"\t[{framework}]");
            }

            Console.WriteLine($"\t {ChildPrefixSymbol}");

            if (nodes == null || nodes.Count == 0)
            {
                Console.WriteLine($"\t {LastChildNodeSymbol}No dependency graphs found\n");
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (i == nodes.Count - 1)
                {
                    PrintDependencyNode(node, "\t ", true);
                }
                else
                {
                    PrintDependencyNode(node, "\t ", false);
                }
            }
            Console.WriteLine();
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
            // When a particular Node is a duplicate, we don't want to print its tree again,
            // so we will print something else like a "(*)" instead
            // See https://doc.rust-lang.org/cargo/commands/cargo-tree.html
            public bool IsDuplicate { get; set; }
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

            public bool Equals(DependencyNode other)
            {
                if (other is null) return false;

                return Id == other.Id &&
                    Version == other.Version &&
                    IsDuplicate == other.IsDuplicate &&
                    Children.SequenceEqualWithNullCheck(other.Children);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as DependencyNode);
            }

            public static bool operator ==(DependencyNode x, DependencyNode y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;

                return x.Equals(y);
            }

            public static bool operator !=(DependencyNode x, DependencyNode y)
            {
                return !(x == y);
            }
        }
    }
}
