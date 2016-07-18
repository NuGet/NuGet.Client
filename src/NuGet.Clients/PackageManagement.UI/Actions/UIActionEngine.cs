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
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Performs package manager actions and controls the UI to display output while the actions are taking place.
    /// </summary>
    public class UIActionEngine
    {
        private readonly ISourceRepositoryProvider _sourceProvider;
        private readonly NuGetPackageManager _packageManager;

        /// <summary>
        /// Create a UIActionEngine to perform installs/uninstalls
        /// </summary>
        public UIActionEngine(ISourceRepositoryProvider sourceProvider, NuGetPackageManager packageManager)
        {
            _sourceProvider = sourceProvider;
            _packageManager = packageManager;
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
            var stopWatch = new Stopwatch();
            stopWatch.Start();

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
                token);

            stopWatch.Stop();
            uiService.ProgressWindow.Log(ProjectManagement.MessageLevel.Info, string.Format(CultureInfo.CurrentCulture, Resources.Operation_TotalTime, stopWatch.Elapsed));
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
            var stopWatch = new Stopwatch();
            stopWatch.Start();

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
                    foreach (var projectActions in actions.GroupBy(action => action.Project))
                    {
                        await _packageManager.ExecuteNuGetProjectActionsAsync(
                           projectActions.Key,
                           projectActions.Select(action => action.Action),
                           uiService.ProgressWindow,
                           token);
                    }
                },
                windowOwner,
                token);

            stopWatch.Stop();
            uiService.ProgressWindow.Log(ProjectManagement.MessageLevel.Info, string.Format(CultureInfo.CurrentCulture, Resources.Operation_TotalTime, stopWatch.Elapsed));
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

            foreach (var project in uiService.Projects)
            {
                var installedPackages = await project.GetInstalledPackagesAsync(token);
                HashSet<string> packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in installedPackages)
                {
                    packageIds.Add(p.PackageIdentity.Id);
                }

                // We need to filter out packages from packagesToUpdate that are not installed
                // in the current project. Otherwise, we'll incorrectly install a
                // package that is not installed before.
                var packagesToUpdateInProject = packagesToUpdate.Where(
                    package => packageIds.Contains(package.Id)).ToList();

                if (packagesToUpdateInProject.Any())
                {
                    var includePrerelease = packagesToUpdateInProject.Where(
                        package => package.Version.IsPrerelease).Any();

                    var resolutionContext = new ResolutionContext(
                        uiService.DependencyBehavior,
                        includePrelease: includePrerelease,
                        includeUnlisted: true,
                        versionConstraints: VersionConstraints.None,
                        gatherCache: gatherCache);

                    var actions = await _packageManager.PreviewUpdatePackagesAsync(
                        packagesToUpdateInProject,
                        project,
                        resolutionContext,
                        uiService.ProgressWindow,
                        uiService.ActiveSources,
                        uiService.ActiveSources,
                        token);
                    resolvedActions.AddRange(actions.Select(action => new ResolvedAction(project, action))
                        .ToList());
                }
            }

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
            CancellationToken token)
        {
            try
            {
                uiService.ShowProgressDialog(windowOwner);

                var actions = await resolveActionsAsync();
                var results = GetPreviewResults(actions);

                // Show the preview window.
                if (uiService.DisplayPreviewWindow)
                {
                    var shouldContinue = uiService.PromptForPreviewAcceptance(results);
                    if (!shouldContinue)
                    {
                        return;
                    }
                }
                
                // Show the license acceptance window.
                var accepted = await CheckLicenseAcceptanceAsync(uiService, results, token);
                if (!accepted)
                {
                    return;
                }

                // Warn about the fact that the "dotnet" TFM is deprecated.
                if (uiService.DisplayDeprecatedFrameworkWindow)
                {
                    var shouldContinue = WarnAboutDotnetDeprecation(uiService, actions, token);
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
                uiService.ShowError(ex);
            }
            finally
            {
                uiService.CloseProgressDialog();
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
            var licenseMetadata = await GetPackageMetadataAsync(licenseCheck, token);

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
        private bool WarnAboutDotnetDeprecation(
            INuGetUI uiService,
            IEnumerable<ResolvedAction> actions,
            CancellationToken token)
        {
            var projects = new HashSet<NuGetProject>();

            foreach (var action in actions)
            {
                var buildIntegrationAction = action.Action as BuildIntegratedProjectAction;

                if (buildIntegrationAction == null || buildIntegrationAction.RestoreResult.Success)
                {
                    continue;
                }

                // Get all failed compatibility check results.
                var incompatible = buildIntegrationAction
                    .RestoreResult
                    .CompatibilityCheckResults
                    .Where(result => !result.Success && result.Issues.Any());

                // Only focus on compatibility check results when restoring for "dotnet".
                var anyIncompatibleDotnet = incompatible.Any(result => string.Equals(
                    result.Graph.Framework.Framework,
                    FrameworkConstants.FrameworkIdentifiers.NetPlatform,
                    StringComparison.OrdinalIgnoreCase));

                if (anyIncompatibleDotnet)
                {
                    projects.Add(action.Project);
                }
            }

            if (projects.Any())
            {
                return uiService.WarnAboutDotnetDeprecation(projects);
            }

            return true;
        }

        /// <summary>
        /// Execute the installs/uninstalls
        /// </summary>
        protected async Task ExecuteActionsAsync(IEnumerable<ResolvedAction> actions,
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
        protected async Task<IReadOnlyList<ResolvedAction>> GetActionsAsync(
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
        protected static IReadOnlyList<PreviewResult> GetPreviewResults(IEnumerable<ResolvedAction> projectActions)
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
        private async Task<List<IPackageSearchMetadata>> GetPackageMetadataAsync(IEnumerable<PackageIdentity> packages, CancellationToken token)
        {
            var sources = _sourceProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);

            var results = new List<IPackageSearchMetadata>();
            foreach (var package in packages)
            {
                var metadata = await GetPackageMetadataAsync(sources, package, token);
                if (metadata == null)
                {
                    throw new InvalidOperationException(
                        string.Format("Unable to find metadata of {0}", package));
                }

                results.Add(metadata);
            }

            return results;
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
                    var r = await metadataResource.GetMetadataAsync(
                        package.Id,
                        includePrerelease: true,
                        includeUnlisted: true,
                        log: Common.NullLogger.Instance,
                        token: token);
                    var packageMetadata = r.FirstOrDefault(p => p.Identity.Version == package.Version);
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
