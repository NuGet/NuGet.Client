// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Shared;

namespace NuGet.CommandLine.XPlat
{
    internal static class WhyCommandRunner
    {
        private const string ProjectName = "MSBuildProjectName";
        private const string ProjectAssetsFile = "ProjectAssetsFile";

        /// <summary>
        /// Executes the 'why' command.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments for the 'why' command.</param>
        public static int ExecuteCommand(WhyCommandArgs whyCommandArgs)
        {
            try
            {
                ValidatePathArgument(whyCommandArgs.Path);
                ValidatePackageArgument(whyCommandArgs.Package);
                ValidateFrameworksOption(whyCommandArgs.Frameworks);
            }
            catch (ArgumentException ex)
            {
                whyCommandArgs.Logger.LogError(ex.Message);
                return ExitCodes.InvalidArguments;
            }

            var msBuild = new MSBuildAPIUtility(whyCommandArgs.Logger);

            string targetPackage = whyCommandArgs.Package;

            IEnumerable<string> projectPaths = Path.GetExtension(whyCommandArgs.Path).Equals(".sln")
                                                    ? MSBuildAPIUtility.GetProjectsFromSolution(whyCommandArgs.Path).Where(f => File.Exists(f))
                                                    : [whyCommandArgs.Path];

            foreach (var projectPath in projectPaths)
            {
                Project project = MSBuildAPIUtility.GetProject(projectPath);
                LockFile assetsFile = GetProjectAssetsFile(project, whyCommandArgs.Logger);

                if (assetsFile != null)
                {
                    FindAllDependencyGraphs(whyCommandArgs, msBuild, project, assetsFile);
                }

                ProjectCollection.GlobalProjectCollection.UnloadProject(project);
            }

            return ExitCodes.Success;
        }

        private static void ValidatePathArgument(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    "PROJECT|SOLUTION"));
            }

            if (!File.Exists(path)
                || (!path.EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_PathIsMissingOrInvalid,
                    path));
            }
        }

        private static void ValidatePackageArgument(string package)
        {
            if (string.IsNullOrEmpty(package))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    "PACKAGE"));
            }
        }

        private static void ValidateFrameworksOption(List<string> frameworks)
        {
            var parsedFrameworks = frameworks.Select(f =>
                                    NuGetFramework.Parse(
                                        f.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => s.Trim())
                                            .ToArray()[0]));

            if (parsedFrameworks.Any(f => f.Framework.Equals("Unsupported", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_InvalidFramework));
            }
        }

        /// <summary>
        /// Validates and returns the assets file for the given project.
        /// </summary>
        /// <param name="project">Evaluated MSBuild project</param>
        /// <param name="logger">Logger for the 'why' command</param>
        /// <returns>Assets file for given project</returns>
        private static LockFile GetProjectAssetsFile(Project project, ILoggerWithColor logger)
        {
            if (!MSBuildAPIUtility.IsPackageReferenceProject(project))
            {
                logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_NotPRProject,
                        project.FullPath));

                return null;
            }

            string assetsPath = project.GetPropertyValue(ProjectAssetsFile);

            if (!File.Exists(assetsPath))
            {
                logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_AssetsFileNotFound,
                        project.FullPath));

                return null;
            }

            var lockFileFormat = new LockFileFormat();
            LockFile assetsFile = lockFileFormat.Read(assetsPath);

            // assets file validation
            if (assetsFile.PackageSpec == null
                || assetsFile.Targets == null
                || assetsFile.Targets.Count == 0)
            {
                logger.LogError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.WhyCommand_Error_CannotReadAssetsFile,
                        assetsPath));

                return null;
            }

            return assetsFile;
        }

        /// <summary>
        /// Finds dependency graphs for a given project, and prints output to the console.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments for the 'why' command</param>
        /// <param name="msBuild">MSBuild utility</param>
        /// <param name="project">Current project</param>
        /// <param name="assetsFile">Assets file for current project</param>
        private static void FindAllDependencyGraphs(
            WhyCommandArgs whyCommandArgs,
            MSBuildAPIUtility msBuild,
            Project project,
            LockFile assetsFile)
        {
            // get all resolved package references for a project
            List<FrameworkPackages> frameworkPackages = msBuild.GetResolvedVersions(project, whyCommandArgs.Frameworks, assetsFile, transitive: true, includeProjectReferences: true);

            if (frameworkPackages?.Count > 0)
            {
                string targetPackage = whyCommandArgs.Package;
                bool doesProjectHaveDependencyOnPackage = false;
                var dependencyGraphPerFramework = new Dictionary<string, List<DependencyNode>>(assetsFile.Targets.Count);

                foreach (var frameworkPackage in frameworkPackages)
                {
                    LockFileTarget target = assetsFile.GetTarget(frameworkPackage.Framework, runtimeIdentifier: null);

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
                    whyCommandArgs.Logger.LogMinimal(
                        string.Format(
                            Strings.WhyCommand_Message_NoDependencyGraphsFoundInProject,
                            project.GetPropertyValue(ProjectName),
                            targetPackage));
                }
                else
                {
                    whyCommandArgs.Logger.LogMinimal(
                        string.Format(
                            Strings.WhyCommand_Message_DependencyGraphsFoundInProject,
                            project.GetPropertyValue(ProjectName),
                            targetPackage));

                    WhyCommandPrintUtility.PrintAllDependencyGraphs(dependencyGraphPerFramework, targetPackage, whyCommandArgs.Logger);
                }
            }
            else
            {
                whyCommandArgs.Logger.LogMinimal(
                        string.Format(
                        Strings.WhyCommand_Message_NoPackagesFoundForGivenFrameworks,
                        project.GetPropertyValue(ProjectName)));
            }
        }

        /// <summary>
        /// Finds all dependency paths from the top-level packages to the target package for a given framework.
        /// </summary>
        /// <param name="topLevelPackages">All top-level packages for the framework.</param>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns>List of all top-level package nodes in the dependency graph.</returns>
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
        /// </summary>
        /// <param name="topLevelPackage">Top-level package.</param>
        /// <param name="packageLibraries">All package libraries for a given framework.</param>
        /// <param name="visited">HashSet tracking every package node that we've traversed.</param>
        /// <param name="dependencyNodes">Dictionary tracking all packageIds that were added to the graph, mapped to their DependencyNode objects.</param>
        /// <param name="versions">Dictionary mapping packageIds to their resolved versions.</param>
        /// <param name="targetPackage">The package we want the dependency paths for.</param>
        /// <returns>The top-level package node in the dependency graph (if a path was found), or null (if no path was found)</returns>
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
    }

    /// <summary>
    /// Represents a node in the package dependency graph.
    /// </summary>
    internal class DependencyNode
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
}
