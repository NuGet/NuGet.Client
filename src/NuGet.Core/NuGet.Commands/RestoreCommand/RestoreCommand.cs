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
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreCommand
    {
        private readonly RestoreCollectorLogger _logger;

        private readonly RestoreRequest _request;

        private bool _success = true;

        private readonly Dictionary<NuGetFramework, RuntimeGraph> _runtimeGraphCache = new Dictionary<NuGetFramework, RuntimeGraph>();

        private readonly ConcurrentDictionary<PackageIdentity, RuntimeGraph> _runtimeGraphCacheByPackage
            = new ConcurrentDictionary<PackageIdentity, RuntimeGraph>(PackageIdentity.Comparer);

        private readonly Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> _includeFlagGraphs
            = new Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>>();

        public RestoreCommand(RestoreRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));

            // Validate the lock file version requested
            if (_request.LockFileVersion < 1 || _request.LockFileVersion > LockFileFormat.Version)
            {
                Debug.Fail($"Lock file version {_request.LockFileVersion} is not supported.");
                throw new ArgumentOutOfRangeException(nameof(_request.LockFileVersion));
            }

            var collectorLoggerHideWarningsAndErrors = request.Project.RestoreSettings.HideWarningsAndErrors
                || request.HideWarningsAndErrors;

            var collectorLogger = new RestoreCollectorLogger(_request.Log, collectorLoggerHideWarningsAndErrors)
            {
                ProjectPath = _request.Project.RestoreMetadata.ProjectPath,
                WarningPropertiesCollection =  new WarningPropertiesCollection()
                {
                    ProjectWideWarningProperties = request.Project?.RestoreMetadata?.ProjectWideWarningProperties,
                    PackageSpecificWarningProperties = WarningPropertiesCollection.GetPackageSpecificWarningProperties(request.Project)
                }
            };

            _logger = collectorLogger;
        }

        public Task<RestoreResult> ExecuteAsync()
        {
            return ExecuteAsync(CancellationToken.None);
        }

        public async Task<RestoreResult> ExecuteAsync(CancellationToken token)
        {
            var restoreTime = Stopwatch.StartNew();

            // Local package folders (non-sources)
            var localRepositories = new List<NuGetv3LocalRepository>
            {
                _request.DependencyProviders.GlobalPackages
            };

            localRepositories.AddRange(_request.DependencyProviders.FallbackPackageFolders);
            
            var contextForProject = CreateRemoteWalkContext(_request, _logger);

            CacheFile cacheFile = null;
            if (NoOpRestoreUtilities.IsNoOpSupported(_request)) {
                var cacheFileAndStatus = EvaluateCacheFile();
                cacheFile = cacheFileAndStatus.Key;
                if (cacheFileAndStatus.Value)
                {
                    if (NoOpRestoreUtilities.VerifyAssetsAndMSBuildFilesAndPackagesArePresent(_request))
                    {
                        restoreTime.Stop();

                        return new NoOpRestoreResult(
                            _success,
                            _request.ExistingLockFile,
                            _request.ExistingLockFile,
                            _request.ExistingLockFile.Path,
                            cacheFile,
                            _request.Project.RestoreMetadata.CacheFilePath,
                            _request.ProjectStyle,
                            restoreTime.Elapsed);
                    }
                }
            }

            // Restore
            var graphs = await ExecuteRestoreAsync(
                _request.DependencyProviders.GlobalPackages,
                _request.DependencyProviders.FallbackPackageFolders,
                contextForProject,
                token);

            // Create assets file
            var assetsFile = BuildAssetsFile(
                _request.ExistingLockFile,
                _request.Project,
                graphs,
                localRepositories,
                contextForProject);

            _success &= await ValidateRestoreGraphsAsync(graphs, _logger);

            // Check package compatibility
            var checkResults = VerifyCompatibility(
                _request.Project,
                _includeFlagGraphs,
                localRepositories,
                assetsFile,
                graphs,
                _request.ValidateRuntimeAssets,
                _logger);

            if (checkResults.Any(r => !r.Success))
            {
                _success = false;
            }


            // Determine the lock file output path
            var assetsFilePath = GetAssetsFilePath(assetsFile);
            // Determine the cache file output path
            var cacheFilePath = NoOpRestoreUtilities.GetCacheFilePath(_request);

            // Tool restores are unique since the output path is not known until after restore
            if (_request.LockFilePath == null
                && _request.ProjectStyle == ProjectStyle.DotnetCliTool)
            {
                _request.LockFilePath = assetsFilePath;
            }

            // Generate Targets/Props files
            var msbuildOutputFiles = Enumerable.Empty<MSBuildOutputFile>();

            if (contextForProject.IsMsBuildBased)
            {
                msbuildOutputFiles = BuildAssetsUtils.GetMSBuildOutputFiles(
                    _request.Project,
                    assetsFile,
                    graphs,
                    localRepositories,
                    _request,
                    assetsFilePath,
                    _success,
                    _logger);
            }

            // If the request is for a lower lock file version, downgrade it appropriately
            DowngradeLockFileIfNeeded(assetsFile);

            // Revert to the original case if needed
            await FixCaseForLegacyReaders(graphs, assetsFile, token);

            // Write the logs into the assets file
            var logs = _logger.Errors
                .Select(l => AssetsLogMessage.Create(l))
                .ToList();

            _success &= !logs.Any(l => l.Level == LogLevel.Error);

            assetsFile.LogMessages = logs;

            if (cacheFile != null)
            {
                cacheFile.Success = _success;
            }

            restoreTime.Stop();
            // Create result
            return new RestoreResult(
                _success,
                graphs,
                checkResults,
                msbuildOutputFiles,
                assetsFile,
                _request.ExistingLockFile,
                assetsFilePath,
                cacheFile,
                cacheFilePath,
                _request.ProjectStyle,
                restoreTime.Elapsed);
        }

        private KeyValuePair<CacheFile,bool> EvaluateCacheFile()
        {
            CacheFile cacheFile;
            var newDgSpecHash = _request.DependencyGraphSpec.GetHash();
            var noOp = false;
            if (_request.AllowNoOp && File.Exists(_request.Project.RestoreMetadata.CacheFilePath))
            {
                cacheFile = CacheFileFormat.Load(_request.Project.RestoreMetadata.CacheFilePath, _logger);

                if (cacheFile.IsValid && StringComparer.Ordinal.Equals(cacheFile.DgSpecHash, newDgSpecHash))
                {
                    _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoreNoOpFinish, _request.Project.Name));
                    _success = true;
                    noOp = true;
                }
                else
                {
                    cacheFile = new CacheFile(newDgSpecHash);
                    _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoreNoOpDGChanged, _request.Project.Name));
                }
            }
            else
            {
                cacheFile = new CacheFile(newDgSpecHash);

            }
            return new KeyValuePair<CacheFile,bool>(cacheFile, noOp) ;
        }

        private string GetAssetsFilePath(LockFile lockFile)
        {
            var projectLockFilePath = _request.LockFilePath;

            if (string.IsNullOrEmpty(projectLockFilePath))
            {
                if (_request.ProjectStyle == ProjectStyle.PackageReference
                    || _request.ProjectStyle == ProjectStyle.Standalone)
                {
                    projectLockFilePath = Path.Combine(_request.RestoreOutputPath, LockFileFormat.AssetsFileName);
                }
                else if (_request.ProjectStyle == ProjectStyle.DotnetCliTool)
                {
                    var toolName = ToolRestoreUtility.GetToolIdOrNullFromSpec(_request.Project);
                    var lockFileLibrary = ToolRestoreUtility.GetToolTargetLibrary(lockFile, toolName);

                    if (lockFileLibrary != null)
                    {
                        var version = lockFileLibrary.Version;

                        var toolPathResolver = new ToolPathResolver(_request.PackagesDirectory);
                        projectLockFilePath = toolPathResolver.GetLockFilePath(
                            toolName,
                            version,
                            lockFile.Targets.First().TargetFramework);
                    }
                }
                else
                {
                    projectLockFilePath = Path.Combine(_request.Project.BaseDirectory, LockFileFormat.LockFileName);
                }
            }

            return Path.GetFullPath(projectLockFilePath);
        }

        private void DowngradeLockFileIfNeeded(LockFile lockFile)
        {
            if (_request.LockFileVersion <= 2)
            {
                DowngradeLockFileToV2(lockFile);
            }

            if (_request.LockFileVersion <= 1)
            {
                DowngradeLockFileToV1(lockFile);
            }
        }

        private async Task FixCaseForLegacyReaders(
            IEnumerable<RestoreTargetGraph> graphs,
            LockFile lockFile,
            CancellationToken token)
        {
            // The main restore operation restores packages with lowercase ID and version. If the
            // restore request is for lowercase packages, then take this additional post-processing
            // step.
            if (!_request.IsLowercasePackagesDirectory)
            {
                var originalCase = new OriginalCaseGlobalPackageFolder(_request);

                // Convert the case of all the packages used in the project restore
                await originalCase.CopyPackagesToOriginalCaseAsync(graphs, token);

                // Convert the project lock file contents.
                originalCase.ConvertLockFileToOriginalCase(lockFile);
            }
        }

        private LockFile BuildAssetsFile(
            LockFile existingLockFile,
            PackageSpec project,
            IEnumerable<RestoreTargetGraph> graphs,
            IReadOnlyList<NuGetv3LocalRepository> localRepositories,
            RemoteWalkContext contextForProject)
        {
            // Build the lock file
            var lockFile = new LockFileBuilder(_request.LockFileVersion, _logger, _includeFlagGraphs)
                    .CreateLockFile(
                        existingLockFile,
                        project,
                        graphs,
                        localRepositories,
                        contextForProject);

            return lockFile;
        }

        /// <summary>
        /// Check if the given graphs are valid and log errors/warnings.
        /// If fatal errors are encountered the rest of the errors/warnings
        /// are not logged. This is to avoid flooding the log with long 
        /// dependency chains for every package.
        /// </summary>
        private static async Task<bool> ValidateRestoreGraphsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            // Check for cycles
            var success = await ValidateCyclesAsync(graphs, logger);

            if (success)
            {
                // Check for conflicts if no cycles existed
                success = await ValidateConflictsAsync(graphs, logger);
            }

            if (success)
            {
                // Log downgrades if everything else was successful
                await LogDowngradeWarningsAsync(graphs, logger);
            }

            return success;
        }

        /// <summary>
        /// Logs an error and returns false if any cycles exist.
        /// </summary>
        private static async Task<bool> ValidateCyclesAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            foreach (var graph in graphs)
            {
                foreach (var cycle in graph.AnalyzeResult.Cycles)
                {
                    var text = Strings.Log_CycleDetected + $" {Environment.NewLine}  {cycle.GetPath()}.";
                    await logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1606, text, cycle.Key?.Name, graph.TargetGraphName));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Logs an error and returns false if any conflicts exist.
        /// </summary>
        private static async Task<bool> ValidateConflictsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            foreach (var graph in graphs)
            {
                foreach (var versionConflict in graph.AnalyzeResult.VersionConflicts)
                {
                    var message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Log_VersionConflict,
                            versionConflict.Selected.Key.Name)
                        + $" {Environment.NewLine} {versionConflict.Selected.GetPath()} {Environment.NewLine} {versionConflict.Conflicting.GetPath()}.";

                    await logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1607, message, versionConflict.Selected.Key.Name, graph.TargetGraphName));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Log downgrade warnings from the graphs.
        /// </summary>
        private static Task LogDowngradeWarningsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            var messages = new List<RestoreLogMessage>();

            foreach (var graph in graphs)
            {
                if (graph.AnalyzeResult.Downgrades.Count > 0)
                {
                    // Find all dependencies in the flattened graph that are not packages.
                    var ignoreIds = new HashSet<string>(
                            graph.Flattened.Where(e => e.Key.Type != LibraryType.Package)
                                       .Select(e => e.Key.Name),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var downgrade in graph.AnalyzeResult.Downgrades)
                    {
                        var downgraded = downgrade.DowngradedFrom;
                        var downgradedBy = downgrade.DowngradedTo;

                        // Filter out non-package dependencies
                        if (!ignoreIds.Contains(downgraded.Key.Name))
                        {
                            // Not all dependencies have a min version, if one does not exist use 0.0.0
                            var fromVersion = downgraded.Key.VersionRange.MinVersion ?? new NuGetVersion(0, 0, 0);
                            var toVersion = downgradedBy.Key.VersionRange.MinVersion ?? new NuGetVersion(0, 0, 0);

                            var message = string.Format(
                                    CultureInfo.CurrentCulture,
                                    Strings.Log_DowngradeWarning,
                                    downgraded.Key.Name,
                                    fromVersion,
                                    toVersion)
                                + $" {Environment.NewLine} {downgraded.GetPath()} {Environment.NewLine} {downgradedBy.GetPath()}";

                            messages.Add(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1605, message, downgraded.Key.Name, graph.TargetGraphName));
                        }
                    }
                }
            }

            // Merge and log messages
            var mergedMessages = DiagnosticUtility.MergeOnTargetGraph(messages);
            return logger.LogMessagesAsync(mergedMessages);
        }

        private static IList<CompatibilityCheckResult> VerifyCompatibility(
                PackageSpec project,
                Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs,
                IReadOnlyList<NuGetv3LocalRepository> localRepositories,
                LockFile lockFile,
                IEnumerable<RestoreTargetGraph> graphs,
                bool validateRuntimeAssets,
                ILogger logger)
        {
            // Scan every graph for compatibility, as long as there were no unresolved packages
            var checkResults = new List<CompatibilityCheckResult>();
            if (graphs.All(g => !g.Unresolved.Any()))
            {
                var checker = new CompatibilityChecker(localRepositories, lockFile, validateRuntimeAssets, logger);
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
                            logger.LogDebug($"Incompatible projects: {projectCount}");
                        }

                        if (packageCount > 0)
                        {
                            logger.LogDebug($"Incompatible packages: {packageCount}");
                        }
                    }
                }
            }

            return checkResults;
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteRestoreAsync(
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            RemoteWalkContext context,
            CancellationToken token)
        {
            if (_request.Project.TargetFrameworks.Count == 0)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ProjectDoesNotSpecifyTargetFrameworks, _request.Project.Name, _request.Project.FilePath);
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1001, message));

                _success = false;
                return Enumerable.Empty<RestoreTargetGraph>();
            }

            _logger.LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, _request.Project.FilePath));

            // Get external project references
            // If the top level project already exists, update the package spec provided
            // with the RestoreRequest spec.
            var updatedExternalProjects = GetProjectReferences(context);

            // Determine if the targets and props files should be written out.
            context.IsMsBuildBased = _request.ProjectStyle != ProjectStyle.DotnetCliTool;

            // Load repositories
            // the external project provider is specific to the current restore project
            context.ProjectLibraryProviders.Add(
                    new PackageSpecReferenceDependencyProvider(updatedExternalProjects, _logger));

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
                _runtimeGraphCacheByPackage,
                _logger);

            var projectRestoreCommand = new ProjectRestoreCommand(projectRestoreRequest);

            var result = await projectRestoreCommand.TryRestore(
                projectRange,
                projectFrameworkRuntimePairs,
                allInstalledPackages,
                userPackageFolder,
                fallbackPackageFolders,
                remoteWalker,
                context,
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
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_UnknownCompatibilityProfile, profile.Key);

                    await _logger.LogAsync(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1502, message));
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
                var compatibilityResult = await projectRestoreCommand.TryRestore(
                    projectRange,
                    _request.CompatibilityProfiles,
                    allInstalledPackages,
                    userPackageFolder,
                    fallbackPackageFolders,
                    remoteWalker,
                    context,
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

        private List<ExternalProjectReference> GetProjectReferences(RemoteWalkContext context)
        {
            // External references
            var updatedExternalProjects = new List<ExternalProjectReference>();

            if (_request.ExternalProjects.Count == 0)
            {
                // If no projects exist add the current project.json file to the project
                // list so that it can be resolved.
                updatedExternalProjects.Add(ToExternalProjectReference(_request.Project));
            }
            else if (_request.ExternalProjects.Count > 0)
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
                    updatedExternalProjects.AddRange(_request.ExternalProjects
                        .Where(project =>
                            !project.UniqueName.Equals(rootProject.UniqueName, StringComparison.Ordinal)));

                    var updatedReference = new ExternalProjectReference(
                        rootProject.UniqueName,
                        _request.Project,
                        rootProject.MSBuildProjectPath,
                        rootProject.ExternalProjectReferences);

                    updatedExternalProjects.Add(updatedReference);
                }
            }
            else
            {
                // External references were passed, but the top level project wasn't found.
                // This is always due to an internal issue and typically caused by errors 
                // building the project closure.
                Debug.Fail("RestoreRequest.ExternalProjects contains references, but does not contain the top level references. Add the project we are restoring for.");
                throw new InvalidOperationException($"Missing external reference metadata for {_request.Project.Name}");
            }

            return updatedExternalProjects;
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

        private static RemoteWalkContext CreateRemoteWalkContext(RestoreRequest request, RestoreCollectorLogger logger)
        {
            var context = new RemoteWalkContext(
                request.CacheContext,
                logger);

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

        private void DowngradeLockFileToV2(LockFile lockFile)
        {
            // noop
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

            // Remove the package spec
            lockFile.PackageSpec = null;
        }

        private static ExternalProjectReference ToExternalProjectReference(PackageSpec project)
        {
            return new ExternalProjectReference(
                project.Name,
                project,
                msbuildProjectPath: null,
                projectReferences: Enumerable.Empty<string>());
        }
    }
}
