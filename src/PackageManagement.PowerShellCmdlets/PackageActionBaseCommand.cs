using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using System;
using System.Management.Automation;
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
            GetActiveSourceRepository(Source);
            GetNuGetProject(ProjectName);
            DetermineFileConflictAction();
        }

        protected async Task InstallPackageByIdentityAsync(NuGetProject project, PackageIdentity identity, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview, bool isForce = false, UninstallationContext uninstallContext = null)
        {
            if (isPreview)
            {
                if (isForce)
                {
                    await PackageManager.PreviewUninstallPackageAsync(project, identity.Id, uninstallContext, projectContext);
                }
                await PackageManager.PreviewInstallPackageAsync(project, identity, resolutionContext, projectContext);
            }
            else
            {
                if (isForce)
                {
                    await PackageManager.UninstallPackageAsync(project, identity.Id, uninstallContext, projectContext);
                }
                await PackageManager.InstallPackageAsync(project, identity, resolutionContext, projectContext);
            }
        }

        protected async Task InstallPackageById(NuGetProject project, string packageId, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview, bool isForce = false, UninstallationContext uninstallContext = null)
        {
            if (isPreview)
            {
                if (isForce)
                {
                    await PackageManager.PreviewUninstallPackageAsync(project, packageId, uninstallContext, projectContext);
                }
                await PackageManager.PreviewInstallPackageAsync(project, packageId, resolutionContext, projectContext);
            }
            else
            {
                if (isForce)
                {
                    await PackageManager.UninstallPackageAsync(project, packageId, uninstallContext, projectContext);
                }
                await PackageManager.InstallPackageAsync(project, packageId, resolutionContext, projectContext);
            }
        }

        private void DetermineFileConflictAction()
        {
            if (FileConflictAction != null)
            {
                this.ConflictAction = FileConflictAction;
            }
        }

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
