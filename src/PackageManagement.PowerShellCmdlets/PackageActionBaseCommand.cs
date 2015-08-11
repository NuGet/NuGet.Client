// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.VisualStudio;
using NuGet.Resolver;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public class PackageActionBaseCommand : NuGetPowerShellBaseCommand
    {
        private IDeleteOnRestartManager _deleteOnRestartManager;

        public PackageActionBaseCommand()
        {
            _deleteOnRestartManager = ServiceLocator.GetInstance<IDeleteOnRestartManager>();
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

        [Parameter]
        [Alias("Prerelease")]
        public SwitchParameter IncludePrerelease { get; set; }

        [Parameter]
        public SwitchParameter IgnoreDependencies { get; set; }

        [Parameter]
        public FileConflictAction? FileConflictAction { get; set; }

        [Parameter]
        public DependencyBehavior? DependencyVersion { get; set; }

        protected virtual void Preprocess()
        {
            CheckSolutionState();
            UpdateActiveSourceRepository(Source);
            GetNuGetProject(ProjectName);
            DetermineFileConflictAction();
            ThreadHelper.JoinableTaskFactory.Run(CheckMissingPackagesAsync);
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
        protected async Task InstallPackageByIdentityAsync(NuGetProject project, PackageIdentity identity, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview)
        {
            try
            {
                var actions = await PackageManager.PreviewInstallPackageAsync(project, identity, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None);

                if (isPreview)
                {
                    PreviewNuGetPackageActions(actions);
                }
                else
                {
                    NuGetPackageManager.SetDirectInstall(identity, projectContext);
                    await PackageManager.ExecuteNuGetProjectActionsAsync(project, actions, this, CancellationToken.None);
                    NuGetPackageManager.ClearDirectInstall(projectContext);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is PackageAlreadyInstalledException)
                {
                    Log(ProjectManagement.MessageLevel.Info, ex.Message);
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
        protected async Task InstallPackageByIdAsync(NuGetProject project, string packageId, ResolutionContext resolutionContext, INuGetProjectContext projectContext, bool isPreview)
        {
            try
            {
                var actions = await PackageManager.PreviewInstallPackageAsync(project, packageId, resolutionContext, projectContext, ActiveSourceRepository, null, CancellationToken.None);

                if (isPreview)
                {
                    PreviewNuGetPackageActions(actions);
                }
                else
                {
                    var identity = actions.Select(v => v.PackageIdentity).Where(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    NuGetPackageManager.SetDirectInstall(identity, projectContext);
                    await PackageManager.ExecuteNuGetProjectActionsAsync(project, actions, this, CancellationToken.None);
                    NuGetPackageManager.ClearDirectInstall(projectContext);
                }
            }
            catch (InvalidOperationException ex)
            {
                if (ex.InnerException is PackageAlreadyInstalledException)
                {
                    Log(ProjectManagement.MessageLevel.Info, ex.Message);
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
            if (!(project is ProjectKNuGetProjectBase))
            {
                return;
            }

            var resource = ActiveSourceRepository.GetResource<UIMetadataResource>();
            if (resource == null)
            {
                return;
            }

            var metadata = ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var result = await resource.GetMetadata(
                    Id,
                    includePrerelease: true,
                    includeUnlisted: false,
                    token: Token);
                return result;
            });

            if (!metadata.Any())
            {
                return;
            }

            // note that we're assuming that package id is the same for all versions.
            Id = metadata.First().Identity.Id;
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();

            var packageDirectoriesMarkedForDeletion = _deleteOnRestartManager.GetPackageDirectoriesMarkedForDeletion();
            if (packageDirectoriesMarkedForDeletion != null && packageDirectoriesMarkedForDeletion.Count != 0)
            {
                _deleteOnRestartManager.CheckAndRaisePackageDirectoriesMarkedForDeletion();
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Cmdlet_RequestRestartToCompleteUninstall,
                    string.Join(", ", packageDirectoriesMarkedForDeletion));
                WriteWarning(message);
            }
        }

        /// <summary>
        /// Determine file confliction action based on user input
        /// </summary>
        private void DetermineFileConflictAction()
        {
            if (FileConflictAction != null)
            {
                ConflictAction = FileConflictAction;
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
            if (DependencyVersion.HasValue)
            {
                return DependencyVersion.Value;
            }
            return GetDependencyBehaviorFromConfig();
        }

        /// <summary>
        /// Get the value of DependencyBehavior from NuGet.Config file
        /// </summary>
        /// <returns></returns>
        protected DependencyBehavior GetDependencyBehaviorFromConfig()
        {
            string dependencySetting = ConfigSettings.GetValue("config", "dependencyversion");
            DependencyBehavior behavior;
            bool success = Enum.TryParse(dependencySetting, true, out behavior);
            if (success)
            {
                return behavior;
            }
            // Default to Lowest
            return DependencyBehavior.Lowest;
        }
    }
}
