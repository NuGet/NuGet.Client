using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Performs package manager actions and controls the UI to display output while the actions are taking place.
    /// </summary>
    public class UIActionEngine
    {
        private readonly SourceRepositoryProvider _sourceProvider;
        private readonly NuGetPackageManager _packageManager;

        /// <summary>
        /// Create a UIActionEngine to perform installs/uninstalls
        /// </summary>
        public UIActionEngine(SourceRepositoryProvider sourceProvider, NuGetPackageManager packageManager)
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

                // get metadata for license check
                HashSet<PackageIdentity> licenseCheck = new HashSet<PackageIdentity>(PackageIdentity.Comparer);

                IEnumerable<Tuple<NuGetProject, NuGetProjectAction>> actions = await GetActions(projects, userAction, uiService.FileConflictAction, uiService.ProgressWindow, token);
                IEnumerable<PreviewResult> results = await GetPreviewResults(actions);

                // find all the packages that might need a license acceptance
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

                // show license agreement
                if (licenseMetadata.Any(e => e.RequireLicenseAcceptance))
                {
                    var licenseInfoItems = licenseMetadata.Where(p => p.RequireLicenseAcceptance).Select(e => new PackageLicenseInfo(e.Identity.Id, e.LicenseUrl, e.Authors));

                    bool acceptLicense = false;

                    acceptLicense = uiService.PromptForLicenseAcceptance(licenseInfoItems);

                    if (!acceptLicense)
                    {
                        return;
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    // execute the actions
                    await ExecuteActions(actions, uiService.ProgressWindow, token);

                    // update
                    uiService.RefreshPackageStatus();
                }
            }
            catch (Exception ex)
            {
                uiService.ShowError(ex.Message, ex.ToString());
            }
            finally
            {
                uiService.CloseProgressDialog();
            }
        }

        /// <summary>
        /// Execute the installs/uninstalls
        /// </summary>
        /// <param name="actions"></param>
        /// <param name="projectContext"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        protected async Task ExecuteActions(IEnumerable<Tuple<NuGetProject, NuGetProjectAction>> actions, INuGetProjectContext projectContext, CancellationToken token)
        {
            foreach (var projectActions in actions.GroupBy(e => e.Item1))
            {
                await _packageManager.ExecuteNuGetProjectActionsAsync(projectActions.Key, projectActions.Select(e => e.Item2), projectContext);
            }
        }

        /// <summary>
        /// Return the resolve package actions
        /// </summary>
        protected async Task<IEnumerable<Tuple<NuGetProject, NuGetProjectAction>>> GetActions(IEnumerable<NuGetProject> targets, UserAction action,
            FileConflictAction conflictActionItem, INuGetProjectContext projectContext, CancellationToken token)
        {
            ResolutionContext resolutionContext = new ResolutionContext(DependencyBehavior.Lowest);

            List<Tuple<NuGetProject, NuGetProjectAction>> results = new List<Tuple<NuGetProject, NuGetProjectAction>>();

            foreach (var target in targets)
            {
                var actions = await _packageManager.PreviewInstallPackageAsync(target, action.PackageIdentity, resolutionContext, projectContext);

                foreach (var targetAction in actions)
                {
                    results.Add(new Tuple<NuGetProject, NuGetProjectAction>(target, targetAction));
                }
            }

            return results;
        }


        /// <summary>
        /// Convert NuGetProjectActions into PreviewResult types
        /// </summary>
        protected async Task<IEnumerable<PreviewResult>> GetPreviewResults(IEnumerable<Tuple<NuGetProject, NuGetProjectAction>> projectActions)
        {
            List<PreviewResult> results = new List<PreviewResult>();

            foreach (var actionTuple in projectActions)
            {
                List<PackageIdentity> added = new List<PackageIdentity>();
                List<PackageIdentity> deleted = new List<PackageIdentity>();
                List<PackageIdentity> unchanged = new List<PackageIdentity>();
                List<UpdatePreviewResult> updated = new List<UpdatePreviewResult>();

                if (actionTuple.Item2.NuGetProjectActionType == NuGetProjectActionType.Install)
                {
                    added.Add(actionTuple.Item2.PackageIdentity);
                }
                else
                {
                    deleted.Add(actionTuple.Item2.PackageIdentity);
                }

                PreviewResult result = new PreviewResult(actionTuple.Item1, added, deleted, unchanged, updated);
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

            HashSet<PackageIdentity> toFind = new HashSet<PackageIdentity>(packages, PackageIdentity.Comparer);

            List<UIPackageMetadata> results = new List<UIPackageMetadata>();

            foreach (var source in sources)
            {
                if (toFind.Count > 0)
                {
                    var metadataResource = source.GetResource<UIMetadataResource>();

                    var sourceResults = await metadataResource.GetMetadata(toFind, true, true, token);

                    foreach (var package in sourceResults)
                    {
                        if (toFind.Remove(package.Identity))
                        {
                            results.Add(package);
                        }
                    }
                }
            }

            if (toFind.Count > 0)
            {
                throw new Exception("Unable to find packages");
            }

            return results;
        }
    }
}
