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
    [Cmdlet(VerbsLifecycle.Install, "Package")]
    public class InstallPackageCommand : NuGetPowerShellBaseCommand
    {
        private NuGetPackageManager _nugetPackageManager;
        private ResolutionContext _context;

        [ImportMany]
        public Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] ResourceProviders;

        public InstallPackageCommand()
        {
        }

        [Parameter(ValueFromPipelineByPropertyName=true)]
        public SourceRepositoryProvider Provider { get; set; }

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
        public DependencyBehavior? DependencyVersion { get; set; }

        private void Preprocess()
        {
            if (Provider == null)
            {
                ISettings settings = Settings.LoadDefaultSettings(Environment.ExpandEnvironmentVariables("%systemdrive%"), null, null);
                Provider = new SourceRepositoryProvider(new PackageSourceProvider(settings), ResourceProviders);
            }
            _nugetPackageManager = new NuGetPackageManager(Provider);
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            FolderNuGetProject project = new FolderNuGetProject("c:\temp");
            PackageIdentity identity = GetPackageIdentity();
            if (WhatIf.IsPresent)
            {
                if (string.IsNullOrEmpty(Version))
                {
                    _nugetPackageManager.PreviewInstallPackageAsync(project, Id, ResolutionContext, this);
                }
                else
                {
                    _nugetPackageManager.PreviewInstallPackageAsync(project, identity, ResolutionContext, this);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(Version))
                {
                    _nugetPackageManager.InstallPackageAsync(project, Id, ResolutionContext, this);
                }
                else
                {
                    _nugetPackageManager.InstallPackageAsync(project, identity, ResolutionContext, this);
                }
            }
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
