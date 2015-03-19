using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Resolver;

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
        public async Task PerformAction(INuGetUI uiService, UserAction userAction, DependencyObject windowOwner, CancellationToken token)
        {
            try
            {
                uiService.ShowProgressDialog(windowOwner);

                var projects = uiService.Projects;

                // TODO: should stable packages allow prerelease dependencies if include prerelease was checked?
                // Allow prerelease packages only if the target is prerelease
                bool includePrelease = userAction.PackageIdentity.Version.IsPrerelease || userAction.Action == NuGetProjectActionType.Uninstall;
                bool includeUnlisted = userAction.Action == NuGetProjectActionType.Uninstall;

                ResolutionContext resolutionContext = new ResolutionContext(uiService.DependencyBehavior, includePrelease, includeUnlisted);

                IEnumerable<Tuple<NuGetProject, NuGetProjectAction>> actions = await GetActions(
                    uiService,
                    projects,
                    userAction,
                    removeDependencies: uiService.RemoveDependencies,
                    forceRemove: uiService.ForceRemove,
                    resolutionContext: resolutionContext,
                    projectContext: uiService.ProgressWindow,
                    token: token);
                IEnumerable<PreviewResult> results = GetPreviewResults(actions);

                // preview window
                if (uiService.DisplayPreviewWindow)
                {
                    var shouldContinue = false;

                    shouldContinue = uiService.PromptForPreviewAcceptance(results);

                    if (!shouldContinue)
                    {
                        return;
                    }
                }

                bool accepted = await CheckLicenseAcceptance(uiService, results, token);
                if (!accepted)
                {
                    return;
                }

                if (!token.IsCancellationRequested)
                {
                    // execute the actions
                    await ExecuteActions(actions, uiService.ProgressWindow, userAction, token);

                    // update
                    uiService.RefreshPackageStatus();
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
        private async Task<bool> CheckLicenseAcceptance(
            INuGetUI uiService,
            IEnumerable<PreviewResult> results,
            CancellationToken token)
        {
            // find all the packages that might need a license acceptance
            HashSet<PackageIdentity> licenseCheck = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
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
            IEnumerable<UIPackageMetadata> licenseMetadata = await GetPackageMetadata(licenseCheck, token);

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
        /// Execute the installs/uninstalls
        /// </summary>
        /// <param name="actions"></param>
        /// <param name="projectContext"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        protected async Task ExecuteActions(IEnumerable<Tuple<NuGetProject, NuGetProjectAction>> actions,
            NuGetUIProjectContext projectContext, UserAction userAction, CancellationToken token)
        {
            HashSet<PackageIdentity> processedDirectInstalls = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
            foreach (var projectActions in actions.GroupBy(e => e.Item1))
            {
                var nuGetProjectActions = projectActions.Select(e => e.Item2);
                var directInstall = GetDirectInstall(nuGetProjectActions, userAction, projectContext.CommonOperations);
                if (directInstall != null && !processedDirectInstalls.Contains(directInstall))
                {
                    NuGetPackageManager.SetDirectInstall(directInstall, projectContext);
                    processedDirectInstalls.Add(directInstall);
                }
                await _packageManager.ExecuteNuGetProjectActionsAsync(projectActions.Key, nuGetProjectActions, projectContext, token);
                NuGetPackageManager.ClearDirectInstall(projectContext);
            }
        }

        private PackageIdentity GetDirectInstall(IEnumerable<NuGetProjectAction> nuGetProjectActions,
            UserAction userAction,
            ICommonOperations commonOperations)
        {
            if (commonOperations != null && userAction != null && userAction.Action == NuGetProjectActionType.Install && nuGetProjectActions.Any())
            {
                PackageIdentity directInstall = null;
                if(userAction.PackageIdentity != null)
                {
                    directInstall = userAction.PackageIdentity;
                }
                else
                {
                    var identitiesWithSameId = nuGetProjectActions.Where(n => n.PackageIdentity.Id.Equals(userAction)).ToList();
                    if(identitiesWithSameId.Count == 1)
                    {
                        directInstall = identitiesWithSameId[0].PackageIdentity;
                    }
                    // If there multiple actions with the same package id as the user action, just ignore
                }

                if (directInstall != null)
                {
                    return directInstall;
                }
            }

            return null;
        }

        /// <summary>
        /// Return the resolve package actions
        /// </summary>
        protected async Task<IEnumerable<Tuple<NuGetProject, NuGetProjectAction>>> GetActions(
            INuGetUI uiService,
            IEnumerable<NuGetProject> targets,
            UserAction userAction,
            bool removeDependencies,
            bool forceRemove,
            ResolutionContext resolutionContext,
            INuGetProjectContext projectContext,
            CancellationToken token)
        {
            List<Tuple<NuGetProject, NuGetProjectAction>> results = new List<Tuple<NuGetProject, NuGetProjectAction>>();

            Debug.Assert(userAction.PackageId != null, "Package id can never be null in a User action");
            if (userAction.Action == NuGetProjectActionType.Install)
            {
                Debug.Assert(userAction.PackageIdentity != null, "Package identity cannot be null when installing a package");

                foreach (var target in targets)
                {
                    IEnumerable<NuGetProjectAction> actions;
                    actions = await _packageManager.PreviewInstallPackageAsync(target, userAction.PackageIdentity,
                        resolutionContext, projectContext, uiService.ActiveSource, null, token);
                    results.AddRange(actions.Select(a => new Tuple<NuGetProject, NuGetProjectAction>(target, a)));
                }
            }
            else
            {
                UninstallationContext uninstallationContext = new UninstallationContext(
                    removeDependencies: removeDependencies,
                    forceRemove: forceRemove);

                foreach (var target in targets)
                {
                    IEnumerable<NuGetProjectAction> actions;
                    if (userAction.PackageIdentity != null)
                    {
                        actions = await _packageManager.PreviewUninstallPackageAsync(target, userAction.PackageIdentity, uninstallationContext, projectContext, token);
                    }
                    else
                    {
                        actions = await _packageManager.PreviewUninstallPackageAsync(target, userAction.PackageId, uninstallationContext, projectContext, token);
                    }
                    results.AddRange(actions.Select(a => new Tuple<NuGetProject, NuGetProjectAction>(target, a)));
                }
            }

            return results;
        }

        /// <summary>
        /// Convert NuGetProjectActions into PreviewResult types
        /// </summary>
        protected IEnumerable<PreviewResult> GetPreviewResults(IEnumerable<Tuple<NuGetProject, NuGetProjectAction>> projectActions)
        {
            List<PreviewResult> results = new List<PreviewResult>();
            var actionsByProject = projectActions.GroupBy(action => action.Item1);
            foreach (var actions in actionsByProject)
            {
                var installed = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);
                var uninstalled = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);
                var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var actionTuple in actions)
                {
                    packageIds.Add(actionTuple.Item2.PackageIdentity.Id);

                    if (actionTuple.Item2.NuGetProjectActionType == NuGetProjectActionType.Install)
                    {
                        installed[actionTuple.Item2.PackageIdentity.Id] = actionTuple.Item2.PackageIdentity;
                    }
                    else
                    {
                        uninstalled[actionTuple.Item2.PackageIdentity.Id] = actionTuple.Item2.PackageIdentity;
                    }
                }

                List<PackageIdentity> added = new List<PackageIdentity>();
                List<PackageIdentity> deleted = new List<PackageIdentity>();
                List<UpdatePreviewResult> updated = new List<UpdatePreviewResult>();
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

                PreviewResult result = new PreviewResult(actions.Key, added, deleted, updated);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Get the package metadata to see if RequireLicenseAcceptance is true
        /// </summary>
        private async Task<List<UIPackageMetadata>> GetPackageMetadata(IEnumerable<PackageIdentity> packages, CancellationToken token)
        {
            var sources = _sourceProvider.GetRepositories().Where(e => e.PackageSource.IsEnabled);

            List<UIPackageMetadata> results = new List<UIPackageMetadata>();
            foreach (var package in packages)
            {
                var metadata = await GetPackageMetadata(sources, package, token);
                if (metadata == null)
                {
                    throw new InvalidOperationException(
                        string.Format("Unable to find metadata of {0}", package));
                }

                results.Add(metadata);
            }

            return results;
        }

        private async Task<UIPackageMetadata> GetPackageMetadata(
            IEnumerable<SourceRepository> sources,
            PackageIdentity package,
            CancellationToken token)
        {
            foreach (var source in sources)
            {
                var metadataResource = source.GetResource<UIMetadataResource>();
                if (metadataResource == null)
                {
                    continue;
                }

                var r = await metadataResource.GetMetadata(
                    package.Id,
                    includePrerelease: true,
                    includeUnlisted: true,
                    token: token);
                var packageMetadata = r.FirstOrDefault(p => p.Identity.Version == package.Version);
                if (packageMetadata != null)
                {
                    return packageMetadata;
                }
            }

            return null;
        }
    }
}