// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Performs package manager actions and controls the UI to display output while the actions are taking place.
    /// </summary>
    public sealed class UIActionEngine
    {
        private readonly ISourceRepositoryProvider _sourceProvider;
        private readonly NuGetPackageManager _packageManager;
        private readonly INuGetLockService _lockService;

        /// <summary>
        /// Create a UIActionEngine to perform installs/uninstalls
        /// </summary>
        public UIActionEngine(
            ISourceRepositoryProvider sourceProvider, 
            NuGetPackageManager packageManager,
            INuGetLockService lockService)
        {
            if (sourceProvider == null)
            {
                throw new ArgumentNullException(nameof(sourceProvider));
            }

            if (packageManager == null)
            {
                throw new ArgumentNullException(nameof(packageManager));
            }

            if (lockService == null)
            {
                throw new ArgumentNullException(nameof(lockService));
            }

            _sourceProvider = sourceProvider;
            _packageManager = packageManager;
            _lockService = lockService;
        }

        /// <summary>
        /// Perform a user action.
        /// </summary>
        /// <remarks>This needs to be called from a background thread. It may hang on the UI thread.</remarks>
        public async Task PerformActionAsync(
            INuGetUI uiService,
            UserAction userAction,
            DependencyObject windowOwner,
            CancellationToken token)
        {
            var operationType = NuGetOperationType.Install;
            if (userAction.Action == NuGetProjectActionType.Uninstall)
            {
                operationType = NuGetOperationType.Uninstall;
            }

            await PerformActionImplAsync(
                uiService,
                () =>
                {
                    var projects = uiService.Projects;

                    // Allow prerelease packages only if the target is prerelease
                    var includePrelease =
                        userAction.Action == NuGetProjectActionType.Uninstall ||
                        userAction.Version.IsPrerelease == true;

                    var includeUnlisted = userAction.Action == NuGetProjectActionType.Uninstall;

                    var resolutionContext = new ResolutionContext(
                        uiService.DependencyBehavior,
                        includePrelease,
                        includeUnlisted,
                        VersionConstraints.None);

                    return GetActionsAsync(
                        uiService,
                        projects,
                        userAction,
                        uiService.RemoveDependencies,
                        uiService.ForceRemove,
                        resolutionContext,
                        projectContext: uiService.ProgressWindow,
                        token: token);
                },
                (actions) =>
                {
                    return ExecuteActionsAsync(actions, uiService.ProgressWindow, userAction, token);
                },
                windowOwner,
                operationType,
                token);
        }

        /// <summary>
        /// Perform the multi-package update action.
        /// </summary>
        /// <remarks>This needs to be called from a background thread. It may hang on the UI thread.</remarks>
        public async Task PerformUpdateAsync(
            INuGetUI uiService,
            List<PackageIdentity> packagesToUpdate,
            DependencyObject windowOwner,
            CancellationToken token)
        {
            await PerformActionImplAsync(
                uiService,
                () =>
                {
                    return ResolveActionsForUpdate(
                        uiService,
                        packagesToUpdate,
                        token);
                },
                async (actions) =>
                {
                    // Get all Nuget projects and actions and call ExecuteNugetProjectActions once for all the projects.
                    var nugetProjects = actions.Select(action => action.Project);
                    var nugetActions = actions.Select(action => action.Action);
                    await _packageManager.ExecuteNuGetProjectActionsAsync(
                        nugetProjects,
                        nugetActions,
                        uiService.ProgressWindow,
                        token);
                },
                windowOwner,
                NuGetOperationType.Update,
                token);
        }

        /// <summary>
        /// Calculates the list of actions needed to perform packages updates.
        /// </summary>
        /// <param name="uiService">ui service.</param>
        /// <param name="packagesToUpdate">The list of packages to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The list of actions.</returns>
        private async Task<IReadOnlyList<ResolvedAction>> ResolveActionsForUpdate(
            INuGetUI uiService,
            List<PackageIdentity> packagesToUpdate,
            CancellationToken token)
        {
            var resolvedActions = new List<ResolvedAction>();

            // Keep a single gather cache across projects
            var gatherCache = new GatherCache();

            var includePrerelease = packagesToUpdate.Where(
                package => package.Version.IsPrerelease).Any();

            var resolutionContext = new ResolutionContext(
                uiService.DependencyBehavior,
                includePrelease: includePrerelease,
                includeUnlisted: true,
                versionConstraints: VersionConstraints.None,
                gatherCache: gatherCache);

            var secondarySources = _sourceProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);

            var actions = await _packageManager.PreviewUpdatePackagesAsync(
                packagesToUpdate,
                uiService.Projects,
                resolutionContext,
                uiService.ProgressWindow,
                uiService.ActiveSources,
                secondarySources,
                token);

            resolvedActions.AddRange(actions.Select(action => new ResolvedAction(action.Project, action))
                .ToList());

            return resolvedActions;
        }

        /// <summary>
        /// The internal implementation to perform user action.
        /// </summary>
        /// <param name="resolveActionsAsync">A function that returns a task that resolves the user
        /// action into project actions.</param>
        /// <param name="executeActionsAsync">A function that returns a task that executes
        /// the project actions.</param>
        private async Task PerformActionImplAsync(
            INuGetUI uiService,
            Func<Task<IReadOnlyList<ResolvedAction>>> resolveActionsAsync,
            Func<IReadOnlyList<ResolvedAction>, Task> executeActionsAsync,
            DependencyObject windowOwner,
            NuGetOperationType operationType,
            CancellationToken token)
        {
            var status = NuGetOperationStatus.Succeeded;
            var startTime = DateTimeOffset.Now;
            var packageCount = 0;
            var operationId = Guid.NewGuid().ToString();

            // Enable granular level telemetry events for nuget ui operation
            var telemetryService = new TelemetryServiceHelper();
            uiService.ProgressWindow.TelemetryService = telemetryService;

            var lck = await _lockService.AcquireLockAsync(token);

            try
            {
                uiService.ShowProgressDialog(windowOwner);

                TelemetryUtility.StartorResumeTimer();

                var actions = await resolveActionsAsync();
                var results = GetPreviewResults(actions);

                if (operationType == NuGetOperationType.Uninstall)
                {
                    packageCount = results.SelectMany(result => result.Deleted).
                        Select(package => package.Id).Distinct().Count();
                }
                else
                {
                    var addCount = results.SelectMany(result => result.Added).
                        Select(package => package.Id).Distinct().Count();

                    var updateCount = results.SelectMany(result => result.Updated).
                        Select(result => result.New.Id).Distinct().Count();

                    // update packages count
                    packageCount = addCount + updateCount;

                    if (updateCount > 0)
                    {
                        // set operation type to update when there are packages being updated
                        operationType = NuGetOperationType.Update;
                    }
                }

                TelemetryUtility.StopTimer();

                // Show the preview window.
                if (uiService.DisplayPreviewWindow)
                {
                    var shouldContinue = uiService.PromptForPreviewAcceptance(results);
                    if (!shouldContinue)
                    {
                        return;
                    }
                }

                TelemetryUtility.StartorResumeTimer();

                // Show the license acceptance window.
                var accepted = await CheckLicenseAcceptanceAsync(uiService, results, token);

                TelemetryUtility.StartorResumeTimer();

                if (!accepted)
                {
                    return;
                }

                // Warn about the fact that the "dotnet" TFM is deprecated.
                if (uiService.DisplayDeprecatedFrameworkWindow)
                {
                    var shouldContinue = ShouldContinueDueToDotnetDeprecation(uiService, actions, token);

                    TelemetryUtility.StartorResumeTimer();

                    if (!shouldContinue)
                    {
                        return;
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    // execute the actions
                    await executeActionsAsync(actions);

                    // fires ActionsExecuted event to update the UI
                    uiService.OnActionsExecuted(actions);
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                status = NuGetOperationStatus.Failed;
                if (ex.InnerException != null)
                {
                    uiService.ShowError(ex.InnerException);
                }
                else
                {
                    uiService.ShowError(ex);
                }
            }
            catch (Exception ex)
            {
                status = NuGetOperationStatus.Failed;
                uiService.ShowError(ex);
            }
            finally
            {
                lck.Dispose();

                uiService.CloseProgressDialog();

                TelemetryUtility.StopTimer();

                var duration = TelemetryUtility.GetTimerElapsedTime();
                uiService.ProgressWindow.Log(MessageLevel.Info,
                    string.Format(CultureInfo.CurrentCulture, Resources.Operation_TotalTime, duration));

                var actionTelemetryEvent = TelemetryUtility.GetActionTelemetryEvent(
                    uiService.Projects,
                    operationType,
                    OperationSource.UI,
                    startTime,
                    status,
                    packageCount,
                    duration.TotalSeconds);

                ActionsTelemetryService.Instance.EmitActionEvent(actionTelemetryEvent, telemetryService.TelemetryEvents);
            }
        }

        // Returns false if user doesn't accept license agreements.
        private async Task<bool> CheckLicenseAcceptanceAsync(
            INuGetUI uiService,
            IEnumerable<PreviewResult> results,
            CancellationToken token)
        {
            // find all the packages that might need a license acceptance
            var licenseCheck = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
            foreach (var result in results)
            {
                foreach (var pkg in result.Added)
                {
                    licenseCheck.Add(pkg);
                }

                foreach (var pkg in result.Updated)
                {
                    licenseCheck.Add(pkg.New);
                }
            }

            var licenseMetadata = await GetPackageMetadataAsync(uiService, licenseCheck, token);

            TelemetryUtility.StopTimer();

            // show license agreement
            if (licenseMetadata.Any(e => e.RequireLicenseAcceptance))
            {
                var licenseInfoItems = licenseMetadata
                    .Where(p => p.RequireLicenseAcceptance)
                    .Select(e => new PackageLicenseInfo(e.Identity.Id, e.LicenseUrl, e.Authors));
                return uiService.PromptForLicenseAcceptance(licenseInfoItems);
            }

            return true;
        }

        /// <summary>
        /// Warns the user about the fact that the dotnet TFM is deprecated.
        /// </summary>
        /// <returns>Returns true if the user wants to ignore the warning or if the warning does not apply.</returns>
        private bool ShouldContinueDueToDotnetDeprecation(
            INuGetUI uiService,
            IEnumerable<ResolvedAction> actions,
            CancellationToken token)
        {
            var projects = DotnetDeprecatedPrompt.GetAffectedProjects(actions);

            TelemetryUtility.StopTimer();

            if (projects.Any())
            {
                return uiService.WarnAboutDotnetDeprecation(projects);
            }

            return true;
        }

        /// <summary>
        /// Execute the installs/uninstalls
        /// </summary>
        private async Task ExecuteActionsAsync(IEnumerable<ResolvedAction> actions,
            NuGetUIProjectContext projectContext, UserAction userAction, CancellationToken token)
        {
            var processedDirectInstalls = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
            foreach (var projectActions in actions.GroupBy(e => e.Project))
            {
                var nuGetProjectActions = projectActions.Select(e => e.Action);
                var directInstall = GetDirectInstall(nuGetProjectActions, userAction, projectContext.CommonOperations);
                if (directInstall != null
                    && !processedDirectInstalls.Contains(directInstall))
                {
                    NuGetPackageManager.SetDirectInstall(directInstall, projectContext);
                    processedDirectInstalls.Add(directInstall);
                }
                await _packageManager.ExecuteNuGetProjectActionsAsync(projectActions.Key, nuGetProjectActions, projectContext, token);
                NuGetPackageManager.ClearDirectInstall(projectContext);
            }
        }

        private static PackageIdentity GetDirectInstall(IEnumerable<NuGetProjectAction> nuGetProjectActions,
            UserAction userAction,
            ICommonOperations commonOperations)
        {
            if (commonOperations != null
                && userAction != null
                && userAction.Action == NuGetProjectActionType.Install
                && nuGetProjectActions.Any())
            {
                return new PackageIdentity(userAction.PackageId, userAction.Version);
            }

            return null;
        }

        /// <summary>
        /// Return the resolve package actions
        /// </summary>
        private async Task<IReadOnlyList<ResolvedAction>> GetActionsAsync(
            INuGetUI uiService,
            IEnumerable<NuGetProject> targets,
            UserAction userAction,
            bool removeDependencies,
            bool forceRemove,
            ResolutionContext resolutionContext,
            INuGetProjectContext projectContext,
            CancellationToken token)
        {
            var results = new List<ResolvedAction>();

            Debug.Assert(userAction.PackageId != null, "Package id can never be null in a User action");
            if (userAction.Action == NuGetProjectActionType.Install)
            {
                foreach (var target in targets)
                {
                    var actions = await _packageManager.PreviewInstallPackageAsync(
                        target,
                        new PackageIdentity(userAction.PackageId, userAction.Version),
                        resolutionContext,
                        projectContext,
                        uiService.ActiveSources,
                        null,
                        token);
                    results.AddRange(actions.Select(a => new ResolvedAction(target, a)));
                }
            }
            else
            {
                var uninstallationContext = new UninstallationContext(
                    removeDependencies: removeDependencies,
                    forceRemove: forceRemove);

                foreach (var target in targets)
                {
                    IEnumerable<NuGetProjectAction> actions;

                    actions = await _packageManager.PreviewUninstallPackageAsync(
                        target, userAction.PackageId, uninstallationContext, projectContext, token);

                    results.AddRange(actions.Select(a => new ResolvedAction(target, a)));
                }
            }

            return results;
        }

        /// <summary>
        /// Convert NuGetProjectActions into PreviewResult types
        /// </summary>
        private static IReadOnlyList<PreviewResult> GetPreviewResults(IEnumerable<ResolvedAction> projectActions)
        {
            var results = new List<PreviewResult>();

            var expandedActions = new List<ResolvedAction>();

            // BuildIntegratedProjectActions contain all project actions rolled up into a single action,
            // to display these we need to expand them into the low level actions.
            foreach (var action in projectActions)
            {
                var buildIntegratedAction = action.Action as BuildIntegratedProjectAction;

                if (buildIntegratedAction != null)
                {
                    foreach (var buildAction in buildIntegratedAction.GetProjectActions())
                    {
                        expandedActions.Add(new ResolvedAction(action.Project, buildAction));
                    }
                }
                else
                {
                    // leave the action as is
                    expandedActions.Add(action);
                }
            }

            // Group actions by project
            var actionsByProject = expandedActions.GroupBy(action => action.Project);

            // Group actions by operation
            foreach (var actions in actionsByProject)
            {
                var installed = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);
                var uninstalled = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);
                var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var action in actions.Select(a => a.Action))
                {
                    var package = action.PackageIdentity;
                    packageIds.Add(package.Id);

                    // Create new identities without the dependency graph
                    if (action.NuGetProjectActionType == NuGetProjectActionType.Install)
                    {
                        installed[package.Id] = new PackageIdentity(package.Id, package.Version);
                    }
                    else
                    {
                        uninstalled[package.Id] = new PackageIdentity(package.Id, package.Version);
                    }
                }

                var added = new List<PackageIdentity>();
                var deleted = new List<PackageIdentity>();
                var updated = new List<UpdatePreviewResult>();
                foreach (var packageId in packageIds)
                {
                    var isInstalled = installed.ContainsKey(packageId);
                    var isUninstalled = uninstalled.ContainsKey(packageId);

                    if (isInstalled && isUninstalled)
                    {
                        // the package is updated
                        updated.Add(new UpdatePreviewResult(uninstalled[packageId], installed[packageId]));
                        installed.Remove(packageId);
                    }
                    else if (isInstalled && !isUninstalled)
                    {
                        // the package is added
                        added.Add(installed[packageId]);
                    }
                    else if (!isInstalled && isUninstalled)
                    {
                        // the package is deleted
                        deleted.Add(uninstalled[packageId]);
                    }
                }

                var result = new PreviewResult(actions.Key, added, deleted, updated);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Get the package metadata to see if RequireLicenseAcceptance is true
        /// </summary>
        private async Task<List<IPackageSearchMetadata>> GetPackageMetadataAsync(
            INuGetUI uiService,
            IEnumerable<PackageIdentity> packages,
            CancellationToken token)
        {
            var results = new List<IPackageSearchMetadata>();

            // local sources
            var sources = new List<SourceRepository>();
            sources.Add(_packageManager.PackagesFolderSourceRepository);
            sources.AddRange(_packageManager.GlobalPackageFolderRepositories);

            var allPackages = packages.ToArray();

            // first check all the packages with local sources.
            var completed = (await TaskCombinators.ThrottledAsync(
                allPackages,
                (p, t) => GetPackageMetadataAsync(sources, p, t),
                token)).Where(metadata => metadata != null).ToArray();

            results.AddRange(completed);

            if (completed.Length != allPackages.Length)
            {
                // get remaining package's metadata from remote repositories
                var remainingPackages = allPackages.Where(package => !completed.Any(pack => pack.Identity.Equals(package)));

                var remoteResults = (await TaskCombinators.ThrottledAsync(
                    remainingPackages,
                    (p, t) => GetPackageMetadataAsync(uiService.ActiveSources, p, t),
                    token)).Where(metadata => metadata != null).ToArray();

                results.AddRange(remoteResults);
            }

            // check if missing metadata for any package
            if (allPackages.Length != results.Count)
            {
                var package = allPackages.First(pkg => !results.Any(result => result.Identity.Equals(pkg)));

                throw new InvalidOperationException(
                        string.Format("Unable to find metadata of {0}", package));
            }

            return results;
        }

        private void LogError(Task task, INuGetUI uiService)
        {
            var exception = ExceptionUtilities.Unwrap(task.Exception);
            uiService.ProgressWindow.Log(MessageLevel.Error, exception.Message);
        }

        private static async Task<IPackageSearchMetadata> GetPackageMetadataAsync(
            IEnumerable<SourceRepository> sources,
            PackageIdentity package,
            CancellationToken token)
        {
            var exceptionList = new List<InvalidOperationException>();

            foreach (var source in sources)
            {
                var metadataResource = source.GetResource<PackageMetadataResource>();
                if (metadataResource == null)
                {
                    continue;
                }

                try
                {
                    var packageMetadata = await metadataResource.GetMetadataAsync(
                        package,
                        log: Common.NullLogger.Instance,
                        token: token);
                    if (packageMetadata != null)
                    {
                        return packageMetadata;
                    }
                }
                catch (InvalidOperationException e)
                {
                    exceptionList.Add(e);
                }
            }

            if (exceptionList.Count > 0)
            {
                throw new AggregateException(exceptionList);
            }

            return null;
        }
    }
}
