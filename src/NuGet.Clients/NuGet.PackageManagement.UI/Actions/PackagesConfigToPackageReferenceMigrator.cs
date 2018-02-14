// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    internal static class PackagesConfigToPackageReferenceMigrator
    {
        internal static async Task<string> DoUpgradeAsync(
            INuGetUIContext context,
            INuGetUI uiService,
            NuGetProject nuGetProject,
            IEnumerable<NuGetProjectUpgradeDependencyItem> upgradeDependencyItems,
            IEnumerable<PackageIdentity> notFoundPackages,
            bool collapseDependencies,
            IProgress<ProgressDialogData> progress,
            CancellationToken token)
        {
            var dependencyItems = upgradeDependencyItems as IList<NuGetProjectUpgradeDependencyItem> ?? upgradeDependencyItems.ToList();

            var startTime = DateTimeOffset.Now;
            var packagesCount = 0;
            var status = NuGetOperationStatus.Succeeded;


            using (var telemetry = new TelemetryActivity(Guid.Empty))
            {
                try
                {
                    // 1. Backup files that will change
                    var solutionManager = context.SolutionManager;
                    var solutionDirectory = solutionManager.SolutionDirectory;
                    var backupPath = Path.Combine(solutionDirectory, "Backup", NuGetProject.GetUniqueNameOrName(nuGetProject));
                    Directory.CreateDirectory(backupPath);

                    // Backup packages.config
                    var msBuildNuGetProject = (MSBuildNuGetProject)nuGetProject;
                    var packagesConfigFullPath = msBuildNuGetProject.PackagesConfigNuGetProject.FullPath;
                    var packagesConfigFileName = Path.GetFileName(packagesConfigFullPath);
                    File.Copy(packagesConfigFullPath, Path.Combine(backupPath, packagesConfigFileName), true);

                    // Backup project file
                    var msBuildNuGetProjectSystem = msBuildNuGetProject.ProjectSystem;
                    var projectFullPath = msBuildNuGetProjectSystem.ProjectFileFullPath;
                    var projectFileName = Path.GetFileName(projectFullPath);
                    File.Copy(projectFullPath, Path.Combine(backupPath, projectFileName), true);

                    // 2. Uninstall all packages currently in packages.config
                    var progressData = new ProgressDialogData(Resources.NuGetUpgrade_WaitMessage, Resources.NuGetUpgrade_Progress_Uninstalling);
                    progress.Report(progressData);

                    // Don't uninstall packages we couldn't find - that will just fail
                    var actions = dependencyItems.Select(d => d.Package)
                        .Where(p => !notFoundPackages.Contains(p))
                        .Select(t => NuGetProjectAction.CreateUninstallProjectAction(t, nuGetProject));

                    try
                    {
                        await context.PackageManager.ExecuteNuGetProjectActionsAsync(nuGetProject, actions, uiService.ProjectContext, NullSourceCacheContext.Instance, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        status = NuGetOperationStatus.Failed;
                        // log error message
                        uiService.ShowError(ex);
                        uiService.ProjectContext.Log(MessageLevel.Info,
                            string.Format(CultureInfo.CurrentCulture, Resources.Upgrade_UninstallFailed));

                        // delete backup directory
                        Directory.Delete(backupPath);

                        return null;
                    }

                    // Reload the project, and get a reference to the reloaded project
                    var uniqueName = msBuildNuGetProjectSystem.ProjectUniqueName;
                    await msBuildNuGetProject.SaveAsync(token);

                    //solutionManager.ReloadProject(nuGetProject);
                    nuGetProject = await solutionManager.GetNuGetProjectAsync(uniqueName);
                    nuGetProject = await solutionManager.UpgradeProjectToPackageReferenceAsync(nuGetProject);

                    // Ensure we use the updated project for installing, and don't display preview or license acceptance windows.
                    context.Projects = new[] { nuGetProject };
                    var nuGetUI = (NuGetUI)uiService;
                    nuGetUI.Projects = new[] { nuGetProject };
                    nuGetUI.DisplayPreviewWindow = false;

                    // 4. Install the requested packages
                    var ideExecutionContext = uiService.ProjectContext.ExecutionContext as IDEExecutionContext;
                    if (ideExecutionContext != null)
                    {
                        await ideExecutionContext.SaveExpandedNodeStates(solutionManager);
                    }

                    progressData = new ProgressDialogData(Resources.NuGetUpgrade_WaitMessage, Resources.NuGetUpgrade_Progress_Installing);
                    progress.Report(progressData);
                    var activeSources = new List<SourceRepository>();
                    PackageSourceMoniker
                        .PopulateList(context.SourceProvider)
                        .ForEach(s => activeSources.AddRange(s.SourceRepositories));
                    var packagesToInstall = GetPackagesToInstall(dependencyItems, collapseDependencies).ToList();
                    packagesCount = packagesToInstall.Count;

                    // create low level NuGet actions based on number of packages being installed
                    var lowLevelActions = new List<NuGetProjectAction>();
                    foreach (var packageIdentity in packagesToInstall)
                    {
                        lowLevelActions.Add(NuGetProjectAction.CreateInstallProjectAction(packageIdentity, activeSources.FirstOrDefault(), nuGetProject));
                    }

                    try
                    {
                        var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;
                        await context.PackageManager.ExecuteBuildIntegratedProjectActionsAsync(
                            buildIntegratedProject,
                            lowLevelActions,
                            uiService.ProjectContext,
                            token);

                        if (ideExecutionContext != null)
                        {
                            await ideExecutionContext.CollapseAllNodes(solutionManager);
                        }

                        return backupPath;
                    }
                    catch (Exception ex)
                    {
                        status = NuGetOperationStatus.Failed;

                        uiService.ShowError(ex);
                        uiService.ProjectContext.Log(MessageLevel.Info,
                                string.Format(CultureInfo.CurrentCulture, Resources.Upgrade_InstallFailed, backupPath));
                        uiService.ProjectContext.Log(MessageLevel.Info,
                            string.Format(CultureInfo.CurrentCulture, Resources.Upgrade_RevertSteps, "https://aka.ms/nugetupgraderevertv1"));

                        return null;
                    }

                   
                }
                finally
                {
                    telemetry.TelemetryEvent = VSTelemetryServiceUtility.GetUpgradeTelemetryEvent(
                            uiService.Projects,
                            status,
                            packagesCount);
                }
            }
        }

        private static IEnumerable<PackageIdentity> GetPackagesToInstall(
            IEnumerable<NuGetProjectUpgradeDependencyItem> upgradeDependencyItems, bool collapseDependencies)
        {
            return
                upgradeDependencyItems.Where(
                    upgradeDependencyItem => !collapseDependencies || !upgradeDependencyItem.DependingPackages.Any())
                    .Select(upgradeDependencyItem => upgradeDependencyItem.Package);
        }
    }
}
