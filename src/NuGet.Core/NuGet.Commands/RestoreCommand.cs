// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreCommand
    {
        private readonly ILogger _log;
        private readonly RestoreRequest _request;

        private bool _success = true;

        private readonly Dictionary<NuGetFramework, RuntimeGraph> _runtimeGraphCache = new Dictionary<NuGetFramework, RuntimeGraph>();
        private readonly ConcurrentDictionary<PackageIdentity, RuntimeGraph> _runtimeGraphCacheByPackage
            = new ConcurrentDictionary<PackageIdentity, RuntimeGraph>(PackageIdentity.Comparer);

        public RestoreCommand(ILogger logger, RestoreRequest request)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

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

            _log = logger;
            _request = request;
        }

        public Task<RestoreResult> ExecuteAsync()
        {
            return ExecuteAsync(CancellationToken.None);
        }

        public async Task<RestoreResult> ExecuteAsync(CancellationToken token)
        {
            var localRepository = new NuGetv3LocalRepository(_request.PackagesDirectory, checkPackageIdCase: false);
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
                _log.LogInformation(Strings.Log_LockFileOutOfDate);
            }

            var context = new RemoteWalkContext();

            var graphs = await ExecuteRestoreAsync(localRepository, context, token);

            // Build the lock file
            LockFile lockFile;
            if (_request.ExistingLockFile != null && _request.ExistingLockFile.IsLocked)
            {
                // No lock file to write!
                lockFile = _request.ExistingLockFile;
            }
            else
            {
                lockFile = CreateLockFile(_request.ExistingLockFile, _request.Project, graphs, localRepository, context);

                // If the lock file was locked originally but we are re-locking it, well... re-lock it :)
                lockFile.IsLocked = relockFile;
            }

            foreach (var g in graphs)
            {
                foreach (var cycle in g.AnalyzeResult.Cycles)
                {
                    _success = false;
                    _log.LogError(Strings.Log_CycleDetected + $" {Environment.NewLine}  {cycle.GetPath()}.");
                }

                foreach (var versionConflict in g.AnalyzeResult.VersionConflicts)
                {
                    _success = false;
                    _log.LogError(Strings.FormatLog_VersionConflict(versionConflict.Selected.Key.Name) + $" {Environment.NewLine} {versionConflict.Selected.GetPath()} {Environment.NewLine} {versionConflict.Conflicting.GetPath()}.");
                }

                foreach (var downgrade in g.AnalyzeResult.Downgrades)
                {
                    var downgraded = downgrade.DowngradedFrom;
                    var downgradedBy = downgrade.DowngradedTo;

                    // Not all dependencies have a min version, if one does not exist use 0.0.0
                    var fromVersion = downgraded.Key.VersionRange.MinVersion ?? new NuGetVersion(0, 0, 0);
                    var toVersion = downgradedBy.Key.VersionRange.MinVersion ?? new NuGetVersion(0, 0, 0);

                    _log.LogWarning(Strings.FormatLog_DowngradeWarning(downgraded.Key.Name, fromVersion, toVersion) + $" {Environment.NewLine} {downgraded.GetPath()} {Environment.NewLine} {downgradedBy.GetPath()}");
                }
            }

            // Scan every graph for compatibility, as long as there were no unresolved packages
            var checkResults = new List<CompatibilityCheckResult>();
            if (graphs.All(g => !g.Unresolved.Any()))
            {
                var checker = new CompatibilityChecker(localRepository, lockFile, _log);
                foreach (var graph in graphs)
                {
                    _log.LogVerbose(Strings.FormatLog_CheckingCompatibility(graph.Name));
                    var res = checker.Check(graph);
                    _success &= res.Success;
                    checkResults.Add(res);
                    if (res.Success)
                    {
                        _log.LogInformation(Strings.FormatLog_PackagesAreCompatible(graph.Name));
                    }
                    else
                    {
                        _log.LogError(Strings.FormatLog_PackagesIncompatible(graph.Name));
                    }
                }
            }

            // Generate Targets/Props files
            var msbuild = RestoreMSBuildFiles(_request.Project, graphs, localRepository, context);

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
                relockFile,
                msbuild);
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteRestoreAsync(NuGetv3LocalRepository localRepository,
            RemoteWalkContext context,
            CancellationToken token)
        {
            if (_request.Project.TargetFrameworks.Count == 0)
            {
                _log.LogError(Strings.FormatLog_ProjectDoesNotSpecifyTargetFrameworks(_request.Project.Name, _request.Project.FilePath));
                _success = false;
                return Enumerable.Empty<RestoreTargetGraph>();
            }

            _log.LogInformation(Strings.FormatLog_RestoringPackages(_request.Project.FilePath));

            // Load repositories
            var projectResolver = new PackageSpecResolver(_request.Project);
            var nugetRepository = Repository.Factory.GetCoreV3(_request.PackagesDirectory);

            // External references
            var updatedExternalProjects = new List<ExternalProjectReference>(_request.ExternalProjects);

            if (_request.ExternalProjects.Any())
            {
                var rootProject = _request.ExternalProjects.SingleOrDefault(proj =>
                     string.Equals(_request.Project.Name, proj.UniqueName, StringComparison.OrdinalIgnoreCase));

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
                }
                else
                {
                    Debug.Fail("RestoreRequest.ExternaProjects contains references, but does not contain the top level references. Add the project we are restoring for.");
                    throw new InvalidOperationException($"Missing external reference metadata for {_request.Project.Name}");
                }
            }

            context.ProjectLibraryProviders.Add(
                    new PackageSpecReferenceDependencyProvider(projectResolver, updatedExternalProjects));

            context.LocalLibraryProviders.Add(
                new SourceRepositoryDependencyProvider(nugetRepository, _log, _request.CacheContext));

            foreach (var provider in _request.Sources.Select(s => CreateProviderFromSource(s, _request.CacheContext)))
            {
                context.RemoteLibraryProviders.Add(provider);
            }

            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = _request.Project.Name,
                VersionRange = new VersionRange(_request.Project.Version),
                TypeConstraint = LibraryTypes.Project
            };

            // Resolve dependency graphs
            var projectFrameworkRuntimePairs = new List<FrameworkRuntimePair>();
            var allInstalledPackages = new HashSet<LibraryIdentity>();
            var allGraphs = new List<RestoreTargetGraph>();

            // Compute the project framework + runtime id pairs based on project information
            foreach (var framework in _request.Project.TargetFrameworks)
            {
                // We care about TFM only and null RID for compilation purposes
                projectFrameworkRuntimePairs.Add(new FrameworkRuntimePair(framework.FrameworkName, null));

                if (!framework.FrameworkName.IsCompileOnly)
                {
                    var runtimeIds = RequestRuntimeUtility.GetRestoreRuntimes(_request);

                    foreach (var runtimeId in runtimeIds)
                    {
                        projectFrameworkRuntimePairs.Add(new FrameworkRuntimePair(framework.FrameworkName, runtimeId));
                    }
                }
            }

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
                    _log.LogWarning(Strings.FormatLog_UnknownCompatibilityProfile(profile.Key));
                    continue;
                }

                foreach (var pair in compatProfile.RestoreContexts)
                {
                    _log.LogDebug($" {profile.Value.Name} -> +{pair}");
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
                _log.LogVerbose(Strings.FormatLog_RestoringPackages(pair.Key.DotNetFrameworkName));

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
                    _log.LogError(Strings.FormatLog_FailedToResolveConflicts(graph.Name));
                    foreach (var conflict in graph.Conflicts)
                    {
                        _log.LogError(Strings.FormatLog_ResolverConflict(
                            conflict.Name,
                            string.Join(", ", conflict.Requests)));
                    }
                }
                if (graph.Unresolved.Any())
                {
                    success = false;
                    foreach (var unresolved in graph.Unresolved)
                    {
                        _log.LogError(Strings.FormatLog_UnresolvedDependency(unresolved.Name,
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
            if (projectFrameworks.Count > 1 || !targetGraphs.Any())
            {
                return new MSBuildRestoreResult(project.Name, project.BaseDirectory);
            }

            var graph = targetGraphs
                .Single(g => g.Framework.Equals(projectFrameworks[0]) && string.IsNullOrEmpty(g.RuntimeIdentifier));

            var pathResolver = new VersionFolderPathResolver(repository.RepositoryRoot);

            var flattenedFlags = IncludeFlagUtils.FlattenDependencyTypes(graph, project);

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

        private LockFile CreateLockFile(
            LockFile previousLockFile,
            PackageSpec project,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            NuGetv3LocalRepository repository,
            RemoteWalkContext context)
        {
            var lockFile = new LockFile();
            lockFile.Version = _request.LockFileVersion;

            var resolver = new VersionFolderPathResolver(repository.RepositoryRoot);
            var previousLibraries = previousLockFile?.Libraries.ToDictionary(l => Tuple.Create(l.Name, l.Version));

            // Use empty string as the key of dependencies shared by all frameworks
            lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                string.Empty,
                project.Dependencies
                    .Select(group => group.LibraryRange.ToLockFileDependencyGroupString())
                    .OrderBy(group => group, StringComparer.Ordinal)));

            foreach (var frameworkInfo in project.TargetFrameworks
                                            .OrderBy(framework => framework.FrameworkName.ToString(),
                                                StringComparer.Ordinal))
            {
                lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                    frameworkInfo.FrameworkName.ToString(),
                    frameworkInfo.Dependencies
                        .Select(x => x.LibraryRange.ToLockFileDependencyGroupString())
                        .OrderBy(dependency => dependency, StringComparer.Ordinal)));
            }

            // Record all libraries used
            foreach (var item in targetGraphs.SelectMany(g => g.Flattened).Distinct()
                .OrderBy(x => x.Data.Match.Library))
            {
                var library = item.Data.Match.Library;

                if (project.Name.Equals(library.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // Do not include the project itself as a library.
                    continue;
                }

                if (library.Type == LibraryTypes.Project || library.Type == LibraryTypes.ExternalProject)
                {
                    // Project
                    LocalMatch localMatch = (LocalMatch)item.Data.Match;

                    var projectLib = new LockFileLibrary()
                    {
                        Name = library.Name,
                        Version = library.Version,
                        Type = LibraryTypes.Project,
                    };

                    // Set the relative path if a path exists
                    // For projects without project.json this will be empty
                    if (!string.IsNullOrEmpty(localMatch.LocalLibrary.Path))
                    {
                        projectLib.Path = PathUtility.GetRelativePath(
                            project.FilePath,
                            localMatch.LocalLibrary.Path,
                            '/');
                    }

                    // The msbuild project path if it exists
                    object msbuildPath;
                    if (localMatch.LocalLibrary.Items.TryGetValue(KnownLibraryProperties.MSBuildProjectPath, out msbuildPath))
                    {
                        var msbuildRelativePath = PathUtility.GetRelativePath(
                            project.FilePath,
                            (string)msbuildPath,
                            '/');

                        projectLib.MSBuildProject = msbuildRelativePath;
                    }

                    lockFile.Libraries.Add(projectLib);
                }
                else
                {
                    // Packages
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var sha512 = File.ReadAllText(resolver.GetHashPath(packageInfo.Id, packageInfo.Version));

                    LockFileLibrary previousLibrary = null;
                    previousLibraries?.TryGetValue(Tuple.Create(library.Name, library.Version), out previousLibrary);

                    var lockFileLib = previousLibrary;

                    // If we have the same library in the lock file already, use that.
                    if (previousLibrary == null || previousLibrary.Sha512 != sha512)
                    {
                        lockFileLib = CreateLockFileLibrary(
                            packageInfo,
                            sha512,
                            correctedPackageName: library.Name);
                    }
                    else if (Path.DirectorySeparatorChar != '/')
                    {
                        // Fix slashes for content model patterns
                        lockFileLib.Files = lockFileLib.Files
                            .Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                            .ToList();
                    }

                    lockFile.Libraries.Add(lockFileLib);

                    var packageIdentity = new PackageIdentity(lockFileLib.Name, lockFileLib.Version);
                    context.PackageFileCache.TryAdd(packageIdentity, lockFileLib.Files);
                }
            }

            var libraries = lockFile.Libraries.ToDictionary(lib => Tuple.Create(lib.Name, lib.Version));

            var warnForImports = project.TargetFrameworks.Any(framework => framework.Warn);
            var librariesWithWarnings = new HashSet<LibraryIdentity>();

            // Add the targets
            foreach (var targetGraph in targetGraphs
                .OrderBy(graph => graph.Framework.ToString(), StringComparer.Ordinal)
                .ThenBy(graph => graph.RuntimeIdentifier, StringComparer.Ordinal))
            {
                var target = new LockFileTarget();
                target.TargetFramework = targetGraph.Framework;
                target.RuntimeIdentifier = targetGraph.RuntimeIdentifier;

                var flattenedFlags = IncludeFlagUtils.FlattenDependencyTypes(targetGraph, project);

                var fallbackFramework = target.TargetFramework as FallbackFramework;
                var warnForImportsOnGraph = warnForImports && fallbackFramework != null;

                foreach (var graphItem in targetGraph.Flattened.OrderBy(x => x.Key))
                {
                    var library = graphItem.Key;

                    if (library.Type == LibraryTypes.Project || library.Type == LibraryTypes.ExternalProject)
                    {
                        if (project.Name.Equals(library.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            // Do not include the project itself as a library.
                            continue;
                        }

                        var localMatch = (LocalMatch)graphItem.Data.Match;

                        // Target framework information is optional and may not exist for csproj projects
                        // that do not have a project.json file.
                        string projectFramework = null;
                        object frameworkInfoObject;
                        if (localMatch.LocalLibrary.Items.TryGetValue(
                            KnownLibraryProperties.TargetFrameworkInformation,
                            out frameworkInfoObject))
                        {
                            var targetFrameworkInformation = (TargetFrameworkInformation)frameworkInfoObject;
                            projectFramework = targetFrameworkInformation.FrameworkName?.DotNetFrameworkName;
                        }

                        // Create the target entry
                        var lib = new LockFileTargetLibrary()
                        {
                            Name = library.Name,
                            Version = library.Version,
                            Type = LibraryTypes.Project,
                            Framework = projectFramework,

                            // Find all dependencies which would be in the nuspec
                            // If there is no
                            Dependencies = graphItem.Data.Dependencies
                                .Where(
                                    d => d.LibraryRange.TypeConstraint == null
                                    || d.LibraryRange.TypeConstraint == LibraryTypes.Package
                                    || d.LibraryRange.TypeConstraint == LibraryTypes.Project
                                    || d.LibraryRange.TypeConstraint == LibraryTypes.ExternalProject)
                                .Select(d => GetDependencyVersionRange(d))
                                .ToList()
                        };

                        object compileAssetObject;
                        if (localMatch.LocalLibrary.Items.TryGetValue(
                            KnownLibraryProperties.CompileAsset,
                            out compileAssetObject))
                        {
                            var item = new LockFileItem((string)compileAssetObject);
                            lib.CompileTimeAssemblies.Add(item);
                            lib.RuntimeAssemblies.Add(item);
                        }

                        target.Libraries.Add(lib);
                        continue;
                    }

                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    // include flags
                    LibraryIncludeFlags includeFlags;
                    if (!flattenedFlags.TryGetValue(library.Name, out includeFlags))
                    {
                        includeFlags = ~LibraryIncludeFlags.ContentFiles;
                    }

                    var targetLibrary = LockFileUtils.CreateLockFileTargetLibrary(
                        libraries[Tuple.Create(library.Name, library.Version)],
                        packageInfo,
                        targetGraph,
                        resolver,
                        correctedPackageName: library.Name,
                        dependencyType: includeFlags);

                    target.Libraries.Add(targetLibrary);

                    // Log warnings if the target library used the fallback framework
                    if (warnForImportsOnGraph && !librariesWithWarnings.Contains(library))
                    {
                        var nonFallbackFramework = new NuGetFramework(fallbackFramework);

                        var targetLibraryWithoutFallback = LockFileUtils.CreateLockFileTargetLibrary(
                            libraries[Tuple.Create(library.Name, library.Version)],
                            packageInfo,
                            targetGraph,
                            resolver,
                            correctedPackageName: library.Name,
                            targetFrameworkOverride: nonFallbackFramework,
                            dependencyType: includeFlags);

                        if (!targetLibrary.Equals(targetLibraryWithoutFallback))
                        {
                            var libraryName = $"{library.Name} {library.Version}";
                            _log.LogWarning(Strings.FormatLog_ImportsFallbackWarning(libraryName, fallbackFramework.Fallback, nonFallbackFramework));

                            // only log the warning once per library
                            librariesWithWarnings.Add(library);
                        }
                    }
                }

                lockFile.Targets.Add(target);
            }

            return lockFile;
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

        private static PackageDependency GetDependencyVersionRange(LibraryDependency dependency)
        {
            var range = dependency.LibraryRange.VersionRange;

            if (range == null && dependency.LibraryRange.TypeConstraint == LibraryTypes.ExternalProject)
            {
                // For csproj -> csproj type references where there is no range, use 1.0.0
                range = VersionRange.Parse("1.0.0");
            }
            else
            {
                // For project dependencies drop the snapshot version.
                // Ex: 1.0.0-* -> 1.0.0
                range = range.ToNonSnapshotRange();
            }

            return new PackageDependency(dependency.Name, range);
        }

        private LockFileLibrary CreateLockFileLibrary(LocalPackageInfo package, string sha512, string correctedPackageName)
        {
            var lockFileLib = new LockFileLibrary();

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;
            lockFileLib.Type = LibraryTypes.Package;
            lockFileLib.Sha512 = sha512;

            using (var packageReader = new PackageFolderReader(package.ExpandedPath))
            {
                // Get package files, excluding directory entries and OPC files
                // This is sorted before it is written out
                lockFileLib.Files = packageReader
                    .GetFiles()
                    .Where(file => IsAllowedLibraryFile(file))
                    .ToList();
            }

            return lockFileLib;
        }

        /// <summary>
        /// True if the file should be added to the lock file library
        /// Fale if it is an OPC file or empty directory
        /// </summary>
        private static bool IsAllowedLibraryFile(string path)
        {
            switch (path)
            {
                case "_rels/.rels":
                case "[Content_Types].xml":
                    return false;
            }

            if (path.EndsWith("/", StringComparison.Ordinal)
                || path.EndsWith(".psmdcp", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
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
                            _log.LogWarning(Strings.FormatLog_LockFileMissingLibraryForTargetLibrary(
                                targetLibrary.Name,
                                targetLibrary.Version,
                                target.Name));
                            continue; // This library is not in the main lockfile?
                        }

                        var range = new LibraryRange()
                        {
                            Name = library.Name,
                            TypeConstraint = library.Type, // Lockfile contains only Package libraries.
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
            _log.LogVerbose(Strings.FormatLog_ResolvingConflicts(name));

            // Flatten and create the RestoreTargetGraph to hold the packages
            var result = RestoreTargetGraph.Create(writeToLockFile, runtimeGraph, graphs, context, _log, framework, runtimeIdentifier);

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
                if (dependency.LibraryRange.VersionRange != null && !dependency.LibraryRange.VersionRange.IsFloating)
                {
                    var match = result.Flattened.FirstOrDefault(g => g.Key.Name.Equals(dependency.LibraryRange.Name));
                    if (match != null && match.Key.Version > dependency.LibraryRange.VersionRange.MinVersion)
                    {
                        _log.LogWarning(Strings.FormatLog_DependencyBumpedUp(
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
                _log.LogVerbose(Strings.FormatLog_RestoringPackages(FrameworkRuntimePair.GetName(graph.Framework, runtimeName)));

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

            _log.LogVerbose(Strings.Log_ScanningForRuntimeJson);
            runtimeGraph = RuntimeGraph.Empty;
            graph.Graphs.ForEach(node =>
            {
                var match = node?.Item?.Data?.Match;
                if (match == null)
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
                        _log.LogVerbose(Strings.FormatLog_MergingRuntimes(match.Library));
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
                _log,
                fixNuspecIdCasing: true,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            await PackageExtractor.InstallFromSourceAsync(
                stream => installItem.Provider.CopyToAsync(installItem.Library, stream, token),
                versionFolderPathContext,
                token);
        }

        private IRemoteDependencyProvider CreateProviderFromSource(
            SourceRepository repository,
            SourceCacheContext cacheContext)
        {
            _log.LogVerbose(Strings.FormatLog_UsingSource(repository.PackageSource.Source));

            return new SourceRepositoryDependencyProvider(repository, _log, cacheContext);
        }
    }
}
