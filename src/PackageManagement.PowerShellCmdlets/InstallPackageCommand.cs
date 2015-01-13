using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Install, "Package")]
    public class InstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private ResolutionContext _context;

        public InstallPackageCommand() 
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

        [Parameter, Alias("Prerelease")]
        public SwitchParameter IncludePrerelease { get; set; }

        [Parameter]
        public SwitchParameter IgnoreDependencies { get; set; }

        [Parameter]
        public FileConflictAction FileConflictAction { get; set; }

        [Parameter]
        public DependencyBehavior? DependencyVersion { get; set; }

        protected override void ProcessRecordCore()
        {
            CheckForSolutionOpen();

            Preprocess();
            PackageIdentity identity = GetPackageIdentity();

            SubscribeToProgressEvents();

            if (string.IsNullOrEmpty(Version))
            {
                InstallPackageById(Project, Id, ResolutionContext, this, WhatIf.IsPresent, Force.IsPresent);
            }
            else
            {
                InstallPackageByIdentity(Project, identity, ResolutionContext, this, WhatIf.IsPresent, Force.IsPresent);
            }

            UnsubscribeFromProgressEvents();
        }

        /// <summary>
        /// Returns single package identity for resolver when Id is specified
        /// </summary>
        /// <returns></returns>
        private PackageIdentity GetPackageIdentity()
        {
            PackageIdentity identity = null;
            if (!string.IsNullOrEmpty(Version))
            {
                NuGetVersion nVersion;
                bool success = NuGetVersion.TryParse(Version, out nVersion);
                if (success)
                {
                    identity = new PackageIdentity(Id, nVersion);
                }
            }
            return identity;
        }

        /// <summary>
        /// Resolution Context for the command
        /// </summary>
        public ResolutionContext ResolutionContext
        {
            get
            {
                _context = new ResolutionContext(GetDependencyBehavior(), IncludePrerelease.IsPresent, false, Force.IsPresent, false);
                return _context;
            }
        }

        private DependencyBehavior GetDependencyBehavior()
        {
            if (Force.IsPresent)
            {
                return DependencyBehavior.Ignore;
            }

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
                // TODO: Read it from NuGet.Config and default to Lowest.
                return DependencyBehavior.Lowest;
            }
        }
    }
}
