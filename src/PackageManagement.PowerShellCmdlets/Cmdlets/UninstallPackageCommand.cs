using NuGet.Client;
using NuGet.Resolver;
using System;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Uninstall, "Package")]
    public class UninstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private ResolutionContext _context;

        public UninstallPackageCommand(
            Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] resourceProvider,
            ISolutionManager solutionManager)
            : base(resourceProvider, solutionManager)
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
            GetSourceRepositoryProvider();
            PackageManager = new NuGetPackageManager(SourceRepositoryProvider);
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
        public ResolutionContext ResolutionContext
        {
            get
            {
                _context = new ResolutionContext(DependencyBehavior.Lowest, false, RemoveDependencies.IsPresent, Force.IsPresent, false);
                return _context;
            }
        }
    }
}
