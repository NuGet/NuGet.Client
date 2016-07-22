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
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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
            // Local package folders (non-sources)
            var localRepositories = new List<NuGetv3LocalRepository>();
            localRepositories.Add(_request.DependencyProviders.GlobalPackages);
            localRepositories.AddRange(_request.DependencyProviders.FallbackPackageFolders);

            var projectLockFilePath = string.IsNullOrEmpty(_request.LockFilePath) ?
                Path.Combine(_request.Project.BaseDirectory, LockFileFormat.LockFileName) :
                _request.LockFilePath;

            var contextForProject = CreateRemoteWalkContext(_request);

            var graphs = await ExecuteRestoreAsync(
                _request.DependencyProviders.GlobalPackages,
                _request.DependencyProviders.FallbackPackageFolders,
                contextForProject,
                token);

            // Only execute tool restore if the request lock file version is 2 or greater.
            // Tools did not exist prior to v2 lock files.
            var toolRestoreResults = Enumerable.Empty<ToolRestoreResult>();
            if (_request.LockFileVersion >= 2)
            {
                toolRestoreResults = await ExecuteToolRestoresAsync(
                                    _request.DependencyProviders.GlobalPackages,
                                    _request.DependencyProviders.FallbackPackageFolders,
                                    token);
            }

            var lockFile = BuildLockFile(
                _request.ExistingLockFile,
                _request.Project,
                graphs,
                localRepositories,
                contextForProject,
                toolRestoreResults);

            if (!ValidateRestoreGraphs(graphs, _logger))
            {
                _success = false;
            }

            var checkResults = VerifyCompatibility(
                _request.Project,
                _includeFlagGraphs,
                localRepositories,
                lockFile,
                graphs,
                _logger);

            if (checkResults.Any(r => !r.Success))
            {
                _success = false;
            }

            // Generate Targets/Props files
            var msbuild = BuildAssetsUtils.RestoreMSBuildFiles(
                _request.Project,
                graphs,
                localRepositories,
                contextForProject,
                _request,
                _includeFlagGraphs);

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

        private LockFile BuildLockFile(
            LockFile existingLockFile,
            PackageSpec project,
            IEnumerable<RestoreTargetGraph> graphs,
            IReadOnlyList<NuGetv3LocalRepository> localRepositories,
            RemoteWalkContext contextForProject,
            IEnumerable<ToolRestoreResult> toolRestoreResults)
        {
            // Build the lock file
            var lockFile = new LockFileBuilder(_request.LockFileVersion, _logger, _includeFlagGraphs)
                    .CreateLockFile(
                        existingLockFile,
                        project,
                        graphs,
                        localRepositories,
                        contextForProject,
                        toolRestoreResults);

            return lockFile;
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

        private static IList<CompatibilityCheckResult> VerifyCompatibility(
            PackageSpec project,
            Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs,
            IReadOnlyList<NuGetv3LocalRepository> localRepositories,
            LockFile lockFile,
            IEnumerable<RestoreTargetGraph> graphs,
            ILogger logger)
        {
            // Scan every graph for compatibility, as long as there were no unresolved packages
            var checkResults = new List<CompatibilityCheckResult>();
            if (graphs.All(g => !g.Unresolved.Any()))
            {
                var checker = new CompatibilityChecker(localRepositories, lockFile, logger);
                foreach (var graph in graphs)
                {
                    logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_CheckingCompatibility, graph.Name));

                    var includeFlags = IncludeFlagUtils.FlattenDependencyTypes(includeFlagGraphs, project, graph);

                    var res = checker.Check(graph, includeFlags);
                    checkResults.Add(res);
                    if (res.Success)
                    {
                        logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_PackagesAndProjectsAreCompatible, graph.Name));
                    }
                    else
                    {
                        // Get error counts on a project vs package basis
                        var projectCount = res.Issues.Count(issue => issue.Type == CompatibilityIssueType.ProjectIncompatible);
                        var packageCount = res.Issues.Count(issue => issue.Type != CompatibilityIssueType.ProjectIncompatible);

                        // Log a summary with compatibility error counts
                        if (projectCount > 0)
                        {
                            logger.LogError(
                                string.Format(CultureInfo.CurrentCulture,
                                    Strings.Log_ProjectsIncompatible,
                                    graph.Name));

                            logger.LogDebug($"Incompatible projects: {projectCount}");
                        }

                        if (packageCount > 0)
                        {
                            logger.LogError(
                                string.Format(CultureInfo.CurrentCulture,
                                    Strings.Log_PackagesIncompatible,
                                    graph.Name));

                            logger.LogDebug($"Incompatible packages: {packageCount}");
                        }
                    }
                }
            }

            return checkResults;
        }

        private async Task<IEnumerable<ToolRestoreResult>> ExecuteToolRestoresAsync(
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            CancellationToken token)
        {
            var toolPathResolver = new ToolPathResolver(_request.PackagesDirectory);
            var results = new List<ToolRestoreResult>();

            var localRepositories = new List<NuGetv3LocalRepository>();
            localRepositories.Add(userPackageFolder);
            localRepositories.AddRange(fallbackPackageFolders);

            foreach (var tool in _request.Project.Tools)
            {
                _logger.LogMinimal(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_RestoringToolPackages,
                    tool.LibraryRange.Name,
                    _request.Project.FilePath));

                // Build the fallback framework (which uses the "imports").
                var framework = LockFile.ToolFramework;
                if (tool.Imports.Any())
                {
                    framework = new FallbackFramework(framework, tool.Imports);
                }

                // Build a package spec in memory to execute the tool restore as if it were
                // its own project. For now, we always restore for a null runtime and a single
                // constant framework.
                var toolPackageSpec = new PackageSpec(new JObject())
                {
                    Name = Guid.NewGuid().ToString(), // make sure this package never collides with a dependency
                    Dependencies = new List<LibraryDependency>(),
                    Tools = new List<ToolDependency>(),
                    TargetFrameworks =
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = framework,
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

                // Try to find the existing lock file. Since the existing lock file is pathed under
                // a folder that includes the resolved tool's version, this is a bit of a chicken
                // and egg problem. That is, we need to run the restore operation in order to resolve
                // a tool version, but we need the tool version to find the existing project.lock.json
                // file which is required before executing the restore! Fortunately, this is solved by
                // looking at the tool's consuming project's lock file to see if the tool has been
                // restored before.
                LockFile existingToolLockFile = null;
                if (_request.ExistingLockFile != null)
                {
                    var existingTarget = _request
                        .ExistingLockFile
                        .Tools
                        .Where(t => t.RuntimeIdentifier == null)
                        .Where(t => t.TargetFramework.Equals(LockFile.ToolFramework))
                        .FirstOrDefault();

                    var existingLibrary = existingTarget?.Libraries
                        .Where(l => StringComparer.OrdinalIgnoreCase.Equals(l.Name, tool.LibraryRange.Name))
                        .Where(l => tool.LibraryRange.VersionRange.Satisfies(l.Version))
                        .FirstOrDefault();

                    if (existingLibrary != null)
                    {
                        var existingLockFilePath = toolPathResolver.GetLockFilePath(
                            existingLibrary.Name,
                            existingLibrary.Version,
                            existingTarget.TargetFramework);

                        existingToolLockFile = LockFileUtilities.GetLockFile(existingLockFilePath, _logger);
                    }
                }

                // Execute the restore.
                var toolSuccess = true; // success for this individual tool restore
                var runtimeIds = new HashSet<string>();
                var projectFrameworkRuntimePairs = CreateFrameworkRuntimePairs(toolPackageSpec, runtimeIds);
                var allInstalledPackages = new HashSet<LibraryIdentity>();
                var contextForTool = CreateRemoteWalkContext(_request);
                var walker = new RemoteDependencyWalker(contextForTool);
                var projectRestoreRequest = new ProjectRestoreRequest(
                    _request,
                    toolPackageSpec,
                    existingToolLockFile,
                    new Dictionary<NuGetFramework, RuntimeGraph>(),
                    _runtimeGraphCacheByPackage);
                var projectRestoreCommand = new ProjectRestoreCommand(_logger, projectRestoreRequest);
                var result = await projectRestoreCommand.TryRestore(
                    tool.LibraryRange,
                    projectFrameworkRuntimePairs,
                    allInstalledPackages,
                    userPackageFolder,
                    fallbackPackageFolders,
                    walker,
                    contextForTool,
                    writeToLockFile: true,
                    forceRuntimeGraphCreation: false,
                    token: token);

                var graphs = result.Item2;
                if (!result.Item1)
                {
                    toolSuccess = false;
                    _success = false;
                }

                // Create the lock file (in memory).
                var toolLockFile = BuildLockFile(
                    existingToolLockFile,
                    toolPackageSpec,
                    graphs,
                    localRepositories,
                    contextForTool,
                    Enumerable.Empty<ToolRestoreResult>());

                // Build the path based off of the resolved tool. For now, we assume there is only
                // ever one target.
                var target = toolLockFile.Targets.Single();
                var fileTargetLibrary = target
                    .Libraries
                    .FirstOrDefault(l => StringComparer.OrdinalIgnoreCase.Equals(tool.LibraryRange.Name, l.Name));
                string toolLockFilePath = null;
                if (fileTargetLibrary != null)
                {
                    toolLockFilePath = toolPathResolver.GetLockFilePath(
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

                var checkResults = VerifyCompatibility(
                    toolPackageSpec,
                    new Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>>(),
                    localRepositories,
                    toolLockFile,
                    graphs,
                    _logger);

                if (checkResults.Any(r => !r.Success))
                {
                    toolSuccess = false;
                    _success = false;
                }

                results.Add(new ToolRestoreResult(
                    tool.LibraryRange.Name,
                    toolSuccess,
                    target,
                    fileTargetLibrary,
                    toolLockFilePath,
                    toolLockFile,
                    existingToolLockFile));
            }

            return results;
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteRestoreAsync(
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
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
                    new PackageSpecReferenceDependencyProvider(projectResolver, updatedExternalProjects, _logger));

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
            var hasSupports = _request.Project.RuntimeGraph.Supports.Count > 0;

            var projectRestoreRequest = new ProjectRestoreRequest(
                _request,
                _request.Project,
                _request.ExistingLockFile,
                _runtimeGraphCache,
                _runtimeGraphCacheByPackage);
            var projectRestoreCommand = new ProjectRestoreCommand(_logger, projectRestoreRequest);
            var result = await projectRestoreCommand.TryRestore(
                projectRange,
                projectFrameworkRuntimePairs,
                allInstalledPackages,
                userPackageFolder,
                fallbackPackageFolders,
                remoteWalker,
                context,
                writeToLockFile: true,
                forceRuntimeGraphCreation: hasSupports,
                token: token);

            var success = result.Item1;

            allGraphs.AddRange(result.Item2);

            _success = success;

            // Calculate compatibility profiles to check by merging those defined in the project with any from the command line
            foreach (var profile in _request.Project.RuntimeGraph.Supports)
            {
                var runtimes = result.Item3;

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
                var compatibilityResult = await projectRestoreCommand.TryRestore(projectRange,
                                                          _request.CompatibilityProfiles,
                                                          allInstalledPackages,
                                                          userPackageFolder,
                                                          fallbackPackageFolders,
                                                          remoteWalker,
                                                          context,
                                                          writeToLockFile: false,
                                                          forceRuntimeGraphCreation: true,
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

        private void DowngradeLockFileToV1(LockFile lockFile)
        {
            // Remove projects from the library section
            var libraryProjects = lockFile.Libraries.Where(lib => lib.Type == LibraryType.Project).ToArray();

            foreach (var library in libraryProjects)
            {
                lockFile.Libraries.Remove(library);
            }

            // Remove projects from the targets section
            foreach (var target in lockFile.Targets)
            {
                var targetProjects = target.Libraries.Where(lib => lib.Type == LibraryType.Project).ToArray();

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

            // Remove tools
            lockFile.Tools.Clear();
            lockFile.ProjectFileToolGroups.Clear();
        }
    }
}
