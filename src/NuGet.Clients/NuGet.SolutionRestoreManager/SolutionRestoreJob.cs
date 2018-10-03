// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Implementation of solution restore operation as executed by the <see cref="SolutionRestoreWorker"/>.
    /// Designed to be called only once during its lifetime.
    /// </summary>
    [Export(typeof(ISolutionRestoreJob))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal sealed class SolutionRestoreJob : ISolutionRestoreJob
    {
        private readonly IAsyncServiceProvider _asyncServiceProvider;
        private readonly IPackageRestoreManager _packageRestoreManager;
        private readonly IVsSolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly ISettings _settings;
        private readonly IRestoreEventsPublisher _restoreEventsPublisher;

        private RestoreOperationLogger _logger;
        private INuGetProjectContext _nuGetProjectContext;
        private PackageRestoreConsent _packageRestoreConsent;

        private NuGetOperationStatus _status;
        private int _packageCount;
        private int _noOpProjectsCount;

        // relevant to packages.config restore only
        private int _missingPackagesCount;
        private int _currentCount;

        [ImportingConstructor]
        public SolutionRestoreJob(
            IPackageRestoreManager packageRestoreManager,
            IVsSolutionManager solutionManager,
            ISourceRepositoryProvider sourceRepositoryProvider,
            IRestoreEventsPublisher restoreEventsPublisher,
            ISettings settings)
            : this(AsyncServiceProvider.GlobalProvider,
                  packageRestoreManager,
                  solutionManager,
                  sourceRepositoryProvider,
                  restoreEventsPublisher,
                  settings
                )
        { }

        public SolutionRestoreJob(
            IAsyncServiceProvider asyncServiceProvider,
            IPackageRestoreManager packageRestoreManager,
            IVsSolutionManager solutionManager,
            ISourceRepositoryProvider sourceRepositoryProvider,
            IRestoreEventsPublisher restoreEventsPublisher,
            ISettings settings)
        {
            Assumes.Present(asyncServiceProvider);
            Assumes.Present(packageRestoreManager);
            Assumes.Present(solutionManager);
            Assumes.Present(sourceRepositoryProvider);
            Assumes.Present(restoreEventsPublisher);
            Assumes.Present(settings);

            _asyncServiceProvider = asyncServiceProvider;
            _packageRestoreManager = packageRestoreManager;
            _solutionManager = solutionManager;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _restoreEventsPublisher = restoreEventsPublisher;
            _settings = settings;
            _packageRestoreConsent = new PackageRestoreConsent(_settings);
        }

        /// <summary>
        /// Restore job entry point. Not re-entrant.
        /// </summary>
        public async Task<bool> ExecuteAsync(
            SolutionRestoreRequest request,
            SolutionRestoreJobContext jobContext,
            RestoreOperationLogger logger,
            CancellationToken token)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (jobContext == null)
            {
                throw new ArgumentNullException(nameof(jobContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _logger = logger;

            // update instance attributes with the shared context values
            _nuGetProjectContext = jobContext.NuGetProjectContext;
            _nuGetProjectContext.OperationId = request.OperationId;

            using (var ctr1 = token.Register(() => _status = NuGetOperationStatus.Cancelled))
            {
                try
                {
                    await RestoreAsync(request.ForceRestore, request.RestoreSource, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception e)
                {
                    // Log the exception to the console and activity log
                    await _logger.LogExceptionAsync(e);
                }
            }

            return _status == NuGetOperationStatus.NoOp || _status == NuGetOperationStatus.Succeeded;
        }



        private async Task RestoreAsync(bool forceRestore, RestoreOperationSource restoreSource, CancellationToken token)
        {
            var startTime = DateTimeOffset.Now;
            _status = NuGetOperationStatus.NoOp;

            // start timer for telemetry event
            var stopWatch = Stopwatch.StartNew();
            var projects = Enumerable.Empty<NuGetProject>();

            _packageRestoreManager.PackageRestoredEvent += PackageRestoreManager_PackageRestored;
            _packageRestoreManager.PackageRestoreFailedEvent += PackageRestoreManager_PackageRestoreFailedEvent;

            try
            {
                var solutionDirectory = _solutionManager.SolutionDirectory;
                var isSolutionAvailable = await _solutionManager.IsSolutionAvailableAsync();

                // Get the projects from the SolutionManager
                // Note that projects that are not supported by NuGet, will not show up in this list
                projects = await _solutionManager.GetNuGetProjectsAsync();

                if (projects.Any() && solutionDirectory == null)
                {
                    await _logger.DoAsync((l, _) =>
                    {
                        _status = NuGetOperationStatus.Failed;
                        l.ShowError(Resources.SolutionIsNotSaved);
                        l.WriteLine(VerbosityLevel.Minimal, Resources.SolutionIsNotSaved);
                    });

                    return;
                }

                // Check if there are any projects that are not INuGetIntegratedProject, that is,
                // projects with packages.config. OR 
                // any of the deferred project is type of packages.config, If so, perform package restore on them
                if (projects.Any(project => !(project is INuGetIntegratedProject)))
                {
                    await RestorePackagesOrCheckForMissingPackagesAsync(
                        solutionDirectory,
                        isSolutionAvailable,
                        restoreSource,
                        token);
                }

                var dependencyGraphProjects = projects
                    .OfType<IDependencyGraphProject>()
                    .ToList();

                await RestorePackageSpecProjectsAsync(
                    dependencyGraphProjects,
                    forceRestore,
                    isSolutionAvailable,
                    restoreSource,
                    token);

#if !VS14
                // TODO: To limit risk, we only publish the event when there is a cross-platform PackageReference
                // project in the solution. Extending this behavior to all solutions is tracked here:
                // NuGet/Home#4478
                if (projects.OfType<NetCorePackageReferenceProject>().Any())
                {
                    _restoreEventsPublisher.OnSolutionRestoreCompleted(
                        new SolutionRestoredEventArgs(_status, solutionDirectory));
                }
#endif
            }
            finally
            {
                _packageRestoreManager.PackageRestoredEvent -= PackageRestoreManager_PackageRestored;
                _packageRestoreManager.PackageRestoreFailedEvent -= PackageRestoreManager_PackageRestoreFailedEvent;

                stopWatch.Stop();
                var duration = stopWatch.Elapsed;

                // Do not log any restore message if user disabled restore.
                if (_packageRestoreConsent.IsGranted)
                {
                    await _logger.WriteSummaryAsync(_status, duration);
                }
                else
                {
                    _logger.LogDebug(Resources.PackageRefNotRestoredBecauseOfNoConsent);
                }
                // Emit telemetry event for restore operation
                EmitRestoreTelemetryEvent(
                    projects,
                    restoreSource,
                    startTime,
                    duration.TotalSeconds);
            }
        }

        private void EmitRestoreTelemetryEvent(IEnumerable<NuGetProject> projects,
            RestoreOperationSource source,
            DateTimeOffset startTime,
            double duration)
        {
            var sortedProjects = projects.OrderBy(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.UniqueName));
            var projectIds = sortedProjects.Select(
                project => project.GetMetadata<string>(NuGetProjectMetadataKeys.ProjectId)).ToArray();

            var restoreTelemetryEvent = new RestoreTelemetryEvent(
                _nuGetProjectContext.OperationId.ToString(),
                projectIds,
                source,
                startTime,
                _status,
                _packageCount,
                _noOpProjectsCount,
                DateTimeOffset.Now,
                duration);

            TelemetryActivity.EmitTelemetryEvent(restoreTelemetryEvent);

            var sources = _sourceRepositoryProvider.PackageSourceProvider.LoadPackageSources().ToList();
            var sourceEvent = SourceTelemetry.GetSourceSummaryEvent(_nuGetProjectContext.OperationId, sources);

            TelemetryActivity.EmitTelemetryEvent(sourceEvent);
        }

        private async Task RestorePackageSpecProjectsAsync(
            List<IDependencyGraphProject> projects,
            bool forceRestore,
            bool isSolutionAvailable,
            RestoreOperationSource restoreSource,
            CancellationToken token)
        {
            // Only continue if there are some build integrated type projects.
            if (!(projects.Any(project => project is BuildIntegratedNuGetProject)))
            {
                return;
            }

            if (_packageRestoreConsent.IsGranted)
            {
                if (!isSolutionAvailable)
                {
                    var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(_settings);
                    if (!Path.IsPathRooted(globalPackagesFolder))
                    {
                        await _logger.DoAsync((l, _) =>
                        {
                            var message = string.Format(
                                CultureInfo.CurrentCulture,
                                Resources.RelativeGlobalPackagesFolder,
                                globalPackagesFolder);

                            l.WriteLine(VerbosityLevel.Quiet, message);
                        });

                        // Cannot restore packages since globalPackagesFolder is a relative path
                        // and the solution is not available
                        return;
                    }
                }

                // Cache p2ps discovered from DTE
                var cacheContext = new DependencyGraphCacheContext(_logger, _settings);
                var pathContext = NuGetPathContext.Create(_settings);

                // Get full dg spec
                var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(_solutionManager, cacheContext);

                // Avoid restoring solutions with zero potential PackageReference projects.
                if (DependencyGraphRestoreUtility.IsRestoreRequired(dgSpec))
                {
                    // NOTE: During restore for build integrated projects,
                    //       We might show the dialog even if there are no packages to restore
                    // When both currentStep and totalSteps are 0, we get a marquee on the dialog
                    await _logger.RunWithProgressAsync(
                        async (l, _, t) =>
                        {
                            // Display the restore opt out message if it has not been shown yet
                            await l.WriteHeaderAsync();

                            var sources = _sourceRepositoryProvider
                                .GetRepositories()
                                .ToList();

                            var providerCache = new RestoreCommandProvidersCache();
                            Action<SourceCacheContext> cacheModifier = (cache) => { };

                            var isRestoreOriginalAction = true;
                            var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                                _solutionManager,
                                dgSpec,
                                cacheContext,
                                providerCache,
                                cacheModifier,
                                sources,
                                _nuGetProjectContext.OperationId,
                                forceRestore,
                                isRestoreOriginalAction,
                                l,
                                t);

                            _packageCount += restoreSummaries.Select(summary => summary.InstallCount).Sum();
                            var isRestoreFailed = restoreSummaries.Any(summary => summary.Success == false);
                            _noOpProjectsCount = restoreSummaries.Where(summary => summary.NoOpRestore == true).Count();

                            if (isRestoreFailed)
                            {
                                _status = NuGetOperationStatus.Failed;
                            }
                            else if (_noOpProjectsCount < restoreSummaries.Count)
                            {
                                _status = NuGetOperationStatus.Succeeded;
                            }
                        },
                        token);
                }
            }
            else if (restoreSource == RestoreOperationSource.Explicit)
            {
                // Log an error when restore is disabled and user explicitly restore.
                await _logger.DoAsync((l, _) =>
                {
                    l.ShowError(Resources.PackageRefNotRestoredBecauseOfNoConsent);
                });
            }
        }

        // This event could be raised from multiple threads. Only perform thread-safe operations
        private void PackageRestoreManager_PackageRestored(
            object sender,
            PackageRestoredEventArgs args)
        {
            if (_status != NuGetOperationStatus.Cancelled && args.Restored)
            {
                var packageIdentity = args.Package;
                Interlocked.Increment(ref _currentCount);

                _logger.Do((_, progress) =>
                {
                    progress?.ReportProgress(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.RestoredPackage,
                            packageIdentity),
                        (uint)_currentCount,
                        (uint)_missingPackagesCount);
                });
            }
        }

        private void PackageRestoreManager_PackageRestoreFailedEvent(
            object sender,
            PackageRestoreFailedEventArgs args)
        {
            if (_status == NuGetOperationStatus.Cancelled)
            {
                // If an operation is canceled, a single message gets shown in the summary
                // that package restore has been canceled
                // Do not report it as separate errors
                return;
            }

            if (args.Exception is SignatureException)
            {
                _status = NuGetOperationStatus.Failed;

                var ex = args.Exception as SignatureException;

                if (!string.IsNullOrEmpty(ex.Message))
                {
                    _logger.Log(ex.AsLogMessage());
                }

                ex.Results.SelectMany(p => p.Issues).ToList().ForEach(p => _logger.Log(p));

                return;
            }

            if (args.ProjectNames.Any())
            {
                _status = NuGetOperationStatus.Failed;

                _logger.Do((l, _) =>
                {
                    foreach (var projectName in args.ProjectNames)
                    {
                        var exceptionMessage =
                            l.OutputVerbosity >= (int)VerbosityLevel.Detailed
                                ? args.Exception.ToString()
                                : args.Exception.Message;
                        var message = string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.PackageRestoreFailedForProject,
                            projectName,
                            exceptionMessage);
                        l.WriteLine(VerbosityLevel.Quiet, message);
                        l.ShowError(message);
                        l.WriteLine(VerbosityLevel.Normal, Resources.PackageRestoreFinishedForProject, projectName);
                    }
                });
            }
        }

        private async Task RestorePackagesOrCheckForMissingPackagesAsync(
            string solutionDirectory,
            bool isSolutionAvailable,
            RestoreOperationSource restoreSource,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                // If the solution is closed, SolutionDirectory will be unavailable. Just return. Do nothing
                return;
            }

            var packages = (await _packageRestoreManager.GetPackagesInSolutionAsync(
                solutionDirectory, token)).ToList();

            if (_packageRestoreConsent.IsGranted)
            {
                _currentCount = 0;

                if (packages.Count == 0)
                {
                    if (!isSolutionAvailable
                        && await CheckPackagesConfigAsync())
                    {
                        await _logger.DoAsync((l, _) =>
                        {
                            l.ShowError(Resources.SolutionIsNotSaved);
                            l.WriteLine(VerbosityLevel.Quiet, Resources.SolutionIsNotSaved);
                        });
                    }

                    // Restore is not applicable, since, there is no project with installed packages
                    return;
                }

                _packageCount += packages.Count;
                var missingPackagesList = packages.Where(p => p.IsMissing).ToList();
                _missingPackagesCount = missingPackagesList.Count;
                if (_missingPackagesCount > 0)
                {
                    // Only show the wait dialog, when there are some packages to restore
                    await _logger.RunWithProgressAsync(
                        async (l, _, t) =>
                        {
                            // Display the restore opt out message if it has not been shown yet
                            await l.WriteHeaderAsync();

                            await RestoreMissingPackagesInSolutionAsync(solutionDirectory, packages, l, t);
                        },
                        token);

                    // Mark that work is being done during this restore
                    _status = NuGetOperationStatus.Succeeded;
                }
            }
            else if (restoreSource == RestoreOperationSource.Explicit)
            {
                // When the user consent is not granted, missing packages may not be restored.
                // So, we just check for them, and report them as warning(s) on the error list window
                await _logger.RunWithProgressAsync(
                    (_, __, ___) => CheckForMissingPackagesAsync(packages),
                    token);
            }

            await _packageRestoreManager.RaisePackagesMissingEventForSolutionAsync(
                solutionDirectory,
                token);
        }

        /// <summary>
        /// Checks if there are missing packages that should be restored. If so, a warning will
        /// be added to the error list.
        /// </summary>
        private async Task CheckForMissingPackagesAsync(IEnumerable<PackageRestoreData> installedPackages)
        {
            var missingPackages = installedPackages.Where(p => p.IsMissing);

            if (missingPackages.Any())
            {
                await _logger.DoAsync((l, _) =>
                {
                    var errorText = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.PackageNotRestoredBecauseOfNoConsent,
                        string.Join(", ", missingPackages.Select(p => p.PackageReference.PackageIdentity.ToString())));
                    l.ShowError(errorText);
                });
            }
        }

        private async Task RestoreMissingPackagesInSolutionAsync(
            string solutionDirectory,
            IEnumerable<PackageRestoreData> packages,
            ILogger logger,
            CancellationToken token)
        {
            await TaskScheduler.Default;
            
            using (var cacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(cacheContext)
                {
                    ParentId = _nuGetProjectContext.OperationId,
                    ExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv3,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        ClientPolicyContext.GetClientPolicy(_settings, logger),
                        logger)
                };

                await _packageRestoreManager.RestoreMissingPackagesAsync(
                    solutionDirectory,
                    packages,
                    _nuGetProjectContext,
                    downloadContext,
                    logger,
                    token);
            }
        }

        /// <summary>
        /// Returns true if the package restore user consent is granted.
        /// </summary>
        /// <returns>True if the package restore user consent is granted.</returns>
        private static bool IsConsentGranted(ISettings settings)
        {
            var packageRestoreConsent = new PackageRestoreConsent(settings);
            return packageRestoreConsent.IsGranted;
        }

        /// <summary>
        /// Returns true if automatic package restore on build is enabled.
        /// </summary>
        /// <returns>True if automatic package restore on build is enabled.</returns>
        private static bool IsAutomatic(ISettings settings)
        {
            var packageRestoreConsent = new PackageRestoreConsent(settings);
            return packageRestoreConsent.IsAutomatic;
        }

        private async Task<bool> CheckPackagesConfigAsync()
        {
            return await NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = await _asyncServiceProvider.GetDTEAsync();
                var projects = dte.Solution.Projects;
                return projects
                    .OfType<EnvDTE.Project>()
                    .Select(p => new ProjectInfo(EnvDTEProjectInfoUtility.GetFullPath(p), p.Name))
                    .Any(p => p.CheckPackagesConfig());
            });
        }

        private class ProjectInfo
        {
            public string ProjectPath { get; }

            public string ProjectName { get; }

            public ProjectInfo(string projectPath, string projectName)
            {
                ProjectPath = projectPath;
                ProjectName = projectName;
            }

            public bool CheckPackagesConfig()
            {
                if (ProjectPath == null)
                {
                    return false;
                }
                else
                {
                    return File.Exists(Path.Combine(ProjectPath, "packages.config"))
                        || File.Exists(Path.Combine(ProjectPath, $"packages.{ProjectName}.config"));
                }
            }
        }
    }
}