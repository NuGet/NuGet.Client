using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Uninstall, "Package")]
    public class UninstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private UninstallationContext _context;

        public UninstallPackageCommand()
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

        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        [Parameter]
        public SwitchParameter Force { get; set; }

        [Parameter]
        public SwitchParameter RemoveDependencies { get; set; }

        protected override void Preprocess()
        {
            base.Preprocess();
            CheckForSolutionOpen();
            UpdateActiveSourceRepository();
            GetNuGetProject(ProjectName);
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            SubscribeToProgressEvents();
            Task.Run(() => UnInstallPackage());
            WaitAndLogPackageActions();
            UnsubscribeFromProgressEvents();
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
                Log(MessageLevel.Error, ex.Message);
            }
            finally
            {
                blockingCollection.Add(new ExecutionCompleteMessage());
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
