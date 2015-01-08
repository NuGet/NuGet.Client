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

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Uninstall, "Package")]
    public class UninstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private NuGetPackageManager _nugetPackageManager;
        private ResolutionContext _context;

        [ImportMany]
        public Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] ResourceProviders;

        public UninstallPackageCommand()
        {
            ISettings settings = Settings.LoadDefaultSettings(Environment.ExpandEnvironmentVariables("%systemdrive%"), null, null);
            SourceRepositoryProvider provider = new SourceRepositoryProvider(new PackageSourceProvider(settings), ResourceProviders);
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

        [Parameter]
        public SwitchParameter RemoveDependencies { get; set; }

        protected override void ProcessRecordCore()
        {
            FolderNuGetProject project = new FolderNuGetProject("c:\temp");
            PackageIdentity identity = GetPackageIdentity();
            // TODO: UninstallAsync?
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
                _context = new ResolutionContext(DependencyBehavior.Lowest, false, RemoveDependencies.IsPresent, Force.IsPresent, false);
                return _context;
            }
        }
    }
}
