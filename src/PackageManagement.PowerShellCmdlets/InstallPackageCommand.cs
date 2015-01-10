using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Install, "Package")]
    public class InstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private ResolutionContext _context;

        public InstallPackageCommand()
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
            Preprocess();
            PackageIdentity identity = GetPackageIdentity();

            if (WhatIf.IsPresent)
            {
                if (string.IsNullOrEmpty(Version))
                {
                    PackageManager.PreviewInstallPackageAsync(Project, Id, ResolutionContext, this).Wait();
                }
                else
                {
                    PackageManager.PreviewInstallPackageAsync(Project, identity, ResolutionContext, this).Wait();
                }
            }
            else
            {
                if (string.IsNullOrEmpty(Version))
                {
                    PackageManager.InstallPackageAsync(Project, Id, ResolutionContext, this).Wait();
                }
                else
                {
                   PackageManager.InstallPackageAsync(Project, identity, ResolutionContext, this).Wait();
                }
            }
        }

        /// <summary>
        /// Returns single package identity for resolver when Id is specified
        /// </summary>
        /// <returns></returns>
        private PackageIdentity GetPackageIdentity()
        {
            // TODO: Get latest package version when Version is not specified.
            PackageIdentity identity = null;
            if (!string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Version))
            {
                identity = new PackageIdentity(Id, NuGetVersion.Parse(Version));
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
