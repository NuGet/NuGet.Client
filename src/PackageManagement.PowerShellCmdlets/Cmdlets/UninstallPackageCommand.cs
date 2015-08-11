// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Management.Automation;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Uninstall, "Package")]
    public class UninstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private UninstallationContext _context;
        private IDeleteOnRestartManager _deleteOnRestartManager;

        public UninstallPackageCommand()
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

        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        [Parameter]
        public SwitchParameter RemoveDependencies { get; set; }

        private void Preprocess()
        {
            CheckSolutionState();
            GetNuGetProject(ProjectName);
            ThreadHelper.JoinableTaskFactory.Run(CheckMissingPackagesAsync);
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            SubscribeToProgressEvents();
            Task.Run(() => UnInstallPackage());
            WaitAndLogPackageActions();
            UnsubscribeFromProgressEvents();
        }

        protected override void EndProcessing()
        {
            base.EndProcessing();
            var packageDirectoriesMarkedForDeletion = _deleteOnRestartManager.GetPackageDirectoriesMarkedForDeletion();
            if (packageDirectoriesMarkedForDeletion != null && packageDirectoriesMarkedForDeletion.Count != 0)
            {
                _deleteOnRestartManager.CheckAndRaisePackageDirectoriesMarkedForDeletion();
                var message = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    Resources.Cmdlet_RequestRestartToCompleteUninstall,
                    string.Join(", ", packageDirectoriesMarkedForDeletion));
                WriteWarning(message);
            }
        }

        /// <summary>
        /// Async call for uninstall a package from the current project
        /// </summary>
        private async Task UnInstallPackage()
        {
            try
            {
                await UninstallPackageByIdAsync(Project, Id, UninstallContext, this, WhatIf.IsPresent);
            }
            catch (Exception ex)
            {
                Log(ProjectManagement.MessageLevel.Error, ex.Message);
            }
            finally
            {
                BlockingCollection.Add(new ExecutionCompleteMessage());
            }
        }

        /// <summary>
        /// Uninstall package by Id
        /// </summary>
        /// <param name="project"></param>
        /// <param name="packageId"></param>
        /// <param name="uninstallContext"></param>
        /// <param name="projectContext"></param>
        /// <param name="isPreview"></param>
        /// <returns></returns>
        protected async Task UninstallPackageByIdAsync(NuGetProject project, string packageId, UninstallationContext uninstallContext, INuGetProjectContext projectContext, bool isPreview)
        {
            if (isPreview)
            {
                IEnumerable<NuGetProjectAction> actions = await PackageManager.PreviewUninstallPackageAsync(project, packageId, uninstallContext, projectContext, CancellationToken.None);
                PreviewNuGetPackageActions(actions);
            }
            else
            {
                await PackageManager.UninstallPackageAsync(project, packageId, uninstallContext, projectContext, CancellationToken.None);
            }
        }

        /// <summary>
        /// Uninstallation Context for Uninstall-Package command
        /// </summary>
        public UninstallationContext UninstallContext
        {
            get
            {
                _context = new UninstallationContext(RemoveDependencies.IsPresent, Force.IsPresent);
                return _context;
            }
        }
    }
}
