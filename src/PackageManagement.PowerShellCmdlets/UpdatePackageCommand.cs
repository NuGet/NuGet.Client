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
    [Cmdlet(VerbsData.Update, "Package", DefaultParameterSetName = "All")]
    public class UpdatePackageCommand : NuGetPowerShellBaseCommand
    {
        private NuGetPackageManager _nugetPackageManager;
        private ResolutionContext _context;
        private string _id;
        private string _projectName;
        private bool _idSpecified;
        private bool _projectSpecified;

        [ImportMany]
        public Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] ResourceProviders;

        public UpdatePackageCommand()
        {
            ISettings settings = Settings.LoadDefaultSettings(Environment.ExpandEnvironmentVariables("%systemdrive%"), null, null);
            SourceRepositoryProvider provider = new SourceRepositoryProvider(new PackageSourceProvider(settings), ResourceProviders);
            _nugetPackageManager = new NuGetPackageManager(provider);
        }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "Project")]
        [Parameter(ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "All")]
        [Parameter(ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "Reinstall")]
        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                _idSpecified = true;
            }
        }

        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "All")]
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "Project")]
        [Parameter(Position = 1, ValueFromPipelineByPropertyName = true, ParameterSetName = "Reinstall")]
        public string ProjectName
        {
            get
            {
                return _projectName;
            }
            set
            {
                _projectName = value;
                _projectSpecified = true;
            }
        }

        [Parameter(Position = 2, ParameterSetName = "Project")]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        [Parameter(Position = 3)]
        [ValidateNotNullOrEmpty]
        public string Source { get; set; }

        [Parameter]
        public SwitchParameter WhatIf { get; set; }

        [Parameter]
        public SwitchParameter Safe { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Reinstall")]
        [Parameter(ParameterSetName = "All")]
        public SwitchParameter Reinstall { get; set; }

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
            FolderNuGetProject project = new FolderNuGetProject("c:\temp");
            PackageIdentity identity = GetPackageIdentity();
            // TODO: Update package logic here or in PackageManager?
            // How to get the latest version of the packages?
            if (WhatIf.IsPresent)
            {
                _nugetPackageManager.PreviewInstallPackageAsync(project, identity, ResolutionContext, this);
            }
            else
            {
                _nugetPackageManager.InstallPackageAsync(project, identity, ResolutionContext, this);
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
                _context = new ResolutionContext(GetDependencyBehavior(), IncludePrerelease.IsPresent, false, Reinstall.IsPresent, false);
                return _context;
            }
        }

        private DependencyBehavior GetDependencyBehavior()
        {
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
