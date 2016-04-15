// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreCommand
    {
        private readonly ILogger _logger;
        private readonly RestoreRequest _request;

        private bool _success = true;

        private readonly Dictionary<NuGetFramework, RuntimeGraph> _runtimeGraphCache = new Dictionary<NuGetFramework, RuntimeGraph>();
        private readonly ConcurrentDictionary<PackageIdentity, RuntimeGraph> _runtimeGraphCacheByPackage
            = new ConcurrentDictionary<PackageIdentity, RuntimeGraph>(PackageIdentity.Comparer);
        private readonly Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> _includeFlagGraphs
            = new Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>>();

        public RestoreCommand(RestoreRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Validate the lock file version requested
            if (request.LockFileVersion < 1 || request.LockFileVersion > LockFileFormat.Version)
            {
                Debug.Fail($"Lock file version {_request.LockFileVersion} is not supported.");
                throw new ArgumentOutOfRangeException(nameof(_request.LockFileVersion));
            }

            _logger = request.Log;
            _request = request;
        }

        public Task<RestoreResult> ExecuteAsync()
        {
            return ExecuteAsync(CancellationToken.None);
        }

        public async Task<RestoreResult> ExecuteAsync(CancellationToken token)
        {
            // Use the shared cache if one was provided, otherwise create a new one.
            var localRepository = _request.DependencyProviders.GlobalPackages;

            var projectLockFilePath = string.IsNullOrEmpty(_request.LockFilePath) ?
                Path.Combine(_request.Project.BaseDirectory, LockFileFormat.LockFileName) :
                _request.LockFilePath;

            bool relockFile = false;
            if (_request.ExistingLockFile != null
                && _request.ExistingLockFile.IsLocked
                && !_request.ExistingLockFile.IsValidForPackageSpec(_request.Project, _request.LockFileVersion))
            {
                // The lock file was locked, but the project.json is out of date
                relockFile = true;
                _request.ExistingLockFile.IsLocked = false;
                _logger.LogMinimal(Strings.Log_LockFileOutOfDate);
            }
            
            var contextForProject = CreateRemoteWalkContext(_request);
            var graphs = await ExecuteRestoreAsync(localRepository, contextForProject, token);

            // Build the lock file
            LockFile lockFile;
            if (_request.ExistingLockFile != null && _request.ExistingLockFile.IsLocked)
            {
                // No lock file to write!
                lockFile = _request.ExistingLockFile;
            }
            else
            {
                lockFile = new LockFileBuilder(_request.LockFileVersion, _logger, _includeFlagGraphs)
                    .CreateLockFile(
                        _request.ExistingLockFile,
                        _request.Project,
                        graphs,
                        localRepository,
                        contextForProject);

                // If the lock file was locked originally but we are re-locking it, well... re-lock it :)
                lockFile.IsLocked = relockFile;
            }

            if(!ValidateRestoreGraphs(graphs, _logger))
            {
                _success = false;
            }

            // Scan every graph for compatibility, as long as there were no unresolved packages
            var checkResults = new List<CompatibilityCheckResult>();
            if (graphs.All(g => !g.Unresolved.Any()))
            {
                var checker = new CompatibilityChecker(localRepository, lockFile, _logger);
                foreach (var graph in graphs)
                {
                    _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_CheckingCompatibility, graph.Name));

                    var includeFlags = IncludeFlagUtils.FlattenDependencyTypes(_includeFlagGraphs, _request.Project, graph);

                    var res = checker.Check(graph, includeFlags);
                    _success &= res.Success;
                    checkResults.Add(res);
                    if (res.Success)
                    {
                        _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_PackagesAreCompatible, graph.Name));
                    }
                    else
                    {
                        _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_PackagesIncompatible, graph.Name));
                    }
                }
            }

            // Only execute tool restore if the request lock file version is 2 or greater.
            // Tools did not exist prior to v2 lock files.
            var toolRestoreResults = Enumerable.Empty<ToolRestoreResult>();
            if (_request.LockFileVersion >= 2)
            {
                toolRestoreResults = await ExecuteToolRestoresAsync(localRepository, token);
            }

            // Generate Targets/Props files
            var msbuild = RestoreMSBuildFiles(_request.Project, graphs, localRepository, contextForProject);

            // If the request is for a v1 lock file then downgrade it and remove all v2 properties
            if (_request.LockFileVersion == 1)
            {
                DowngradeLockFileToV1(lockFile);
            }

            return new RestoreResult(
                _success,
                graphs,
                checkResults,
                lockFile,
                _request.ExistingLockFile,
                projectLockFilePath,
                msbuild,
                toolRestoreResults);
        }

        private static bool ValidateRestoreGraphs(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            foreach (var g in graphs)
            {
                foreach (var cycle in g.AnalyzeResult.Cycles)
                {
                    logger.LogError(Strings.Log_CycleDetected + $" {Environment.NewLine}  {cycle.GetPath()}.");
                    return false;
                }

                foreach (var versionConflict in g.AnalyzeResult.VersionConflicts)
                {
                    logger.LogError(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Log_VersionConflict,
                            versionConflict.Selected.Key.Name)
                        + $" {Environment.NewLine} {versionConflict.Selected.GetPath()} {Environment.NewLine} {versionConflict.Conflicting.GetPath()}.");
                    return false;
                }

                foreach (var downgrade in g.AnalyzeResult.Downgrades)
                {
                    var downgraded = downgrade.DowngradedFrom;
                    var downgradedBy = downgrade.DowngradedTo;

                    // Not all dependencies have a min version, if one does not exist use 0.0.0
                    var fromVersion = downgraded.Key.VersionRange.MinVersion ?? new NuGetVersion(0, 0, 0);
                    var toVersion = downgradedBy.Key.VersionRange.MinVersion ?? new NuGetVersion(0, 0, 0);

                    logger.LogWarning(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Log_DowngradeWarning,
                            downgraded.Key.Name,
                            fromVersion,
                            toVersion)
                        + $" {Environment.NewLine} {downgraded.GetPath()} {Environment.NewLine} {downgradedBy.GetPath()}");
                }
            }

            return true;
        }

        private async Task<IEnumerable<ToolRestoreResult>> ExecuteToolRestoresAsync(
            NuGetv3LocalRepository localRepository,
            CancellationToken token)
        {
            var toolPathResolver = new ToolPathResolver(_request.PackagesDirectory);
            var lockFileBuilder = new LockFileBuilder(_request.LockFileVersion, _logger, _includeFlagGraphs);
            var results = new List<ToolRestoreResult>();

            foreach (var tool in _request.Project.Tools)
            {
                _logger.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_RestoringToolPackages,
                    tool.LibraryRange.Name,
                    _request.Project.FilePath));

                // Build a package spec in memory to execute the tool restore as if it were
                // its own project. For now, we always restore for a null runtime and a single
                // constant framework.
                var toolPackageSpec = new PackageSpec(new JObject())
                {
                    Name = Guid.NewGuid().ToString(), // make sure this package never collides with a dependency
                    Dependencies = new List<LibraryDependency>(),
                    TargetFrameworks =
                    {
                         new TargetFrameworkInformation
                        {
                            FrameworkName = FrameworkConstants.CommonFrameworks.NetStandardApp15,
                            Dependencies = new List<LibraryDependency>
                            {
                                new LibraryDependency
                                {
                                    LibraryRange = tool.LibraryRange
                                }
                            }
                        }
                    }
                };

                // Execute the restore.
                var toolSuccess = true; // success for this individual tool restore
                var runtimeIds = new HashSet<string>();
                var projectFrameworkRuntimePairs = CreateFrameworkRuntimePairs(toolPackageSpec, runtimeIds);
                var allInstalledPackages = new HashSet<LibraryIdentity>();
                var contextForTool = CreateRemoteWalkContext(_request);
                var walker = new RemoteDependencyWalker(contextForTool);
                var result = await TryRestore(tool.LibraryRange,
                                              projectFrameworkRuntimePairs,
                                              allInstalledPackages,
                                              localRepository,
                                              walker,
                                              contextForTool,
                                              writeToLockFile: true,
                                              token: token);

                var graphs = result.Item2;
                if (!result.Item1)
                {
                    toolSuccess = false;
                    _success = false;
                }

                // Create the lock file (in memory).
                var lockFile = lockFileBuilder.CreateLockFile(
                    previousLockFile: null,
                    project: toolPackageSpec,
                    targetGraphs: graphs,
                    repository: localRepository,
                    context: contextForTool);

                // Build the path based off of the resolved tool. For now, we assume there is only
                // ever one target.
                var target = lockFile.Targets.Single();
                var fileTargetLibrary = target
                    .Libraries
                    .FirstOrDefault(l => StringComparer.OrdinalIgnoreCase.Equals(tool.LibraryRange.Name, l.Name));
                string lockFilePath = null;
                if (fileTargetLibrary != null)
                {
                    lockFilePath = toolPathResolver.GetLockFilePath(
                        fileTargetLibrary.Name,
                        fileTargetLibrary.Version,
                        target.TargetFramework);
                }

                // Validate the results.
                if (!ValidateRestoreGraphs(graphs, _logger))
                {
                    toolSuccess = false;
                    _success = false;
                }

                results.Add(new ToolRestoreResult(toolSuccess, graphs, lockFilePath, lockFile));
            }

            return results;
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteRestoreAsync(NuGetv3LocalRepository localRepository,
            RemoteWalkContext context,
            CancellationToken token)
        {
            if (_request.Project.TargetFrameworks.Count == 0)
            {
                _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_ProjectDoesNotSpecifyTargetFrameworks, _request.Project.Name, _request.Project.FilePath));
                _success = false;
                return Enumerable.Empty<RestoreTargetGraph>();
            }

            _logger.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, _request.Project.FilePath));

            // External references
            var updatedExternalProjects = new List<ExternalProjectReference>(_request.ExternalProjects);

            if (_request.ExternalProjects.Count > 0)
            {
                // There should be at most one match in the external projects.
                var rootProjectMatches = _request.ExternalProjects.Where(proj =>
                     string.Equals(
                         _request.Project.Name,
                         proj.PackageSpecProjectName,
                         StringComparison.OrdinalIgnoreCase))
                     .ToList();

                if (rootProjectMatches.Count > 1)
                {
                    throw new InvalidOperationException($"Ambiguous project name '{_request.Project.Name}'.");
                }

                var rootProject = rootProjectMatches.SingleOrDefault();

                if (rootProject != null)
                {
                    // Replace the project spec with the passed in package spec,
                    // for installs which are done in memory first this will be
                    // different from the one on disk
                    updatedExternalProjects.RemoveAll(project =>
                        project.UniqueName.Equals(rootProject.UniqueName, StringComparison.Ordinal));

                    var updatedReference = new ExternalProjectReference(
                        rootProject.UniqueName,
                        _request.Project,
                        rootProject.MSBuildProjectPath,
                        rootProject.ExternalProjectReferences);

                    updatedExternalProjects.Add(updatedReference);

                    // Determine if the targets and props files should be written out.
                    context.IsMsBuildBased = XProjUtility.IsMSBuildBasedProject(rootProject.MSBuildProjectPath);
                }
                else
                {
                    Debug.Fail("RestoreRequest.ExternaProjects contains references, but does not contain the top level references. Add the project we are restoring for.");
                    throw new InvalidOperationException($"Missing external reference metadata for {_request.Project.Name}");
                }
            }

            // Load repositories

            // the external project provider is specific to the current restore project
            var projectResolver = new PackageSpecResolver(_request.Project);
            context.ProjectLibraryProviders.Add(
                    new PackageSpecReferenceDependencyProvider(projectResolver, updatedExternalProjects));

            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = _request.Project.Name,
                VersionRange = new VersionRange(_request.Project.Version),
                TypeConstraint = LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject
            };

            // Resolve dependency graphs
            var allInstalledPackages = new HashSet<LibraryIdentity>();
            var allGraphs = new List<RestoreTargetGraph>();
            var runtimeIds = RequestRuntimeUtility.GetRestoreRuntimes(_request);
            var projectFrameworkRuntimePairs = CreateFrameworkRuntimePairs(_request.Project, runtimeIds);

            var result = await TryRestore(projectRange,
                                          projectFrameworkRuntimePairs,
                                          allInstalledPackages,
                                          localRepository,
                                          remoteWalker,
                                          context,
                                          writeToLockFile: true,
                                          token: token);

            var success = result.Item1;
            var runtimes = result.Item3;

            allGraphs.AddRange(result.Item2);

            _success = success;

            // Calculate compatibility profiles to check by merging those defined in the project with any from the command line
            foreach (var profile in _request.Project.RuntimeGraph.Supports)
            {
                CompatibilityProfile compatProfile;
                if (profile.Value.RestoreContexts.Any())
                {
                    // Just use the contexts from the project definition
                    compatProfile = profile.Value;
                }
                else if (!runtimes.Supports.TryGetValue(profile.Value.Name, out compatProfile))
                {
                    // No definition of this profile found, so just continue to the next one
                    _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_UnknownCompatibilityProfile, profile.Key));
                    continue;
                }

                foreach (var pair in compatProfile.RestoreContexts)
                {
                    _logger.LogDebug($" {profile.Value.Name} -> +{pair}");
                    _request.CompatibilityProfiles.Add(pair);
                }
            }

            // Walk additional runtime graphs for supports checks
            if (_success && _request.CompatibilityProfiles.Any())
            {
                var compatibilityResult = await TryRestore(projectRange,
                                                          _request.CompatibilityProfiles,
                                                          allInstalledPackages,
                                                          localRepository,
                                                          remoteWalker,
                                                          context,
                                                          writeToLockFile: false,
                                                          token: token);

                _success = compatibilityResult.Item1;

                // TryRestore may contain graphs that are already in allGraphs if the
                // supports section contains the same TxM as the project framework.
                var currentGraphs = new HashSet<KeyValuePair<NuGetFramework, string>>(
                    allGraphs.Select(graph => new KeyValuePair<NuGetFramework, string>(
                        graph.Framework,
                        graph.RuntimeIdentifier))
                    );

                foreach (var graph in compatibilityResult.Item2)
                {
                    var key = new KeyValuePair<NuGetFramework, string>(
                        graph.Framework,
                        graph.RuntimeIdentifier);

                    if (currentGraphs.Add(key))
                    {
                        allGraphs.Add(graph);
                    }
                }
            }

            return allGraphs;
        }

        private static IEnumerable<FrameworkRuntimePair> CreateFrameworkRuntimePairs(
            PackageSpec packageSpec,
            ISet<string> runtimeIds)
        {
            var projectFrameworkRuntimePairs = new List<FrameworkRuntimePair>();
            foreach (var framework in packageSpec.TargetFrameworks)
            {
                // We care about TFM only and null RID for compilation purposes
                projectFrameworkRuntimePairs.Add(new FrameworkRuntimePair(framework.FrameworkName, null));

                foreach (var runtimeId in runtimeIds)
                {
                    projectFrameworkRuntimePairs.Add(new FrameworkRuntimePair(framework.FrameworkName, runtimeId));
                }
            }

            return projectFrameworkRuntimePairs;
        }

        private static RemoteWalkContext CreateRemoteWalkContext(RestoreRequest request)
        {
            var context = new RemoteWalkContext();
            
            foreach (var provider in request.DependencyProviders.LocalProviders)
            {
                context.LocalLibraryProviders.Add(provider);
            }

            foreach (var provider in request.DependencyProviders.RemoteProviders)
            {
                context.RemoteLibraryProviders.Add(provider);
            }

            return context;
        }

        private async Task<Tuple<bool, List<RestoreTargetGraph>, RuntimeGraph>> TryRestore(LibraryRange projectRange,
            IEnumerable<FrameworkRuntimePair> frameworkRuntimePairs,
            HashSet<LibraryIdentity> allInstalledPackages,
            NuGetv3LocalRepository localRepository,
            RemoteDependencyWalker remoteWalker,
            RemoteWalkContext context,
            bool writeToLockFile,
            CancellationToken token)
        {
            var allRuntimes = RuntimeGraph.Empty;
            var frameworkTasks = new List<Task<RestoreTargetGraph>>();
            var graphs = new List<RestoreTargetGraph>();
            var runtimesByFramework = frameworkRuntimePairs.ToLookup(p => p.Framework, p => p.RuntimeIdentifier);

            foreach (var pair in runtimesByFramework)
            {
                _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, pair.Key.DotNetFrameworkName));

                frameworkTasks.Add(WalkDependenciesAsync(projectRange,
                    pair.Key,
                    remoteWalker,
                    context,
                    writeToLockFile: writeToLockFile,
                    token: token));
            }

            var frameworkGraphs = await Task.WhenAll(frameworkTasks);

            graphs.AddRange(frameworkGraphs);

            if (!ResolutionSucceeded(frameworkGraphs))
            {
                return Tuple.Create(false, graphs, allRuntimes);
            }

            await InstallPackagesAsync(graphs,
                    _request.PackagesDirectory,
                    allInstalledPackages,
                    _request.MaxDegreeOfConcurrency,
                    token);

            // Clear the in-memory cache for newly installed packages
            localRepository.ClearCacheForIds(allInstalledPackages.Select(package => package.Name));

            // Resolve runtime dependencies
            var runtimeGraphs = new List<RestoreTargetGraph>();
            if (runtimesByFramework.Count > 0)
            {
                var runtimeTasks = new List<Task<RestoreTargetGraph[]>>();
                foreach (var graph in graphs)
                {
                    // Get the runtime graph for this specific tfm graph
                    var runtimeGraph = GetRuntimeGraph(graph, localRepository);
                    var runtimeIds = runtimesByFramework[graph.Framework];

                    // Merge all runtimes for the output
                    allRuntimes = RuntimeGraph.Merge(allRuntimes, runtimeGraph);

                    runtimeTasks.Add(WalkRuntimeDependenciesAsync(projectRange,
                        graph,
                        runtimeIds.Where(rid => !string.IsNullOrEmpty(rid)),
                        remoteWalker,
                        context,
                        localRepository,
                        runtimeGraph,
                        writeToLockFile: writeToLockFile,
                        token: token));
                }

                foreach (var runtimeSpecificGraph in (await Task.WhenAll(runtimeTasks)).SelectMany(g => g))
                {
                    runtimeGraphs.Add(runtimeSpecificGraph);
                }

                graphs.AddRange(runtimeGraphs);

                if (!ResolutionSucceeded(runtimeGraphs))
                {
                    return Tuple.Create(false, graphs, allRuntimes);
                }

                // Install runtime-specific packages
                await InstallPackagesAsync(runtimeGraphs,
                    _request.PackagesDirectory,
                    allInstalledPackages,
                    _request.MaxDegreeOfConcurrency,
                    token);

                // Clear the in-memory cache for newly installed packages
                localRepository.ClearCacheForIds(allInstalledPackages.Select(package => package.Name));
            }

            return Tuple.Create(true, graphs, allRuntimes);
        }

        private bool ResolutionSucceeded(IEnumerable<RestoreTargetGraph> graphs)
        {
            var success = true;
            foreach (var graph in graphs)
            {
                if (graph.Conflicts.Any())
                {
                    success = false;
                    _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToResolveConflicts, graph.Name));
                    foreach (var conflict in graph.Conflicts)
                    {
                        _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_ResolverConflict, 
                            conflict.Name,
                            string.Join(", ", conflict.Requests)));
                    }
                }
                if (graph.Unresolved.Any())
                {
                    success = false;
                    foreach (var unresolved in graph.Unresolved)
                    {
                        _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_UnresolvedDependency, unresolved.Name,
                            unresolved.VersionRange.ToNonSnapshotRange().PrettyPrint(),
                            graph.Name));
                    }
                }
            }

            return success;
        }

        private MSBuildRestoreResult RestoreMSBuildFiles(PackageSpec project,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            NuGetv3LocalRepository repository,
            RemoteWalkContext context)
        {
            // Get the project graph
            var projectFrameworks = project.TargetFrameworks.Select(f => f.FrameworkName).ToList();

            // Non-Msbuild projects should skip targets and treat it as success
            if (!context.IsMsBuildBased)
            {
                return new MSBuildRestoreResult(project.Name, project.BaseDirectory, success: true);
            }

            // Invalid msbuild projects should write out an msbuild error target
            if (projectFrameworks.Count != 1
                || !targetGraphs.Any())
            {
                return new MSBuildRestoreResult(project.Name, project.BaseDirectory, success: false);
            }

            // Gather props and targets to write out
            var graph = targetGraphs
                .Single(g => g.Framework.Equals(projectFrameworks[0]) && string.IsNullOrEmpty(g.RuntimeIdentifier));

            var pathResolver = new VersionFolderPathResolver(repository.RepositoryRoot);

            var flattenedFlags = IncludeFlagUtils.FlattenDependencyTypes(_includeFlagGraphs, _request.Project, graph);

            var targets = new List<string>();
            var props = new List<string>();
            foreach (var library in graph.Flattened
                .Distinct()
                .OrderBy(g => g.Data.Match.Library))
            {
                var includeLibrary = true;

                LibraryIncludeFlags libraryFlags;
                if (flattenedFlags.TryGetValue(library.Key.Name, out libraryFlags))
                {
                    includeLibrary = libraryFlags.HasFlag(LibraryIncludeFlags.Build);
                }

                // Skip libraries that do not include build files such as transitive packages
                if (includeLibrary)
                {
                    var packageIdentity = new PackageIdentity(library.Key.Name, library.Key.Version);
                    IList<string> packageFiles;
                    context.PackageFileCache.TryGetValue(packageIdentity, out packageFiles);

                    if (packageFiles != null)
                    {
                        var contentItemCollection = new ContentItemCollection();
                        contentItemCollection.Load(packageFiles);

                        // Find MSBuild thingies
                        var groups = contentItemCollection.FindItemGroups(graph.Conventions.Patterns.MSBuildFiles);

                        // Find the nearest msbuild group, this can include the root level Any group.
                        var buildItems = NuGetFrameworkUtility.GetNearest(
                            groups,
                            graph.Framework,
                            group =>
                                group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker]
                                    as NuGetFramework);

                        if (buildItems != null)
                        {
                            // We need to additionally filter to items that are named "{packageId}.targets" and "{packageId}.props"
                            // Filter by file name here and we'll filter by extension when we add things to the lists.
                            var items = buildItems.Items
                                .Where(item =>
                                    Path.GetFileNameWithoutExtension(item.Path)
                                    .Equals(library.Key.Name, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            targets.AddRange(items
                                .Where(c => Path.GetExtension(c.Path).Equals(".targets", StringComparison.OrdinalIgnoreCase))
                                .Select(c =>
                                    Path.Combine(pathResolver.GetPackageDirectory(library.Key.Name, library.Key.Version),
                                    c.Path.Replace('/', Path.DirectorySeparatorChar))));

                            props.AddRange(items
                                .Where(c => Path.GetExtension(c.Path).Equals(".props", StringComparison.OrdinalIgnoreCase))
                                .Select(c =>
                                    Path.Combine(pathResolver.GetPackageDirectory(library.Key.Name, library.Key.Version),
                                    c.Path.Replace('/', Path.DirectorySeparatorChar))));
                        }
                    }
                }
            }

            return new MSBuildRestoreResult(project.Name, project.BaseDirectory, repository.RepositoryRoot, props, targets);
        }

        private void DowngradeLockFileToV1(LockFile lockFile)
        {
            // Remove projects from the library section
            var libraryProjects = lockFile.Libraries.Where(lib => lib.Type == LibraryTypes.Project).ToArray();

            foreach (var library in libraryProjects)
            {
                lockFile.Libraries.Remove(library);
            }

            // Remove projects from the targets section
            foreach (var target in lockFile.Targets)
            {
                var targetProjects = target.Libraries.Where(lib => lib.Type == LibraryTypes.Project).ToArray();

                foreach (var library in targetProjects)
                {
                    target.Libraries.Remove(library);
                }
            }

            foreach (var library in lockFile.Targets.SelectMany(target => target.Libraries))
            {
                // Null out all target types, these did not exist in v1
                library.Type = null;
            }
        }
        

        private Task<RestoreTargetGraph> WalkDependenciesAsync(LibraryRange projectRange,
            NuGetFramework framework,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            bool writeToLockFile,
            CancellationToken token)
        {
            return WalkDependenciesAsync(projectRange,
                framework,
                runtimeIdentifier: null,
                runtimeGraph: RuntimeGraph.Empty,
                walker: walker,
                context: context,
                writeToLockFile: writeToLockFile,
                token: token);
        }

        private async Task<RestoreTargetGraph> WalkDependenciesAsync(LibraryRange projectRange,
            NuGetFramework framework,
            string runtimeIdentifier,
            RuntimeGraph runtimeGraph,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            bool writeToLockFile,
            CancellationToken token)
        {
            var name = FrameworkRuntimePair.GetName(framework, runtimeIdentifier);
            var graphs = new List<GraphNode<RemoteResolveResult>>();
            if (_request.ExistingLockFile != null && _request.ExistingLockFile.IsLocked)
            {
                // Walk all the items in the lock file target and just synthesize the outer graph
                var target = _request.ExistingLockFile.GetTarget(framework, runtimeIdentifier);

                token.ThrowIfCancellationRequested();
                if (target != null)
                {
                    foreach (var targetLibrary in target.Libraries)
                    {
                        token.ThrowIfCancellationRequested();

                        var library = _request.ExistingLockFile.GetLibrary(targetLibrary.Name, targetLibrary.Version);
                        if (library == null)
                        {
                            _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_LockFileMissingLibraryForTargetLibrary, 
                                targetLibrary.Name,
                                targetLibrary.Version,
                                target.Name));
                            continue; // This library is not in the main lockfile?
                        }

                        var range = new LibraryRange()
                        {
                            Name = library.Name,
                            TypeConstraint = LibraryDependencyTargetUtils.Parse(library.Type),
                            VersionRange = new VersionRange(
                                minVersion: library.Version,
                                includeMinVersion: true,
                                maxVersion: library.Version,
                                includeMaxVersion: true)
                        };
                        graphs.Add(await walker.WalkAsync(
                            range,
                            framework,
                            runtimeIdentifier,
                            runtimeGraph,
                            recursive: false));
                    }
                }
            }
            else
            {
                graphs.Add(await walker.WalkAsync(
                    projectRange,
                    framework,
                    runtimeIdentifier,
                    runtimeGraph,
                    recursive: true));
            }

            // Resolve conflicts
            _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_ResolvingConflicts, name));

            // Flatten and create the RestoreTargetGraph to hold the packages
            var result = RestoreTargetGraph.Create(writeToLockFile, runtimeGraph, graphs, context, _logger, framework, runtimeIdentifier);

            // Check if the dependencies got bumped up
            // ...but not if there is an existing locked lock file.
            if (_request.ExistingLockFile == null || !_request.ExistingLockFile.IsLocked)
            {
                // No lock file, OR the lock file is unlocked, so check dependencies
                CheckDependencies(result, _request.Project.Dependencies);

                var fxInfo = _request.Project.GetTargetFramework(framework);
                if (fxInfo != null)
                {
                    CheckDependencies(result, fxInfo.Dependencies);
                }
            }

            return result;
        }

        private void CheckDependencies(RestoreTargetGraph result, IEnumerable<LibraryDependency> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                // Ignore floating or version-less (project) dependencies
                // Avoid warnings for non-packages
                if (dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)
                    && dependency.LibraryRange.VersionRange != null && !dependency.LibraryRange.VersionRange.IsFloating)
                {
                    var match = result.Flattened.FirstOrDefault(g => g.Key.Name.Equals(dependency.LibraryRange.Name));
                    if (match != null 
                        && LibraryTypes.Package == match.Key.Type
                        && match.Key.Version > dependency.LibraryRange.VersionRange.MinVersion)
                    {
                        _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_DependencyBumpedUp, 
                            dependency.LibraryRange.Name,
                            dependency.LibraryRange.VersionRange.PrettyPrint(),
                            match.Key.Name,
                            match.Key.Version));
                    }
                }
            }
        }

        private Task<RestoreTargetGraph[]> WalkRuntimeDependenciesAsync(LibraryRange projectRange,
            RestoreTargetGraph graph,
            IEnumerable<string> runtimeIds,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            NuGetv3LocalRepository localRepository,
            RuntimeGraph runtimes,
            bool writeToLockFile,
            CancellationToken token)
        {
            var resultGraphs = new List<Task<RestoreTargetGraph>>();
            foreach (var runtimeName in runtimeIds)
            {
                _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, FrameworkRuntimePair.GetName(graph.Framework, runtimeName)));

                resultGraphs.Add(WalkDependenciesAsync(projectRange,
                    graph.Framework,
                    runtimeName,
                    runtimes,
                    walker,
                    context,
                    writeToLockFile,
                    token));
            }

            return Task.WhenAll(resultGraphs);
        }

        private RuntimeGraph GetRuntimeGraph(RestoreTargetGraph graph, NuGetv3LocalRepository localRepository)
        {
            // TODO: Caching!
            RuntimeGraph runtimeGraph;
            if (_runtimeGraphCache.TryGetValue(graph.Framework, out runtimeGraph))
            {
                return runtimeGraph;
            }

            _logger.LogVerbose(Strings.Log_ScanningForRuntimeJson);
            runtimeGraph = RuntimeGraph.Empty;
            graph.Graphs.ForEach(node =>
            {
                var match = node?.Item?.Data?.Match;
                if (match == null)
                {
                    return;
                }

                // Ignore runtime.json from rejected nodes
                if (node.Disposition == Disposition.Rejected)
                {
                    return;
                }

                // Locate the package in the local repository
                var package = localRepository.FindPackagesById(match.Library.Name)
                    .FirstOrDefault(p => p.Version == match.Library.Version);

                if (package != null)
                {
                    var nextGraph = LoadRuntimeGraph(package);
                    if (nextGraph != null)
                    {
                        _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_MergingRuntimes, match.Library));
                        runtimeGraph = RuntimeGraph.Merge(runtimeGraph, nextGraph);
                    }
                }
            });
            _runtimeGraphCache[graph.Framework] = runtimeGraph;
            return runtimeGraph;
        }

        private RuntimeGraph LoadRuntimeGraph(LocalPackageInfo package)
        {
            var id = new PackageIdentity(package.Id, package.Version);
            return _runtimeGraphCacheByPackage.GetOrAdd(id, (x) => LoadRuntimeGraphCore(package));
        }

        private RuntimeGraph LoadRuntimeGraphCore(LocalPackageInfo package)
        {
            var runtimeGraphFile = Path.Combine(package.ExpandedPath, RuntimeGraph.RuntimeGraphFileName);
            if (File.Exists(runtimeGraphFile))
            {
                using (var stream = File.OpenRead(runtimeGraphFile))
                {
                    return JsonRuntimeFormat.ReadRuntimeGraph(stream);
                }
            }
            return null;
        }

        private async Task InstallPackagesAsync(IEnumerable<RestoreTargetGraph> graphs,
            string packagesDirectory,
            HashSet<LibraryIdentity> allInstalledPackages,
            int maxDegreeOfConcurrency,
            CancellationToken token)
        {
            var packagesToInstall = graphs.SelectMany(g => g.Install.Where(match => allInstalledPackages.Add(match.Library)));
            if (maxDegreeOfConcurrency <= 1)
            {
                foreach (var match in packagesToInstall)
                {
                    await InstallPackageAsync(match, packagesDirectory, token);
                }
            }
            else
            {
                var bag = new ConcurrentBag<RemoteMatch>(packagesToInstall);
                var tasks = Enumerable.Range(0, maxDegreeOfConcurrency)
                    .Select(async _ =>
                        {
                            RemoteMatch match;
                            while (bag.TryTake(out match))
                            {
                                await InstallPackageAsync(match, packagesDirectory, token);
                            }
                        });
                await Task.WhenAll(tasks);
            }
        }

        private async Task InstallPackageAsync(RemoteMatch installItem, string packagesDirectory, CancellationToken token)
        {
            var packageIdentity = new PackageIdentity(installItem.Library.Name, installItem.Library.Version);

            var versionFolderPathContext = new VersionFolderPathContext(
                packageIdentity,
                packagesDirectory,
                _logger,
                fixNuspecIdCasing: true,
                packageSaveMode: _request.PackageSaveMode,
                normalizeFileNames: false,
                xmlDocFileSaveMode: _request.XmlDocFileSaveMode);

            await PackageExtractor.InstallFromSourceAsync(
                stream => installItem.Provider.CopyToAsync(installItem.Library, stream, token),
                versionFolderPathContext,
                token);
        }
    }
}
