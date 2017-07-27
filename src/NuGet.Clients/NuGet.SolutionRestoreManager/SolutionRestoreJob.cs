// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly IPackageRestoreManager _packageRestoreManager;
        private readonly IVsSolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly ISettings _settings;
        private readonly IRestoreEventsPublisher _restoreEventsPublisher;
        private readonly ILightWeightProjectWorkspaceService _lightWeightProjectWorkspaceService;

        private RestoreOperationLogger _logger;
        private INuGetProjectContext _nuGetProjectContext;

        private NuGetOperationStatus _status;
        private int _packageCount;
        private int _noOpProjectsCount;

        // relevant to packages.config restore only
        private int _missingPackagesCount;
        private int _currentCount;

        [ImportingConstructor]
        public SolutionRestoreJob(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IPackageRestoreManager packageRestoreManager,
            IVsSolutionManager solutionManager,
            ISourceRepositoryProvider sourceRepositoryProvider,
            IRestoreEventsPublisher restoreEventsPublisher,
            ISettings settings,
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService)
        {
            Assumes.Present(serviceProvider);
            Assumes.Present(packageRestoreManager);
            Assumes.Present(solutionManager);
            Assumes.Present(sourceRepositoryProvider);
            Assumes.Present(restoreEventsPublisher);
            Assumes.Present(settings);

            _serviceProvider = serviceProvider;
            _packageRestoreManager = packageRestoreManager;
            _solutionManager = solutionManager;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _restoreEventsPublisher = restoreEventsPublisher;
            _settings = settings;
            _lightWeightProjectWorkspaceService = lightWeightProjectWorkspaceService;
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
            TelemetryUtility.StartorResumeTimer();

            var projects = Enumerable.Empty<NuGetProject>();

            _packageRestoreManager.PackageRestoredEvent += PackageRestoreManager_PackageRestored;
            _packageRestoreManager.PackageRestoreFailedEvent += PackageRestoreManager_PackageRestoreFailedEvent;

            try
            {
                var solutionDirectory = _solutionManager.SolutionDirectory;
                var isSolutionAvailable = _solutionManager.IsSolutionAvailable;

                if (solutionDirectory == null)
                {
                    await _logger.DoAsync((l, _) =>
                    {
                        _status = NuGetOperationStatus.Failed;
                        l.ShowError(Resources.SolutionIsNotSaved);
                        l.WriteLine(VerbosityLevel.Minimal, Resources.SolutionIsNotSaved);
                    });
                    return;
                }

                // Check if solution has deferred projects
                var deferredProjectsData = new DeferredProjectRestoreData(new Dictionary<PackageReference, List<string>>(), new List<PackageSpec>());
                if (await DeferredProjectVSUtility.SolutionHasDeferredProjectsAsync())
                {
                    var deferredProjectsPath = await DeferredProjectVSUtility.GetDeferredProjectsFilePathAsync();

                    deferredProjectsData = await DeferredProjectRestoreUtility.GetDeferredProjectsData(_lightWeightProjectWorkspaceService, deferredProjectsPath, _settings, token);
                }

                // Get the projects from the SolutionManager
                // Note that projects that are not supported by NuGet, will not show up in this list
                projects = _solutionManager.GetNuGetProjects();

                // Check if there are any projects that are not INuGetIntegratedProject, that is,
                // projects with packages.config. OR 
                // any of the deferred project is type of packages.config, If so, perform package restore on them
                if (projects.Any(project => !(project is INuGetIntegratedProject)) ||
                   deferredProjectsData.PackageReferenceDict.Count > 0)
                {
                    await RestorePackagesOrCheckForMissingPackagesAsync(
                        solutionDirectory,
                        isSolutionAvailable,
                        deferredProjectsData.PackageReferenceDict,
                        token);
                }

                var dependencyGraphProjects = projects
                    .OfType<IDependencyGraphProject>()
                    .ToList();

                await RestorePackageSpecProjectsAsync(
                    dependencyGraphProjects,
                    forceRestore,
                    isSolutionAvailable,
                    deferredProjectsData.PackageSpecs,
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

                TelemetryUtility.StopTimer();

                var duration = TelemetryUtility.GetTimerElapsedTime();
                await _logger.WriteSummaryAsync(_status, duration);

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
                Guid.NewGuid().ToString(),
                projectIds,
                source,
                startTime,
                _status,
                _packageCount,
                _noOpProjectsCount,
                DateTimeOffset.Now,
                duration);

            RestoreTelemetryService.Instance.EmitRestoreEvent(restoreTelemetryEvent);
        }

        private async Task RestorePackageSpecProjectsAsync(
           List<IDependencyGraphProject> projects,
           bool forceRestore,
           bool isSolutionAvailable,
           IReadOnlyList<PackageSpec> packageSpecs,
           CancellationToken token)
        {
            // Only continue if there are some build integrated type projects.
            if (!(projects.Any(project => project is BuildIntegratedNuGetProject) ||
                packageSpecs.Any(project => IsProjectBuildIntegrated(project))))
            {
                return;
            }

            if (IsConsentGranted(_settings))
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

                // add deferred projects package spec in cacheContext packageSpecCache
                cacheContext.DeferredPackageSpecs.AddRange(packageSpecs);

                // Get full dg spec
                // TODO: pass this down instead of creating it twice.
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

                            var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                                _solutionManager,
                                cacheContext,
                                providerCache,
                                cacheModifier,
                                sources,
                                forceRestore,
                                dgSpec,
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
        }

        private bool IsProjectBuildIntegrated(PackageSpec packageSpec)
        {
            return packageSpec.RestoreMetadata?.ProjectStyle == ProjectStyle.PackageReference ||
                packageSpec.RestoreMetadata?.ProjectStyle == ProjectStyle.ProjectJson ||
                packageSpec.RestoreMetadata?.ProjectStyle == ProjectStyle.DotnetCliTool;
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
            IReadOnlyDictionary<PackageReference, List<string>> packageReferencesDict,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                // If the solution is closed, SolutionDirectory will be unavailable. Just return. Do nothing
                return;
            }

            var packages = (await _packageRestoreManager.GetPackagesInSolutionAsync(
                solutionDirectory, token)).ToList();

            packages.AddRange(
                _packageRestoreManager.GetPackagesRestoreData(
                    solutionDirectory, packageReferencesDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList())));

            if (IsConsentGranted(_settings))
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

                            await RestoreMissingPackagesInSolutionAsync(solutionDirectory, packages, t);
                        },
                        token);

                    // Mark that work is being done during this restore
                    _status = NuGetOperationStatus.Succeeded;
                }
            }
            else
            {
                // When the user consent is not granted, missing packages may not be restored.
                // So, we just check for them, and report them as warning(s) on the error list window
                await _logger.RunWithProgressAsync(
                    (_, __, ___) => CheckForMissingPackages(packages),
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
        private void CheckForMissingPackages(IEnumerable<PackageRestoreData> missingPackages)
        {
            if (missingPackages.Any())
            {
                _logger.Do((l, _) =>
                {
                    var errorText = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.PackageNotRestoredBecauseOfNoConsent,
                        string.Join(", ", missingPackages.Select(p => p.ToString())));
                    l.ShowError(errorText);
                });
            }
        }

        private async Task RestoreMissingPackagesInSolutionAsync(
            string solutionDirectory,
            IEnumerable<PackageRestoreData> packages,
            CancellationToken token)
        {
            await TaskScheduler.Default;

            using (var cacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(cacheContext);

                await _packageRestoreManager.RestoreMissingPackagesAsync(
                    solutionDirectory,
                    packages,
                    _nuGetProjectContext,
                    downloadContext,
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

                var dte = _serviceProvider.GetDTE();
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
