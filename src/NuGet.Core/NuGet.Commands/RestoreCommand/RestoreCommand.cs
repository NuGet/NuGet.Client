// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
using NuGet.RuntimeModel;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreCommand
    {
        private readonly RestoreCollectorLogger _logger;

        private readonly RestoreRequest _request;

        private bool _success = true;

        private Guid _operationId;

        private readonly Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> _includeFlagGraphs
            = new Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>>();

        public Guid ParentId { get; }

        // names for ProjectRestoreInformation and intervals
        private const string ProjectRestoreInformation = "ProjectRestoreInformation";
        private const string ErrorCodes = "ErrorCodes";
        private const string WarningCodes = "WarningCodes";
        private const string RestoreSuccess = "RestoreSuccess";

        // names for child events for ProjectRestoreInformation
        private const string GenerateRestoreGraph = "GenerateRestoreGraph";
        private const string GenerateAssetsFile = "GenerateAssetsFile";
        private const string ValidateRestoreGraphs = "ValidateRestoreGraphs";
        private const string CreateRestoreResult = "CreateRestoreResult";
        private const string RestoreNoOpInformation = "RestoreNoOpInformation";
        private const string RestoreLockFileInformation = "RestoreLockFileInformation";
        private const string ValidatePackagesSha = "ValidatePackagesSha";

        // names for intervals in RestoreNoOpInformation
        private const string CacheFileEvaluateDuration = "CacheFileEvaluateDuration";
        private const string MsbuildAssetsVerificationDuration = "MsbuildAssetsVerificationDuration";
        private const string MsbuildAssetsVerificationResult = "MsbuildAssetsVerificationResult";
        private const string ReplayLogsDuration = "ReplayLogsDuration";

        //names for child events for GenerateRestoreGraph
        private const string CreateRestoreTargetGraph = "CreateRestoreTargetGraph";
        private const string RestoreAdditionalCompatCheck = "RestoreAdditionalCompatCheck";

        // names for intervals in RestoreLockFileInformation
        private const string IsLockFileEnabled = "IsLockFileEnabled";
        private const string ReadLockFileDuration = "ReadLockFileDuration";
        private const string ValidateLockFileDuration = "ValidateLockFileDuration";
        private const string IsLockFileValidForRestore = "IsLockFileValidForRestore";
        private const string LockFileEvaluationResult = "LockFileEvaluationResult";

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

            var collectorLogger = new RestoreCollectorLogger(_request.Log, collectorLoggerHideWarningsAndErrors);

            collectorLogger.ApplyRestoreInputs(_request.Project);

            _logger = collectorLogger;
            ParentId = request.ParentId;
        }

        public Task<RestoreResult> ExecuteAsync()
        {
            return ExecuteAsync(CancellationToken.None);
        }

        public async Task<RestoreResult> ExecuteAsync(CancellationToken token)
        {
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: ParentId, eventName: ProjectRestoreInformation))
            {
                _operationId = telemetry.OperationId;
                var restoreTime = Stopwatch.StartNew();

                // Local package folders (non-sources)
                var localRepositories = new List<NuGetv3LocalRepository>
                {
                    _request.DependencyProviders.GlobalPackages
                };

                localRepositories.AddRange(_request.DependencyProviders.FallbackPackageFolders);

                var contextForProject = CreateRemoteWalkContext(_request, _logger);

                CacheFile cacheFile = null;

                using (var noOpTelemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: _operationId, eventName: RestoreNoOpInformation))
                {
                    if (NoOpRestoreUtilities.IsNoOpSupported(_request))
                    {
                        noOpTelemetry.StartIntervalMeasure();

                        var cacheFileAndStatus = EvaluateCacheFile();

                        noOpTelemetry.EndIntervalMeasure(CacheFileEvaluateDuration);

                        cacheFile = cacheFileAndStatus.Key;
                        if (cacheFileAndStatus.Value)
                        {
                            noOpTelemetry.StartIntervalMeasure();

                            var noOpSuccess = NoOpRestoreUtilities.VerifyAssetsAndMSBuildFilesAndPackagesArePresent(_request);

                            noOpTelemetry.EndIntervalMeasure(MsbuildAssetsVerificationDuration);
                            noOpTelemetry.TelemetryEvent[MsbuildAssetsVerificationResult] = noOpSuccess;

                            if (noOpSuccess)
                            {
                                noOpTelemetry.StartIntervalMeasure();

                                // Replay Warnings and Errors from an existing lock file in case of a no-op.
                                await MSBuildRestoreUtility.ReplayWarningsAndErrorsAsync(_request.ExistingLockFile, _logger);

                                noOpTelemetry.EndIntervalMeasure(ReplayLogsDuration);

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
                }

                // evaluate packages.lock.json file
                var packagesLockFilePath = PackagesLockFileUtilities.GetNuGetLockFilePath(_request.Project);
                var isLockFileValid = false;
                PackagesLockFile packagesLockFile = null;

                using (var lockFileTelemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: _operationId, eventName: RestoreLockFileInformation))
                {
                    lockFileTelemetry.TelemetryEvent[IsLockFileEnabled] = PackagesLockFileUtilities.IsNuGetLockFileSupported(_request.Project);

                    var packagesLockFileResult = await EvaluatePackagesLockFileAsync(packagesLockFilePath, contextForProject, lockFileTelemetry);

                    // result of packages.lock.json file evaluation where
                    // Item1 is the status of evaluating packages lock file if false, then bail restore
                    // Item2 is also a tuple which has 2 parts -
                    //      Item1 tells whether lock file is still valid to be consumed for this restore
                    //      Item2 is the PackagesLockFile instance
                    var result = packagesLockFileResult.Item1;
                    isLockFileValid = packagesLockFileResult.Item2.Item1;
                    packagesLockFile = packagesLockFileResult.Item2.Item2;

                    lockFileTelemetry.TelemetryEvent[IsLockFileValidForRestore] = isLockFileValid;
                    lockFileTelemetry.TelemetryEvent[LockFileEvaluationResult] = result;

                    if (!result)
                    {
                        _success = result;

                        // Replay Warnings and Errors from an existing lock file
                        await MSBuildRestoreUtility.ReplayWarningsAndErrorsAsync(_request.ExistingLockFile, _logger);

                        if (cacheFile != null)
                        {
                            cacheFile.Success = _success;
                        }

                        return new RestoreResult(
                            success: _success,
                            restoreGraphs: new List<RestoreTargetGraph>(),
                            compatibilityCheckResults: new List<CompatibilityCheckResult>(),
                            msbuildFiles: new List<MSBuildOutputFile>(),
                            lockFile: _request.ExistingLockFile,
                            previousLockFile: _request.ExistingLockFile,
                            lockFilePath: _request.ExistingLockFile?.Path,
                            cacheFile: cacheFile,
                            cacheFilePath: _request.Project.RestoreMetadata.CacheFilePath,
                            packagesLockFilePath: packagesLockFilePath,
                            packagesLockFile: packagesLockFile,
                            projectStyle: _request.ProjectStyle,
                            elapsedTime: restoreTime.Elapsed);
                    }
                }

                IEnumerable<RestoreTargetGraph> graphs = null;
                using (var restoreGraphTelemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: _operationId, eventName: GenerateRestoreGraph))
                {
                    // Restore
                    graphs = await ExecuteRestoreAsync(
                    _request.DependencyProviders.GlobalPackages,
                    _request.DependencyProviders.FallbackPackageFolders,
                    contextForProject,
                    token,
                    restoreGraphTelemetry);
                }

                LockFile assetsFile = null;
                using (TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: _operationId, eventName: GenerateAssetsFile))
                {
                    // Create assets file
                    assetsFile = BuildAssetsFile(
                    _request.ExistingLockFile,
                    _request.Project,
                    graphs,
                    localRepositories,
                    contextForProject);
                }

                IList<CompatibilityCheckResult> checkResults = null;
                using (TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: _operationId, eventName: ValidateRestoreGraphs))
                {
                    _success &= await ValidateRestoreGraphsAsync(graphs, _logger);

                    // Check package compatibility
                    checkResults = await VerifyCompatibilityAsync(
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

                }

                // Generate Targets/Props files
                var msbuildOutputFiles = Enumerable.Empty<MSBuildOutputFile>();
                string assetsFilePath = null;
                string cacheFilePath = null;
                using (TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: _operationId, eventName: CreateRestoreResult))
                {
                    // Determine the lock file output path
                    assetsFilePath = GetAssetsFilePath(assetsFile);

                    // Determine the cache file output path
                    cacheFilePath = NoOpRestoreUtilities.GetCacheFilePath(_request, assetsFile);

                    // Tool restores are unique since the output path is not known until after restore
                    if (_request.LockFilePath == null
                        && _request.ProjectStyle == ProjectStyle.DotnetCliTool)
                    {
                        _request.LockFilePath = assetsFilePath;
                    }

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

                    // if lock file was still valid then validate package's sha512 hash or else write
                    // the file if enabled.
                    if (isLockFileValid)
                    {
                        using (TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(parentId: _operationId, eventName: ValidatePackagesSha))
                        {
                            // validate package's SHA512
                            _success &= ValidatePackagesSha512(packagesLockFile, assetsFile);
                        }

                        // clear out the existing lock file so that we don't over-write the same file
                        packagesLockFile = null;
                    }
                    else if (PackagesLockFileUtilities.IsNuGetLockFileSupported(_request.Project))
                    {
                        // generate packages.lock.json file if enabled
                        packagesLockFile = new PackagesLockFileBuilder()
                            .CreateNuGetLockFile(assetsFile);
                    }

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

                    var errorCodes = ConcatAsString(new HashSet<NuGetLogCode>(logs.Where(l => l.Level == LogLevel.Error).Select(l => l.Code)));
                    var warningCodes = ConcatAsString(new HashSet<NuGetLogCode>(logs.Where(l => l.Level == LogLevel.Warning).Select(l => l.Code)));

                    if (!string.IsNullOrEmpty(errorCodes))
                    {
                        telemetry.TelemetryEvent[ErrorCodes] = errorCodes;
                    }

                    if (!string.IsNullOrEmpty(warningCodes))
                    {
                        telemetry.TelemetryEvent[WarningCodes] = warningCodes;
                    }

                    telemetry.TelemetryEvent[RestoreSuccess] = _success;
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
                    packagesLockFilePath,
                    packagesLockFile,
                    _request.ProjectStyle,
                    restoreTime.Elapsed);
            }
        }

        private string ConcatAsString<T>(IEnumerable<T> enumerable)
        {
            string result = null;

            if (enumerable != null && enumerable.Any())
            {
                var builder = new StringBuilder();
                foreach (var entry in enumerable)
                {
                    builder.Append(entry.ToString());
                    builder.Append(";");
                }

                result = builder.ToString(0, builder.Length - 1);
            }

            return result;
        }

        /// <summary>
        /// Accounts for using the restore commands on 2 projects living in the same path
        /// </summary>
        private bool VerifyAssetsFileMatchesProject()
        {
            if (_request.Project.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool)
            {
                return true;
            }
            var pathComparer = PathUtility.GetStringComparerBasedOnOS();
            return (_request.ExistingLockFile != null && pathComparer.Equals( _request.ExistingLockFile.PackageSpec.FilePath , _request.Project.FilePath));
        }

        private bool ValidatePackagesSha512(PackagesLockFile lockFile, LockFile assetsFile)
        {
            var librariesLookUp = lockFile.Targets
                .SelectMany(t => t.Dependencies.Where(dep => dep.Type != PackageDependencyType.Project))
                .Distinct(new LockFileDependencyIdVersionComparer())
                .ToDictionary(dep => new PackageIdentity(dep.Id, dep.ResolvedVersion), val => val.Sha512);

            foreach (var library in assetsFile.Libraries.Where(lib => lib.Type == LibraryType.Package))
            {
                var package = new PackageIdentity(library.Name, library.Version);

                if (!librariesLookUp.TryGetValue(package, out var sha512) || sha512 != library.Sha512)
                {
                    // raise validation error
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageValidationFailed, package.ToString());
                    _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1403, message));

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evaluate packages.lock.json file if available and accordingly return result.
        /// </summary>
        /// <param name="packagesLockFilePath"></param>
        /// <param name="contextForProject"></param>
        /// <returns>result of packages.lock.json file evaluation where
        /// Item1 is the status of evaluating packages lock file if false, then bail restore
        /// Item2 is also a tuple which has 2 parts -
        ///     Item1 tells whether lock file is still valid to be consumed for this restore
        ///     Item2 is the PackagesLockFile instance
        /// </returns>
        private async Task<Tuple<bool, Tuple<bool, PackagesLockFile>>> EvaluatePackagesLockFileAsync(
            string packagesLockFilePath,
            RemoteWalkContext contextForProject,
            TelemetryActivity lockFileTelemetry)
        {
            PackagesLockFile packagesLockFile = null;
            var isLockFileValid = false;
            var success = true;

            var restorePackagesWithLockFile = _request.Project.RestoreMetadata?.RestoreLockProperties.RestorePackagesWithLockFile;

            if (!MSBuildStringUtility.IsTrueOrEmpty(restorePackagesWithLockFile) && File.Exists(packagesLockFilePath))
            {
                success = false;

                // invalid input since packages.lock.json file exists along with RestorePackagesWithLockFile is set to false.
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Error_InvalidLockFileInput, packagesLockFilePath);
                await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1005, message));

                return Tuple.Create(success, Tuple.Create(isLockFileValid, packagesLockFile));
            }

            // read packages.lock.json file if exists and ReevaluateRestoreGraph flag is not set to true
            if (!_request.ReevaluateRestoreGraph && File.Exists(packagesLockFilePath))
            {
                lockFileTelemetry.StartIntervalMeasure();
                packagesLockFile = PackagesLockFileFormat.Read(packagesLockFilePath, _logger);
                lockFileTelemetry.EndIntervalMeasure(ReadLockFileDuration);

                if (_request.DependencyGraphSpec != null && packagesLockFile.Targets.Count > 0)
                {
                    // check if lock file is out of sync with project data
                    lockFileTelemetry.StartIntervalMeasure();
                    isLockFileValid = PackagesLockFileUtilities.IsLockFileStillValid(_request.DependencyGraphSpec, packagesLockFile);
                    lockFileTelemetry.EndIntervalMeasure(ValidateLockFileDuration);

                    if (isLockFileValid)
                    {
                        // pass lock file details down to generate restore graph
                        foreach (var target in packagesLockFile.Targets)
                        {
                            var libraries = target.Dependencies
                                .Where(dep => dep.Type != PackageDependencyType.Project)
                                .Select(dep => new LibraryIdentity(dep.Id, dep.ResolvedVersion, LibraryType.Package))
                                .ToList();

                            // add lock file libraries into RemoteWalkContext so that it can be used during restore graph generation
                            contextForProject.LockFileLibraries.Add(new LockFileCacheKey(target.TargetFramework, target.RuntimeIdentifier), libraries);
                        }
                    }
                    else if (_request.IsRestoreOriginalAction && _request.Project.RestoreMetadata.RestoreLockProperties.RestoreLockedMode)
                    {
                        success = false;

                        // bail restore since it's the locked mode but required to update the lock file.
                        await _logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1004, Strings.Error_RestoreInLockedMode));
                    }
                }
            }

            return Tuple.Create(success, Tuple.Create(isLockFileValid, packagesLockFile));
        }

        private KeyValuePair<CacheFile, bool> EvaluateCacheFile()
        {
            CacheFile cacheFile;
            var noOp = false;

            var newDgSpecHash = NoOpRestoreUtilities.GetHash(_request);

            if (_request.ProjectStyle == ProjectStyle.DotnetCliTool && _request.AllowNoOp)
            {
                // No need to attempt to resolve the tool if no-op is not allowed.
                NoOpRestoreUtilities.UpdateRequestBestMatchingToolPathsIfAvailable(_request);
            }

            // if --reevaluate flag is passed then restore noop check will also be skipped.
            // this will also help us to get rid of -force flag in near future.
            if (_request.AllowNoOp &&
                !_request.ReevaluateRestoreGraph &&
                File.Exists(_request.Project.RestoreMetadata.CacheFilePath))
            {
                cacheFile = FileUtility.SafeRead(_request.Project.RestoreMetadata.CacheFilePath, (stream, path) => CacheFileFormat.Read(stream, _logger, path));

                if (cacheFile.IsValid && StringComparer.Ordinal.Equals(cacheFile.DgSpecHash, newDgSpecHash) && VerifyAssetsFileMatchesProject())
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

            if (_request.ProjectStyle == ProjectStyle.DotnetCliTool)
            {
                if (noOp) // Only if the hash matches, then load the lock file. This is a performance hit, so we need to delay it as much as possible.
                {
                    _request.ExistingLockFile = LockFileUtilities.GetLockFile(_request.LockFilePath, _logger);
                }
                else
                {
                    // Clean up to preserve the pre no-op behavior. This should not be used, but we want to be cautious. 
                    _request.LockFilePath = null;
                    _request.Project.RestoreMetadata.CacheFilePath = null;
                }
            }
            return new KeyValuePair<CacheFile, bool>(cacheFile, noOp);
        }

        private string GetAssetsFilePath(LockFile lockFile)
        {
            var projectLockFilePath = _request.LockFilePath;

            if (string.IsNullOrEmpty(projectLockFilePath))
            {
                if (_request.ProjectStyle == ProjectStyle.PackageReference
                    || _request.ProjectStyle == ProjectStyle.DotnetToolReference
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
                var originalCase = new OriginalCaseGlobalPackageFolder(_request, _operationId);

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
        private async Task<bool> ValidateRestoreGraphsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
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
                    await logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1108, text, cycle.Key?.Name, graph.TargetGraphName));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Logs an error and returns false if any conflicts exist.
        /// </summary>
        private async Task<bool> ValidateConflictsAsync(IEnumerable<RestoreTargetGraph> graphs, ILogger logger)
        {
            foreach (var graph in graphs)
            {
                foreach (var versionConflict in graph.AnalyzeResult.VersionConflicts)
                {
                    var message = string.Format(
                           CultureInfo.CurrentCulture,
                           Strings.Log_VersionConflict,
                           versionConflict.Selected.Key.Name,
                           versionConflict.Selected.GetIdAndVersionOrRange(),
                           _request.Project.Name)
                       + $" {Environment.NewLine} {versionConflict.Selected.GetPathWithLastRange()} {Environment.NewLine} {versionConflict.Conflicting.GetPathWithLastRange()}.";

                    await logger.LogAsync(RestoreLogMessage.CreateError(NuGetLogCode.NU1107, message, versionConflict.Selected.Key.Name, graph.TargetGraphName));
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
                            var fromVersion = downgraded.GetVersionRange().MinVersion
                                            ?? new NuGetVersion(0, 0, 0);

                            // Use the actual version resolved if it exists
                            var toVersion = downgradedBy.GetVersionOrDefault()
                                            ?? downgradedBy.GetVersionRange().MinVersion
                                            ?? new NuGetVersion(0, 0, 0);

                            var message = string.Format(
                                    CultureInfo.CurrentCulture,
                                    Strings.Log_DowngradeWarning,
                                    downgraded.Key.Name,
                                    fromVersion,
                                    toVersion)
                                + $" {Environment.NewLine} {downgraded.GetPathWithLastRange()} {Environment.NewLine} {downgradedBy.GetPathWithLastRange()}";

                            messages.Add(RestoreLogMessage.CreateWarning(NuGetLogCode.NU1605, message, downgraded.Key.Name, graph.TargetGraphName));
                        }
                    }
                }
            }

            // Merge and log messages
            var mergedMessages = DiagnosticUtility.MergeOnTargetGraph(messages);
            return logger.LogMessagesAsync(mergedMessages);
        }

        private static async Task<IList<CompatibilityCheckResult>> VerifyCompatibilityAsync(
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
                    // Don't do compat checks for the ridless graph of DotnetTooReference restore. Everything relevant will be caught in the graph with the rid
                    if (!(ProjectStyle.DotnetToolReference == project.RestoreMetadata?.ProjectStyle && string.IsNullOrEmpty(graph.RuntimeIdentifier)))
                    {
                        await logger.LogAsync(LogLevel.Verbose, string.Format(CultureInfo.CurrentCulture, Strings.Log_CheckingCompatibility, graph.Name));

                        var includeFlags = IncludeFlagUtils.FlattenDependencyTypes(includeFlagGraphs, project, graph);

                        var res = await checker.CheckAsync(graph, includeFlags, project);

                        checkResults.Add(res);
                        if (res.Success)
                        {
                            await logger.LogAsync(LogLevel.Verbose, string.Format(CultureInfo.CurrentCulture, Strings.Log_PackagesAndProjectsAreCompatible, graph.Name));
                        }
                        else
                        {
                            // Get error counts on a project vs package basis
                            var projectCount = res.Issues.Count(issue => issue.Type == CompatibilityIssueType.ProjectIncompatible);
                            var packageCount = res.Issues.Count(issue => issue.Type != CompatibilityIssueType.ProjectIncompatible);

                            // Log a summary with compatibility error counts
                            if (projectCount > 0)
                            {
                                await logger.LogAsync(LogLevel.Debug, $"Incompatible projects: {projectCount}");
                            }

                            if (packageCount > 0)
                            {
                                await logger.LogAsync(LogLevel.Debug, $"Incompatible packages: {packageCount}");
                            }
                        }
                    }
                    else
                    {
                        await logger.LogAsync(LogLevel.Verbose, string.Format(CultureInfo.CurrentCulture, Strings.Log_SkippingCompatibiilityCheckOnRidlessGraphForDotnetToolReferenceProject, graph.Name));
                    }
                }
            }

            return checkResults;
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteRestoreAsync(
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            RemoteWalkContext context,
            CancellationToken token,
            TelemetryActivity telemetryActivity)
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
            var allGraphs = new List<RestoreTargetGraph>();
            var runtimeIds = RequestRuntimeUtility.GetRestoreRuntimes(_request);
            var projectFrameworkRuntimePairs = CreateFrameworkRuntimePairs(_request.Project, runtimeIds);
            var hasSupports = _request.Project.RuntimeGraph.Supports.Count > 0;

            var projectRestoreRequest = new ProjectRestoreRequest(
                _request,
                _request.Project,
                _request.ExistingLockFile,
                _logger)
            {
                ParentId = _operationId
            };

            var projectRestoreCommand = new ProjectRestoreCommand(projectRestoreRequest);

            Tuple<bool, List<RestoreTargetGraph>, RuntimeGraph> result = null;
            using (var tryRestoreTelemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(telemetryActivity.OperationId, CreateRestoreTargetGraph))
            {
                result = await projectRestoreCommand.TryRestoreAsync(
                    projectRange,
                    projectFrameworkRuntimePairs,
                    userPackageFolder,
                    fallbackPackageFolders,
                    remoteWalker,
                    context,
                    forceRuntimeGraphCreation: hasSupports,
                    token: token,
                    telemetryActivity: tryRestoreTelemetry);
            }

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
                Tuple<bool, List<RestoreTargetGraph>, RuntimeGraph> compatibilityResult = null;
                using (var runtimeTryRestoreTelemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationIdAndEvent(telemetryActivity.OperationId, RestoreAdditionalCompatCheck))
                {
                    compatibilityResult = await projectRestoreCommand.TryRestoreAsync(
                    projectRange,
                    _request.CompatibilityProfiles,
                    userPackageFolder,
                    fallbackPackageFolders,
                    remoteWalker,
                    context,
                    forceRuntimeGraphCreation: true,
                    token: token,
                    telemetryActivity: runtimeTryRestoreTelemetry);

                }

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
