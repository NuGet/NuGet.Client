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
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    internal class WhyCommandRunner : IWhyCommandRunner
    {
        private const string ProjectAssetsFile = "ProjectAssetsFile";
        private const string ProjectName = "MSBuildProjectName";

        private const string LeafSymbol = "└──";

        /// <summary>
        /// Use CLI arguments to execute why command.
        /// </summary>
        /// <param name="whyCommandArgs">CLI arguments.</param>
        /// <returns></returns>
        public Task ExecuteCommandAsync(WhyCommandArgs whyCommandArgs)
        {
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
                                Console.Write($"Project '{projectName}' has the following dependency graph for '{package}'\n");
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
                // Print framework name
                string frameworkName = frameworkPackages.Framework;
                Console.Write($"\n\t[{frameworkName}]:\n\n");

                // Get all the top level packages in the framework
                IEnumerable<InstalledPackageReference> frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;

                // Get all the libraries in the framework
                IList<LockFileTargetLibrary> libraries = targetFrameworks
                                                                .FirstOrDefault(i => i.Name == frameworkPackages.Framework)
                                                                .Libraries;

                List<List<Dependency>> dependencyGraph = GetDependencyGraphPerFramework(frameworkTopLevelPackages, libraries, package);
                //PrintDependencyGraphPerFramework(dependencyGraph);
                AlternativePrintDependencyPaths(dependencyGraph); // TODO: this has duplicates, fix it
            }
        }

        /// <summary>
        /// Find all dependency paths.
        /// </summary>
        /// <param name="topLevelPackages">"root nodes" of the graph.</param>
        /// <param name="libraries">All libraries in a given project. </param>
        /// <param name="destination">The package name CLI argument.</param>
        /// <returns></returns>
        private List<List<Dependency>> GetDependencyGraphPerFramework(IEnumerable<InstalledPackageReference> topLevelPackages, IList<LockFileTargetLibrary> libraries, string destination)
        {
            var dependencyGraph = new List<List<Dependency>>();
            var listOfPaths = new List<List<Dependency>>();
            var visited = new HashSet<Dependency>();

            foreach (var package in topLevelPackages)
            {
                List<Dependency> path = new List<Dependency>
                {
                    new Dependency()
                    {
                        Name = package.Name,
                        Version = package.OriginalRequestedVersion
                    }
                };

                List<List<Dependency>> dependencyPathsInFramework = DfsTraversal(package.Name, libraries, visited, path, listOfPaths, destination);
                dependencyGraph.AddRange(dependencyPathsInFramework);
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
        private List<List<Dependency>> DfsTraversal(string rootPackage, IList<LockFileTargetLibrary> libraries, HashSet<Dependency> visited, List<Dependency> path, List<List<Dependency>> listOfPaths, string destination)
        {
            if (rootPackage == destination)
            {
                // copy what is stored in list variable over to list that you allocate memory for
                List<Dependency> pathToAdd = new List<Dependency>();
                foreach (var p in path)
                {
                    pathToAdd.Add(p);
                }
                listOfPaths.Add(pathToAdd);
                return listOfPaths;
            }

            // Find the library that matches the root package's ID and get all its dependencies
            LockFileTargetLibrary library = libraries.FirstOrDefault(i => i.Name == rootPackage);
            var listDependencies = library.Dependencies;

            if (listDependencies.Count != 0)
            {
                foreach (var dependency in listDependencies)
                {
                    Dependency dep;
                    dep.Name = dependency.Id;
                    dep.Version = dependency.VersionRange.MinVersion.Version.ToString();
                    if (!visited.Contains(dep))
                    {
                        visited.Add(dep);
                        path.Add(dep);

                        // recurse
                        DfsTraversal(dependency.Id, libraries, visited, path, listOfPaths, destination);

                        // backtrack
                        path.RemoveAt(path.Count - 1);
                        visited.Remove(dep);
                    }
                }
            }

            return listOfPaths;
        }

        /// <summary>
        /// Print dependency graph with syntax/punctuation.
        /// </summary>
        /// <param name="listOfPaths">List of all paths that lead to destination.</param>
        private void PrintDependencyGraphPerFramework(List<List<Dependency>> listOfPaths)
        {
            if (listOfPaths.Count == 0)
            {
                Console.Write("\t\tNo dependency paths found.\n");
            }

            foreach (var path in listOfPaths)
            {
                Console.Write("\t\t");
                int iteration = 0;
                foreach (var package in path)
                {
                    Console.Write($"{package.Name} ({package.Version})");
                    // don't print arrows after the last package in the path
                    if (iteration < path.Count - 1)
                    {
                        Console.Write(" -> ");
                    }
                    iteration++;
                }
                Console.Write("\n");
            }
        }

        private void AlternativePrintDependencyPaths(List<List<Dependency>> listOfPaths)
        {
            if (listOfPaths.Count == 0)
            {
                Console.Write("\t\tNo dependency paths found.\n");
            }

            Console.OutputEncoding = System.Text.Encoding.UTF8; // Set UTF-8 encoding

            foreach (var path in listOfPaths)
            {
                int iteration = 0;
                var dependencyPathOutput = new StringBuilder();

                foreach (var package in path)
                {
                    if (iteration == 0)
                    {
                        Console.Write($"{new string(' ', 12)}");

                        dependencyPathOutput.Append($"\t");
                    }
                    else
                    {
                        string indentation = new string(' ', iteration * 4);

                        Console.Write($"\t{indentation}{LeafSymbol} ");
                        dependencyPathOutput.Append($"\t{indentation}{LeafSymbol} ");
                    }

                    Console.Write($"{package.Name} ({package.Version})\n");
                    dependencyPathOutput.Append($"{package.Name} ({package.Version})\n");

                    iteration++;
                }

                Console.Write("\n");
            }
        }

        struct Dependency
        {
            public string Name;
            public string Version;
        }

        private List<List<Dependency>> NonRecursiveDfsTraversal(string rootPackage, IList<LockFileTargetLibrary> libraries, string destination)
        {
            var dependencyPaths = new List<List<Dependency>>();
            var stack = new Stack<(string package, List<Dependency> path)>();
            var visited = new HashSet<Dependency>();

            stack.Push((rootPackage, new List<Dependency>
            {
                new Dependency
                {
                    Name = rootPackage,
                    Version = GetPackageVersion(rootPackage, libraries)
                }
            }));

            while (stack.Count > 0)
            {
                var (currentPackage, currentPath) = stack.Pop();

                if (currentPackage == destination)
                {
                    dependencyPaths.Add(currentPath);
                    continue;
                }

                var library = libraries.FirstOrDefault(lib => lib.Name == currentPackage);
                if (library != null)
                {
                    foreach (var dependency in library.Dependencies)
                    {
                        var dep = new Dependency
                        {
                            Name = dependency.Id,
                            Version = dependency.VersionRange.MinVersion.Version.ToString()
                        };

                        if (!visited.Contains(dep))
                        {
                            visited.Add(dep);
                            var newPath = new List<Dependency>(currentPath) { dep };
                            stack.Push((dependency.Id, newPath));
                        }
                    }
                }
            }

            return dependencyPaths;
        }

        private string GetPackageVersion(string packageName, IList<LockFileTargetLibrary> libraries)
        {
            var library = libraries.FirstOrDefault(lib => lib.Name == packageName);
            return library?.Version.ToString();
        }

    }
}
