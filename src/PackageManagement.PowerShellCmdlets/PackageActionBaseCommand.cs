using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.VisualStudio;
using NuGet.Resolver;
using NuGet.Versioning;

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

        protected override void Preprocess()
        {
            base.Preprocess();
            CheckForSolutionOpen();
            UpdateActiveSourceRepository(Source);
            GetNuGetProject(ProjectName);
            DetermineFileConflictAction();
        }

        protected override void ProcessRecordCore()
        {
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
            try
            {
                List<NuGetProjectAction> actions = new List<NuGetProjectAction>();
                // For Install-Package -Force
                if (isForce)
                {
                    PackageReference installedReference = project.GetInstalledPackagesAsync(CancellationToken.None).Result.Where(p =>
                        StringComparer.OrdinalIgnoreCase.Equals(identity.Id, p.PackageIdentity.Id)).FirstOrDefault();
                    if (installedReference != null)
                    {
                        actions.AddRange(await PackageManager.PreviewUninstallPackageAsync(project, installedReference.PackageIdentity, uninstallContext, projectContext, CancellationToken.None));
                    }
                    NuGetProjectAction installAction = NuGetProjectAction.CreateInstallProjectAction(identity, ActiveSourceRepository);
                    actions.Add(installAction);
                }
                else
                {
                    actions.AddRange(await PackageManager.PreviewInstallPackageAsync(project, identity, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None));
                }

                if (isPreview)
                {
                    PreviewNuGetPackageActions(actions);
                }
                else
                {
                    await PackageManager.ExecuteNuGetProjectActionsAsync(project, actions, this, CancellationToken.None);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is PackageAlreadyInstalledException)
                {
                    Log(MessageLevel.Info, ex.Message);
                }
                else
                {
                    throw ex;
                }
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
            try
            {
                List<NuGetProjectAction> actions = new List<NuGetProjectAction>();
                // For Install-Package -Force
                if (isForce)
                {
                    PackageReference installedReference = project.GetInstalledPackagesAsync(CancellationToken.None).Result.Where(p =>
                        StringComparer.OrdinalIgnoreCase.Equals(packageId, p.PackageIdentity.Id)).FirstOrDefault();
                    if (installedReference != null)
                    {
                        actions.AddRange(await PackageManager.PreviewUninstallPackageAsync(project, packageId, uninstallContext, projectContext, CancellationToken.None));
                    }
                    NuGetVersion nVersion = PowerShellCmdletsUtility.GetLastestVersionForPackageId(ActiveSourceRepository, packageId, project, resolutionContext.IncludePrerelease);
                    if (nVersion != null)
                    {
                        PackageIdentity identityToInstall = new PackageIdentity(packageId, nVersion);
                        NuGetProjectAction installAction = NuGetProjectAction.CreateInstallProjectAction(identityToInstall, ActiveSourceRepository);
                        actions.Add(installAction);
                    }
                }
                else
                {
                    actions.AddRange(await PackageManager.PreviewInstallPackageAsync(project, packageId, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None));
                }

                if (isPreview)
                {
                    PreviewNuGetPackageActions(actions);
                }
                else
                {
                    await PackageManager.ExecuteNuGetProjectActionsAsync(project, actions, this, CancellationToken.None);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is PackageAlreadyInstalledException)
                {
                    Log(MessageLevel.Info, ex.Message);
                }
                else
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Normalize package Id input against server metadata for project K, which is case-sensitive.
        /// </summary>
        /// <param name="project"></param>
        protected void NormalizePackageId(NuGetProject project)
        {
            if (!(project is ProjectManagement.Projects.ProjectKNuGetProjectBase))
            {
                return;
            }

            var resource = ActiveSourceRepository.GetResource<UIMetadataResource>();
            if (resource == null)
            {
                return;
            }

            var metadata = resource.GetMetadata(
                Id,
                includePrerelease: true,
                includeUnlisted: false,
                token: CancellationToken.None).Result;
            if (!metadata.Any())
            {
                return;
            }

            // note that we're assuming that package id is the same for all versions.
            Id = metadata.First().Identity.Id;
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