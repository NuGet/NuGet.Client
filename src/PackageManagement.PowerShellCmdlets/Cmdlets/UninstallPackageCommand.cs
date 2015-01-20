using NuGet.Client;
using NuGet.Resolver;
using System;
using System.Management.Automation;

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
            GetActiveSourceRepository();
            GetNuGetProject();
            base.Preprocess();
        }

        protected override void ProcessRecordCore()
        {
            CheckForSolutionOpen();

            Preprocess();

            SubscribeToProgressEvents();
            UninstallPackageById(Project, Id, ResolutionContext, this, WhatIf.IsPresent);
            UnsubscribeFromProgressEvents();
        }

        /// <summary>
        /// Resolution Context for the command
        /// </summary>
        public UninstallationContext ResolutionContext
        {
            get
            {
                _context = new UninstallationContext(RemoveDependencies.IsPresent, Force.IsPresent);
                return _context;
            }
        }
    }
}
