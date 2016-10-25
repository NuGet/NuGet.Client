// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Implementation of solution restore operation as executed by the <see cref="SolutionRestoreWorker"/>.
    /// Designed to be called only once during its lifetime.
    /// </summary>
    internal sealed class SolutionRestoreJob : ISolutionRestoreJob, IDisposable
    {
        private readonly EnvDTE.DTE _dte;
        private readonly IPackageRestoreManager _packageRestoreManager;
        private readonly ISolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly ISettings _settings;
        private readonly RestoreOperationLogger _logger;

        private string _dependencyGraphProjectCacheHash;
        private INuGetProjectContext _nuGetProjectContext;

        // Restore summary
        // True if any of restores had errors
        private bool _hasErrors;
        // True if any of the restores were canceled
        private bool _cancelled;
        // True if any restores failed to restore all packages
        private bool _hasMissingPackages;
        // True if restore actions were taken and the summary should be displayed
        private bool _displayRestoreSummary;
        // If false the opt out message should be displayed
        private bool _hasOptOutBeenShown;

        private NuGetOperationStatus _status;
        private int _packageCount;

        private int _totalCount;
        private int _currentCount;

        private SolutionRestoreJob(
            IServiceProvider serviceProvider,
            IComponentModel componentModel,
            RestoreOperationLogger logger)
        {
            _packageRestoreManager = componentModel.GetService<IPackageRestoreManager>();

            if (_packageRestoreManager == null)
            {
                throw new ArgumentNullException(nameof(_packageRestoreManager));
            }

            _solutionManager = componentModel.GetService<IVsSolutionManager>();

            if (_solutionManager == null)
            {
                throw new ArgumentNullException(nameof(_solutionManager));
            }

            _sourceRepositoryProvider = componentModel.GetService<ISourceRepositoryProvider>();

            if (_sourceRepositoryProvider == null)
            {
                throw new ArgumentNullException(nameof(_sourceRepositoryProvider));
            }

            _settings = componentModel.GetService<ISettings>();

            if (_settings == null)
            {
                throw new ArgumentNullException(nameof(_settings));
            }

            _dte = serviceProvider.GetDTE();
            _logger = logger;
        }

        public static async Task<SolutionRestoreJob> CreateAsync(
            IServiceProvider serviceProvider,
            IComponentModel componentModel,
            RestoreOperationLogger logger,
            CancellationToken token)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (componentModel == null)
            {
                throw new ArgumentNullException(nameof(componentModel));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            return await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return new SolutionRestoreJob(serviceProvider, componentModel, logger);
            });
        }

        /// <summary>
        /// Restore job entry point. Not re-entrant.
        /// </summary>
        public async Task<bool> ExecuteAsync(
            SolutionRestoreRequest request,
            SolutionRestoreJobContext jobContext,
            CancellationToken token)
        {
            // update instance attributes with the shared context values
            _dependencyGraphProjectCacheHash = jobContext.DependencyGraphProjectCacheHash;
            _nuGetProjectContext = jobContext.NuGetProjectContext;

            _hasOptOutBeenShown = !request.ShowOptOutMessage;

            using (var ctr1 = token.Register(() => _cancelled = true))
            {
                try
                {
                    await RestoreAsync(request.ForceRestore, request.RestoreSource, token);
                }
                catch (Exception ex)
                {
                    // Log the exception to the console and activity log
                    await _logger.LogExceptionAsync(ex, request.LogError);
                }
                finally
                {
                    // update shared context values with instance attributes
                    jobContext.DependencyGraphProjectCacheHash = _dependencyGraphProjectCacheHash;
                }
            }

            if (request.ForceStatusWrite || _displayRestoreSummary)
            {
                // Always write out the final status message, even if no actions took place.
                await WriteSummaryAsync(_cancelled, _hasMissingPackages, _hasErrors, request.ForceStatusWrite);
            }

            return !_cancelled && !_hasErrors;
        }

        private async Task RestoreAsync(bool forceRestore, RestoreOperationSource restoreSource, CancellationToken token)
        {
            var startTime = DateTimeOffset.Now;
            _status = NuGetOperationStatus.Succeeded;

            // start timer for telemetry event
            TelemetryUtility.StartorResumeTimer();

            var projects = Enumerable.Empty<NuGetProject>();

            _packageRestoreManager.PackageRestoredEvent += PackageRestoreManager_PackageRestored;
            _packageRestoreManager.PackageRestoreFailedEvent += PackageRestoreManager_PackageRestoreFailedEvent;

            try
            {
                var solutionDirectory = _solutionManager.SolutionDirectory;
                var isSolutionAvailable = _solutionManager.IsSolutionAvailable;

                // make sure all projects are loaded before start to restore. Since
                // projects might not be loaded when DPL is enabled.
                _solutionManager.EnsureSolutionIsLoaded();

                // Get the projects from the SolutionManager
                // Note that projects that are not supported by NuGet, will not show up in this list
                projects = _solutionManager.GetNuGetProjects();

                // Check if there are any projects that are not INuGetIntegratedProject, that is,
                // projects with packages.config. If so, perform package restore on them
                if (projects.Any(project => !(project is INuGetIntegratedProject)))
                {
                    await RestorePackagesOrCheckForMissingPackagesAsync(
                        solutionDirectory,
                        isSolutionAvailable,
                        token);
                }

                var dependencyGraphProjects = projects
                    .OfType<IDependencyGraphProject>()
                    .ToList();

                await RestorePackageSpecProjectsAsync(
                    dependencyGraphProjects,
                    forceRestore,
                    isSolutionAvailable,
                    token);
            }
            finally
            {
                _packageRestoreManager.PackageRestoredEvent -= PackageRestoreManager_PackageRestored;
                _packageRestoreManager.PackageRestoreFailedEvent -= PackageRestoreManager_PackageRestoreFailedEvent;

                TelemetryUtility.StopTimer();

                // Emit telemetry event for restore operation
                EmitRestoreTelemetryEvent(
                    projects,
                    restoreSource,
                    startTime,
                    _status,
                    _packageCount,
                    TelemetryUtility.GetTimerElapsedTimeInSeconds());
            }
        }

        private static void EmitRestoreTelemetryEvent(IEnumerable<NuGetProject> projects,
            RestoreOperationSource source,
            DateTimeOffset startTime,
            NuGetOperationStatus status,
            int packageCount,
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
                status,
                packageCount,
                DateTimeOffset.Now,
                duration);

            RestoreTelemetryService.Instance.EmitRestoreEvent(restoreTelemetryEvent);
        }


        private async Task DisplayOptOutMessageAsync()
        {
            if (!_hasOptOutBeenShown)
            {
                _hasOptOutBeenShown = true;

                await _logger.DoAsync((l, _) =>
                {
                    // Only write the PackageRestoreOptOutMessage to output window,
                    // if, there are packages to restore
                    l.WriteLine(VerbosityLevel.Quiet, Strings.PackageRestoreOptOutMessage);
                });
            }
        }

        private async Task RestorePackageSpecProjectsAsync(
            List<IDependencyGraphProject> projects,
            bool forceRestore,
            bool isSolutionAvailable,
            CancellationToken token)
        {
            // Only continue if there are some projects.
            if (!projects.Any())
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
                                Strings.RelativeGlobalPackagesFolder,
                                globalPackagesFolder);

                            l.WriteLine(VerbosityLevel.Quiet, message);
                        });

                        // Cannot restore packages since globalPackagesFolder is a relative path
                        // and the solution is not available
                        return;
                    }
                }

                // Cache p2ps discovered from DTE
                var cacheContext = new DependencyGraphCacheContext(_logger);
                var pathContext = NuGetPathContext.Create(_settings);

                var isRestoreRequired = await DependencyGraphRestoreUtility.IsRestoreRequiredAsync(
                    _solutionManager,
                    forceRestore,
                    pathContext,
                    cacheContext,
                    _dependencyGraphProjectCacheHash);

                // No-op all project closures are up to date and all packages exist on disk.
                if (isRestoreRequired)
                {
                    // Save the project between operations.
                    _dependencyGraphProjectCacheHash = cacheContext.SolutionSpecHash;

                    // NOTE: During restore for build integrated projects,
                    //       We might show the dialog even if there are no packages to restore
                    // When both currentStep and totalSteps are 0, we get a marquee on the dialog
                    await _logger.RunWithProgressAsync(
                        async (l, _, t) =>
                        {
                            // Display the restore opt out message if it has not been shown yet
                            await DisplayOptOutMessageAsync();

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
                                _settings,
                                l,
                                t);

                            _packageCount += restoreSummaries.Select(summary => summary.InstallCount).Sum();
                            var isRestoreFailed = restoreSummaries.Any(summary => summary.Success == false);
                            if (isRestoreFailed)
                            {
                                _status = NuGetOperationStatus.Failed;
                            }
                        },
                        token);
                }
                else
                {
                    _status = NuGetOperationStatus.NoOp;
                }
            }
        }

        // This event could be raised from multiple threads. Only perform thread-safe operations
        private void PackageRestoreManager_PackageRestored(
            object sender,
            PackageRestoredEventArgs args)
        {
            if (!_cancelled && args.Restored)
            {
                var packageIdentity = args.Package;
                Interlocked.Increment(ref _currentCount);

                _logger.Do((_, progress) => 
                {
                    progress?.ReportProgress(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.RestoredPackage,
                            packageIdentity),
                        (uint)_currentCount,
                        (uint)_totalCount);
                });
            }
        }

        private void PackageRestoreManager_PackageRestoreFailedEvent(
            object sender,
            PackageRestoreFailedEventArgs args)
        {
            if (_cancelled)
            {
                // If an operation is canceled, a single message gets shown in the summary
                // that package restore has been canceled
                // Do not report it as separate errors
                return;
            }

            if (args.ProjectNames.Any())
            {
                // HasErrors will be used to show a message in the output window, that, Package restore failed
                // If Canceled is not already set to true
                _hasErrors = true;
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
                            Strings.PackageRestoreFailedForProject,
                            projectName,
                            exceptionMessage);
                        l.WriteLine(VerbosityLevel.Quiet, message);
                        l.ShowError(message);
                        l.WriteLine(VerbosityLevel.Normal, Strings.PackageRestoreFinishedForProject, projectName);
                    }
                });
            }
        }

        private async Task RestorePackagesOrCheckForMissingPackagesAsync(
            string solutionDirectory,
            bool isSolutionAvailable,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(solutionDirectory))
            {
                // If the solution is closed, SolutionDirectory will be unavailable. Just return. Do nothing
                return;
            }

            var packages = (await _packageRestoreManager.GetPackagesInSolutionAsync(
                solutionDirectory, token)).ToList();

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
                            l.ShowError(Strings.SolutionIsNotSaved);
                            l.WriteLine(VerbosityLevel.Quiet, Strings.SolutionIsNotSaved);
                        });
                    }

                    // Restore is not applicable, since, there is no project with installed packages
                    return;
                }

                _packageCount += packages.Count;
                var missingPackagesList = packages.Where(p => p.IsMissing).ToList();
                _totalCount = missingPackagesList.Count;
                if (_totalCount > 0)
                {
                    // Only show the wait dialog, when there are some packages to restore
                    await _logger.RunWithProgressAsync(
                        async (_, __, t) =>
                        {
                            // Display the restore opt out message if it has not been shown yet
                            await DisplayOptOutMessageAsync();

                            await RestoreMissingPackagesInSolutionAsync(solutionDirectory, packages, t);
                        },
                        token);

                    // Mark that work is being done during this restore
                    _hasMissingPackages = true;
                    _displayRestoreSummary = true;
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
                        Strings.PackageNotRestoredBecauseOfNoConsent,
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

        private Task WriteSummaryAsync(bool canceled, bool hasMissingPackages, bool hasErrors, bool forceStatusWrite)
        {
            return _logger.DoAsync((l, _) =>
            {
                // Write just "PackageRestore Canceled" message if package restore has been canceled
                if (canceled)
                {
                    l.WriteLine(
                        forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Minimal,
                        Strings.PackageRestoreCanceled);

                    return;
                }

                // Write just "Nothing to restore" message when there are no missing packages.
                if (!hasMissingPackages)
                {
                    l.WriteLine(
                        forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Detailed,
                        Strings.NothingToRestore);

                    return;
                }

                // Here package restore has happened. It can finish with/without error.
                if (hasErrors)
                {
                    l.WriteLine(
                        forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Minimal,
                        Strings.PackageRestoreFinishedWithError);
                }
                else
                {
                    l.WriteLine(
                        forceStatusWrite ? VerbosityLevel.Quiet : VerbosityLevel.Normal,
                        Strings.PackageRestoreFinished);
                }
            });
        }

        private async Task<bool> CheckPackagesConfigAsync()
        {
            return await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var projects = _dte.Solution.Projects;
                return projects
                    .OfType<EnvDTE.Project>()
                    .Select(p => new ProjectInfo(EnvDTEProjectUtility.GetFullPath(p), p.Name))
                    .Any(p => p.CheckPackagesConfig());
            });
        }

        public void Dispose()
        {
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
