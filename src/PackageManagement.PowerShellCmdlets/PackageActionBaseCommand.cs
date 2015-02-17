using NuGet.Client.VisualStudio;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public class PackageActionBaseCommand : NuGetPowerShellBaseCommand
    {
        public PackageActionBaseCommand()
            : base()
        {
        }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0)]
        public virtual string Id { get; set; }

        [Parameter(ValueFromPipelineByPropertyName = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public virtual string ProjectName { get; set; }

        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty]
        public virtual string Version { get; set; }

        [Parameter(Position = 3)]
        [ValidateNotNullOrEmpty]
        public virtual string Source { get; set; }

        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        [Parameter, Alias("Prerelease")]
        public SwitchParameter IncludePrerelease { get; set; }

        [Parameter]
        public SwitchParameter IgnoreDependencies { get; set; }

        [Parameter]
        public FileConflictAction? FileConflictAction { get; set; }

        [Parameter]
        public DependencyBehavior? DependencyVersion { get; set; }

        /// <summary>
        /// Derived classess must implement this method instead of ProcessRecord(), which is sealed by NuGetBaseCmdlet.
        /// </summary>
        protected override void ProcessRecordCore()
        {
            Preprocess();
            CheckForSolutionOpen();
        }

        protected override void Preprocess()
        {
            base.Preprocess();
            UpdateActiveSourceRepository(Source);
            GetNuGetProject(ProjectName);
            DetermineFileConflictAction();
        }

        /// <summary>
        /// Install package by Identity
        /// </summary>
        /// <param name="project"></param>
        /// <param name="identity"></param>
        /// <param name="resolutionContext"></param>
        /// <param name="projectContext"></param>
        /// <param name="isPreview"></param>
        /// <param name="isForce"></param>
        /// <param name="uninstallContext"></param>
        /// <returns></returns>
        protected async Task InstallPackageByIdentityAsync(NuGetProject project, PackageIdentity identity, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview, bool isForce = false, UninstallationContext uninstallContext = null)
        {
            if (isPreview)
            {
                List<NuGetProjectAction> actions = new List<NuGetProjectAction>();
                if (isForce)
                {
                    actions.AddRange(await PackageManager.PreviewUninstallPackageAsync(project, identity.Id, uninstallContext, projectContext, CancellationToken.None));
                    // Temporarily work around the issue of Install -Force -WhatIf, where the package was not actually uninstalled in the first step.
                    // TODO: Fix the logic and add the concept of Force Install to Resolver after Beta. And Remove the hack here.
                    NuGetProjectAction installAction = NuGetProjectAction.CreateInstallProjectAction(identity, ActiveSourceRepository);
                    actions.Add(installAction);
                }
                else
                {
                    actions.AddRange(await PackageManager.PreviewInstallPackageAsync(project, identity, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None));
                }
                PreviewNuGetPackageActions(actions);
            }
            else
            {
                if (isForce)
                {
                    await PackageManager.UninstallPackageAsync(project, identity.Id, uninstallContext, projectContext, CancellationToken.None);
                }
                await PackageManager.InstallPackageAsync(project, identity, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None);
            }
        }

        /// <summary>
        /// Install package by Id
        /// </summary>
        /// <param name="project"></param>
        /// <param name="packageId"></param>
        /// <param name="resolutionContext"></param>
        /// <param name="projectContext"></param>
        /// <param name="isPreview"></param>
        /// <param name="isForce"></param>
        /// <param name="uninstallContext"></param>
        /// <returns></returns>
        protected async Task InstallPackageByIdAsync(NuGetProject project, string packageId, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview, bool isForce = false, UninstallationContext uninstallContext = null)
        {
            if (isPreview)
            {
                List<NuGetProjectAction> actions = new List<NuGetProjectAction>();
                if (isForce)
                {
                    actions.AddRange(await PackageManager.PreviewUninstallPackageAsync(project, packageId, uninstallContext, projectContext, CancellationToken.None));
                    // Temporarily work around the issue of Install -Force -WhatIf, where the package was not uninstalled in the first step.
                    // TODO: Fix the logic and add the concept of Force Install to Resolver after Beta. And Remove the hack here.
                    PackageReference identity = project.GetInstalledPackagesAsync(CancellationToken.None).Result.Where(p =>
                        StringComparer.OrdinalIgnoreCase.Equals(packageId, p.PackageIdentity.Id)).FirstOrDefault();
                    NuGetProjectAction installAction = NuGetProjectAction.CreateInstallProjectAction(identity.PackageIdentity, ActiveSourceRepository);
                    actions.Add(installAction);
                }
                else
                {
                    actions.AddRange(await PackageManager.PreviewInstallPackageAsync(project, packageId, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None));
                }
                PreviewNuGetPackageActions(actions);
            }
            else
            {
                if (isForce)
                {
                    await PackageManager.UninstallPackageAsync(project, packageId, uninstallContext, projectContext, CancellationToken.None);
                }
                await PackageManager.InstallPackageAsync(project, packageId, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None);
            }
        }

        /// <summary>
        /// Normalize package Id input against server metadata for project K, which is case-sensitive.
        /// </summary>
        /// <param name="project"></param>
        protected void NormalizePackageId(NuGetProject project)
        {
            if (project is ProjectManagement.Projects.ProjectKNuGetProjectBase)
            {
                IEnumerable<string> frameworks = Enumerable.Empty<string>();
                PSSearchMetadata match = GetPackagesFromRemoteSource(Id, frameworks, true, 0, 30)
                    .Where(p => StringComparer.OrdinalIgnoreCase.Equals(Id, p.Identity.Id))
                    .FirstOrDefault();
                Id = match.Identity.Id;
            }
        }

        /// <summary>
        /// Determine file confliction action based on user input
        /// </summary>
        private void DetermineFileConflictAction()
        {
            if (FileConflictAction != null)
            {
                this.ConflictAction = FileConflictAction;
            }
        }

        /// <summary>
        /// Determine DependencyBehavior based on user input
        /// </summary>
        /// <returns></returns>
        protected virtual DependencyBehavior GetDependencyBehavior()
        {
            if (IgnoreDependencies.IsPresent)
            {
                return DependencyBehavior.Ignore;
            }
            else if (DependencyVersion.HasValue)
            {
                return DependencyVersion.Value;
            }
            else
            {
                return GetDependencyBehaviorFromConfig();
            }
        }

        /// <summary>
        /// Get the value of DependencyBehavior from NuGet.Config file
        /// </summary>
        /// <returns></returns>
        protected DependencyBehavior GetDependencyBehaviorFromConfig()
        {
            string dependencySetting = ConfigSettings.GetValue("config", "dependencyversion");
            DependencyBehavior behavior;
            bool success = Enum.TryParse<DependencyBehavior>(dependencySetting, true, out behavior);
            if (success)
            {
                return behavior;
            }
            else
            {
                // Default to Lowest
                return DependencyBehavior.Lowest;
            }
        }
    }
}
