// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;

namespace NuGet.PackageManagement.UI
{
    internal static class NuGetProjectUpgrader
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
            var msBuildNuGetProjectSystem = msBuildNuGetProject.MSBuildNuGetProjectSystem;
            var projectFullPath = msBuildNuGetProjectSystem.ProjectFileName;
            var projectFileName = Path.GetFileName(projectFullPath);
            File.Copy(projectFullPath, Path.Combine(backupPath, projectFileName), true);

            // 2. Uninstall all packages currently in packages.config
            var progressData = new ProgressDialogData(Resources.NuGetUpgrade_WaitMessage, Resources.NuGetUpgrade_Progress_Uninstalling);
            progress.Report(progressData);

            // Don't uninstall packages we couldn't find - that will just fail
            var actions = dependencyItems.Select(d => d.Package)
                .Where(p => !notFoundPackages.Contains(p))
                .Select(NuGetProjectAction.CreateUninstallProjectAction);

            // TODO: How should we handle a failure in uninstalling a package (unfortunately ExecuteNuGetProjectActionsAsync()
            // doesn't give us any useful information about the failure).
            await context.PackageManager.ExecuteNuGetProjectActionsAsync(nuGetProject, actions, uiService.ProgressWindow, CancellationToken.None);

            // If there were packages we didn't uninstall because we couldn't find them, they will still be present in
            // packages.config, so we'll have to delete that file now.
            if (File.Exists(packagesConfigFullPath))
            {
                FileSystemUtility.DeleteFile(packagesConfigFullPath, uiService.ProgressWindow);
                msBuildNuGetProjectSystem.RemoveFile(Path.GetFileName(packagesConfigFullPath));
            }

            // 3. Create stub project.json file
            progressData = new ProgressDialogData(Resources.NuGetUpgrade_WaitMessage, Resources.NuGetUpgrade_Progress_CreatingProjectJson);
            progress.Report(progressData);

            var json = new JObject();

            // Target framework
            var targetNuGetFramework = msBuildNuGetProjectSystem.TargetFramework;
            json["frameworks"] = new JObject {[targetNuGetFramework.GetShortFolderName()] = new JObject()};

            // Runtimes
            var runtimeStub = msBuildNuGetProjectSystem.GetPropertyValue("TargetPlatformIdentifier") == "UAP"
                ? "win10"
                : "win";
            var runtimes = new JObject();
            var supportedPlatforms = msBuildNuGetProjectSystem.SupportedPlatforms;

            if (supportedPlatforms.Any())
            {
                foreach (var supportedPlatformString in supportedPlatforms)
                {
                    if (string.IsNullOrEmpty(supportedPlatformString) || supportedPlatformString == "Any CPU")
                    {
                        runtimes[runtimeStub] = new JObject();
                    }
                    else
                    {
                        runtimes[runtimeStub + "-" + supportedPlatformString.ToLowerInvariant()] = new JObject();
                    }
                }
            }
            else
            {
                runtimes[runtimeStub] = new JObject();
            }

            json["runtimes"] = runtimes;

            // Write out project.json and add it to the project
            var projectJsonFileName = Path.Combine(msBuildNuGetProjectSystem.ProjectFullPath, PackageSpec.PackageSpecFileName);
            WriteProjectJson(projectJsonFileName, json);
            msBuildNuGetProjectSystem.AddExistingFile(PackageSpec.PackageSpecFileName);

            // Reload the project, and get a reference to the reloaded project
            var uniqueName = msBuildNuGetProjectSystem.ProjectUniqueName;
            solutionManager.SaveProject(nuGetProject);
            solutionManager.ReloadProject(nuGetProject);
            nuGetProject = solutionManager.GetNuGetProject(uniqueName);

            // Ensure we use the updated project for installing, and don't display preview or license acceptance windows.
            context.Projects = new[] {nuGetProject};
            var nuGetUI = (NuGetUI) uiService;
            nuGetUI.Projects = new[] {nuGetProject};
            nuGetUI.DisplayPreviewWindow = false;
            nuGetUI.DisplayLicenseAcceptanceWindow = false;

            // 4. Install the requested packages
            var ideExecutionContext = uiService.ProgressWindow.ExecutionContext as IDEExecutionContext;
            if (ideExecutionContext != null)
            {
                await ideExecutionContext.SaveExpandedNodeStates(solutionManager);
            }

            progressData = new ProgressDialogData(Resources.NuGetUpgrade_WaitMessage, Resources.NuGetUpgrade_Progress_Installing);
            progress.Report(progressData);

            var packagesToInstall = GetPackagesToInstall(dependencyItems, collapseDependencies).ToList();
            foreach (var packageIdentity in packagesToInstall)
            {
                var action = UserAction.CreateInstallAction(packageIdentity.Id, packageIdentity.Version);
                await context.UIActionEngine.PerformActionAsync(uiService, action, null, CancellationToken.None);
            }

            // If any packages didn't install, manually add them to project.json and let the user deal with it.
            var installedPackages = (await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None)).Select(p => p.PackageIdentity);
            var notInstalledPackages = packagesToInstall.Except(installedPackages).ToList();
            if (notInstalledPackages.Any())
            {
                using (var stream = new FileStream(projectJsonFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var reader = new JsonTextReader(new StreamReader(stream));
                    json = JObject.Load(reader);
                    var dependencies = json["dependencies"];
                    foreach (var notInstalledPackage in notInstalledPackages)
                    {
                        dependencies[notInstalledPackage.Id] = notInstalledPackage.Version.ToNormalizedString();
                    }
                }
                WriteProjectJson(projectJsonFileName, json);
            }

            if (ideExecutionContext != null)
            {
                await ideExecutionContext.CollapseAllNodes(solutionManager);
            }

            return backupPath;
        }

        private static void WriteProjectJson(string projectJsonFileName, JToken json)
        {
            using (var textWriter = new StreamWriter(projectJsonFileName, false, Encoding.UTF8))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;
                json.WriteTo(jsonWriter);
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