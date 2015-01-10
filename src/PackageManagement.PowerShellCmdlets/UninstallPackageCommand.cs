using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.ComponentModel.Composition;
using System.Management.Automation;
using System.Linq;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Uninstall, "Package")]
    public class UninstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private ResolutionContext _context;

        public UninstallPackageCommand()
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

        protected override void ProcessRecordCore()
        {
            Preprocess();

            PackageIdentity identity = GetPackageIdentity();
            // TODO: UninstallAsync?
            //PackageManager.UninstallPackageAsync(project, identity, ResolutionContext, this);
            Project.UninstallPackage(identity, this);
        }

        /// <summary>
        /// Returns single package identity for resolver when Id is specified
        /// </summary>
        /// <returns></returns>
        private PackageIdentity GetPackageIdentity()
        {
            PackageIdentity identity = null;
            if (!string.IsNullOrEmpty(Id))
            {
                if (!string.IsNullOrEmpty(Version))
                {
                    identity = new PackageIdentity(Id, NuGetVersion.Parse(Version));
                }
                else
                {
                    // If Version is not specified.
                    identity = Project.GetInstalledPackages()
                        .Where(p => string.Equals(p.PackageIdentity.Id, Id, StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault().
                        PackageIdentity;
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
                _context = new ResolutionContext(DependencyBehavior.Lowest, false, RemoveDependencies.IsPresent, Force.IsPresent, false);
                return _context;
            }
        }
    }
}
