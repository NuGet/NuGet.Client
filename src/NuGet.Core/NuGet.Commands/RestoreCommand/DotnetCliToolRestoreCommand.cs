// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.Versioning;

namespace NuGet.Commands
{
    /// <summary>
    /// Restores DotnetCliTool type packages.
    /// </summary>
    internal class DotnetCliToolRestoreCommand
    {
        private readonly ILogger _logger;
        private readonly RestoreRequest _request;

        public DotnetCliToolRestoreCommand(RestoreRequest request)
        {
            _logger = request.Log;
            _request = request;
        }

        public async Task<Tuple<DotnetCliToolFile, List<RestoreTargetGraph>>> TryRestore(
            LibraryRange toolRange,
            HashSet<LibraryIdentity> allInstalledPackages,
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            RemoteToolWalker remoteWalker,
            RemoteWalkContext context,
            CancellationToken token)
        {
            var toolFile = new DotnetCliToolFile();
            var graphs = new List<RestoreTargetGraph>();
            var success = true;

            var toolNode = await remoteWalker.GetNodeAsync(toolRange, token);

            // Create a graph containing only the top level tool package. 
            // Framework is not used here.
            var toolOnlyGraph = RestoreTargetGraph.Create(
                new[] { toolNode },
                context,
                _request.Log,
                NuGetFramework.AgnosticFramework);

            var toolOnlyGraphs = new[] { toolOnlyGraph };

            // Verify that the tool package needed to complete
            // the remaining part of the restore was acquired.
            if (!ResolutionSucceeded(toolOnlyGraphs))
            {
                success = false;
                toolFile.ToolVersion = toolRange.VersionRange?.MinVersion ?? new NuGetVersion(0, 0, 0);
            }
            else
            {
                var toolLibrary = toolNode.Item.Data.Match.Library;
                toolFile.ToolVersion = toolLibrary.Version;

                // Install the tool package to the user folder inorder to read it for the deps files.
                await InstallPackagesAsync(toolOnlyGraphs, allInstalledPackages, token);

                userPackageFolder.ClearCacheForIds(allInstalledPackages.Select(e => e.Name));

                // Deps files is populated by reference
                var depsFiles = new List<KeyValuePair<NuGetFramework, List<string>>>();

                // Read the deps files from the package.
                var nodes = GetNodesByFramework(userPackageFolder, fallbackPackageFolders, toolNode, toolLibrary, depsFiles);

                // Populate dependencies and download packages
                await remoteWalker.WalkAsync(nodes.Values.SelectMany(e => e), token);

                // Create graphs from populated nodes
                graphs.AddRange(nodes.Select(pair =>
                    RestoreTargetGraph.Create(pair.Value, context, _request.Log, pair.Key)));

                // Validate package graphs by checking for missing packages
                success = ResolutionSucceeded(graphs);

                // Install packages to the user packages folder
                // This happens regardless of success to avoid downloading the packages
                // again on the next restore.
                await InstallPackagesAsync(graphs, allInstalledPackages, token);

                userPackageFolder.ClearCacheForIds(allInstalledPackages.Select(e => e.Name));

                // Add deps file paths
                foreach (var pair in depsFiles)
                {
                    toolFile.DepsFiles.Add(pair.Key, pair.Value);
                }
            }

            toolFile.Success = success;
            toolFile.DependencyRange = toolRange.VersionRange;
            toolFile.ToolId = toolRange.Name;

            // Sources in order
            toolFile.PackageFolders.Add(userPackageFolder.RepositoryRoot);

            foreach (var fallback in fallbackPackageFolders)
            {
                toolFile.PackageFolders.Add(fallback.RepositoryRoot);
            }

            // Copy error messages to the tool output log
            var collectorLogger = _request.Log as CollectorLogger;

            if (collectorLogger != null)
            {
                foreach (var error in collectorLogger.Errors)
                {
                    toolFile.Log.Add(new FileLogEntry(FileLogEntryType.Error, error));
                }
            }

            return new Tuple<DotnetCliToolFile, List<RestoreTargetGraph>>(toolFile, graphs);
        }

        private static Dictionary<NuGetFramework, List<GraphNode<RemoteResolveResult>>> GetNodesByFramework(
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            GraphNode<RemoteResolveResult> toolNode,
            LibraryIdentity toolLibrary,
            List<KeyValuePair<NuGetFramework, List<string>>> depsFiles)
        {
            var nodes = new Dictionary<NuGetFramework, List<GraphNode<RemoteResolveResult>>>();

            var localRepositories = new List<NuGetv3LocalRepository>();
            localRepositories.Add(userPackageFolder);
            localRepositories.AddRange(fallbackPackageFolders);

            // Read the package, this is expected to be there.
            // A missing package will error out before this.
            var packageSourceInfo = NuGetv3LocalRepositoryUtility
                .GetPackage(localRepositories, toolLibrary.Name, toolLibrary.Version);

            var packageInfo = packageSourceInfo?.Package;

            if (packageInfo == null)
            {
                // This could only be caused by an internal problem, if it does display a friendly message.
                throw new InvalidOperationException(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.UnableToFindPackageOnDisk,
                    $"{toolLibrary.Name} {toolLibrary.Version.ToNormalizedString()}"));
            }

            var package = packageInfo.GetPackage();

            if (!package.NuspecReader.GetPackageTypes().Contains(PackageType.DotnetCliTool))
            {
                throw new InvalidOperationException(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.MissingPackageDotnetCliToolPackageType,
                    $"{toolLibrary.Name} {toolLibrary.Version.ToNormalizedString()}"));
            }

            var depsFileItems = package.GetLibItems()
                .Select(group => new KeyValuePair<NuGetFramework, List<string>>(
                    group.TargetFramework,
                    GetDepsFilePaths(group, toolLibrary.Name, packageInfo.ExpandedPath).ToList()))
                .Where(pair => pair.Value != null)
                .ToList();

            // Create nodes for each deps file found
            foreach (var pair in depsFileItems)
            {
                if (!nodes.ContainsKey(pair.Key))
                {
                    var nodeList = new List<GraphNode<RemoteResolveResult>>();
                    nodes.Add(pair.Key, nodeList);

                    foreach (var depsPath in pair.Value)
                    {
                        nodeList.Add(CopyNodeAndAddDeps(toolNode, depsPath));
                    }

                    depsFiles.Add(pair);
                }
            }

            return nodes;
        }

        /// <summary>
        /// Verify all packages were found.
        /// </summary>
        private bool ResolutionSucceeded(IEnumerable<RestoreTargetGraph> graphs)
        {
            var success = true;

            foreach (var graph in graphs)
            {
                if (graph.Unresolved.Any())
                {
                    success = false;
                    foreach (var unresolved in graph.Unresolved)
                    {
                        var displayVersionRange = unresolved.VersionRange.ToNonSnapshotRange().PrettyPrint();
                        var packageDisplayName = $"{unresolved.Name} {displayVersionRange}";

                        var message = string.Format(CultureInfo.CurrentCulture,
                            Strings.UnableToResolveToolDependency,
                            packageDisplayName);

                        _logger.LogError(message);
                    }
                }
            }

            return success;
        }

        private static GraphNode<RemoteResolveResult> CopyNodeAndAddDeps(GraphNode<RemoteResolveResult> toolNode, string depsPath)
        {
            var depsContent = LockFileFormat.Load(depsPath);

            // Find all ids listed as dependencies by a library
            // Things outside of this set are part of the package.
            var childIds = new HashSet<string>(depsContent.Targets
                .SelectMany(target => target.Libraries)
                .SelectMany(lib => lib.Dependencies)
                .Select(d => d.Id), StringComparer.OrdinalIgnoreCase);

            // Find all non-top level libraries, this will exclude the tool package itself
            // which may have a name different from the package.
            var dependencies = depsContent.Libraries
                .Where(lib =>
                    childIds.Contains(lib.Name)
                    && IsAllowedType(lib))
                .Select(ToToolDependency)
                .ToList();

            return new GraphNode<RemoteResolveResult>(toolNode.Key)
            {
                Item = new GraphItem<RemoteResolveResult>(toolNode.Item.Key)
                {
                    Data = new RemoteResolveResult()
                    {
                        Match = toolNode.Item.Data.Match,
                        Dependencies = dependencies
                    }
                }
            };
        }

        /// <summary>
        /// Identity -> Dependency with a range allowing a single version.
        /// </summary>
        private static LibraryDependency ToToolDependency(LockFileLibrary library)
        {
            return new LibraryDependency()
            {
                LibraryRange = new LibraryRange(
                            name: library.Name,
                            versionRange: new VersionRange(
                                minVersion: library.Version,
                                includeMinVersion: true,
                                maxVersion: library.Version,
                                includeMaxVersion: true),
                            typeConstraint: LibraryDependencyTarget.Package)
            };
        }

        // CoreHost does not use the type, but to ensure that we do not accidently
        // reference a new type of entry filter down to package and project.
        private static bool IsAllowedType(LockFileLibrary library)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(library.Type, LibraryType.Package)
                      || StringComparer.OrdinalIgnoreCase.Equals(library.Type, LibraryType.Project);
        }

        private static IEnumerable<string> GetDepsFilePaths(FrameworkSpecificGroup group, string packageId, string packageRoot)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(
                FrameworkConstants.FrameworkIdentifiers.NetCoreApp,
                group.TargetFramework.Framework))
            {
                foreach (var path in group.Items)
                {
                    // lib/tfm/id.deps.json
                    // Package readers normalize all paths to use '/'
                    var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 3
                        && parts[2].EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return Path.GetFullPath(Path.Combine(packageRoot, path));
                    }
                }
            }
        }

        private async Task InstallPackagesAsync(IEnumerable<RestoreTargetGraph> graphs,
            HashSet<LibraryIdentity> allInstalledPackages,
            CancellationToken token)
        {
            var packagesToInstall = graphs.SelectMany(g => g.Install.Where(match => allInstalledPackages.Add(match.Library)));

            await RestoreInstallUtility.InstallPackagesAsync(
                _request,
                packagesToInstall,
                allInstalledPackages,
                token);
        }
    }
}