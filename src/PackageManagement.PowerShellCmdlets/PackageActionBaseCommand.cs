using NuGet.Client;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using System;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public class PackageActionBaseCommand : NuGetPowerShellBaseCommand
    {
        private ISolutionManager _solutionManager;

        public PackageActionBaseCommand(
            Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] resourceProvider,
            ISolutionManager solutionManager)
            : base(resourceProvider, solutionManager)
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
            CheckForSolutionOpen();
            Preprocess();
        }

        protected override void Preprocess()
        {
            GetSourceRepositoryProvider(Source);
            PackageManager = new NuGetPackageManager(SourceRepositoryProvider);
            GetNuGetProject(ProjectName);
            DetermineFileConflictAction();
            base.Preprocess();
        }

        protected void InstallPackageByIdentity(NuGetProject project, PackageIdentity identity, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview, bool isForce = false)
        {
            if (isPreview)
            {
                if (isForce)
                {
                    PackageManager.PreviewUninstallPackageAsync(project, identity.Id, resolutionContext, projectContext).Wait();
                }
                PackageManager.PreviewInstallPackageAsync(project, identity, resolutionContext, projectContext).Wait();
            }
            else
            {
                if (isForce)
                {
                    PackageManager.UninstallPackageAsync(project, identity.Id, resolutionContext, projectContext).Wait();
                }
                PackageManager.InstallPackageAsync(project, identity, resolutionContext, projectContext).Wait();
            }
        }

        protected void InstallPackageById(NuGetProject project, string packageId, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview, bool isForce = false)
        {
            if (isPreview)
            {
                if (isForce)
                {
                    PackageManager.PreviewUninstallPackageAsync(project, packageId, resolutionContext, projectContext).Wait();
                }
                PackageManager.PreviewInstallPackageAsync(project, packageId, resolutionContext, projectContext).Wait();
            }
            else
            {
                if (isForce)
                {
                    PackageManager.UninstallPackageAsync(project, packageId, resolutionContext, projectContext).Wait();
                }
                PackageManager.InstallPackageAsync(project, packageId, resolutionContext, projectContext).Wait();
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
