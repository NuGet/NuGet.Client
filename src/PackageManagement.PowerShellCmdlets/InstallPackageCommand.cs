using NuGet.PackageManagement;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace PackageManagement.PowerShellCmdlets
{
    public class InstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private NuGetPackageManager _nugetPackageManager;
        private ResolutionContext _context;

        public InstallPackageCommand()
        {
            SourceRepositoryProvider provider = new SourceRepositoryProvider();
            _nugetPackageManager = new NuGetPackageManager(provider);
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
        public string Source { get; set; }

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
        public NuGet.Resolver.DependencyBehavior? DependencyVersion { get; set; }

        protected override void ProcessRecordCore()
        {
            FolderNuGetProject project = new FolderNuGetProject("c:\temp");
            PackageIdentity identity = GetPackageIdentity();
            _nugetPackageManager.InstallPackageAsync(project, identity, ResolutionContext, this);
        }

        /// <summary>
        /// Returns single package identity for resolver when Id is specified
        /// </summary>
        /// <returns></returns>
        private PackageIdentity GetPackageIdentity()
        {
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
