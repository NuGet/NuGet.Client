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
using NuGet.Packaging;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This command lists the available packages which are either from a package source or installed in the current solution.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Package", DefaultParameterSetName = ParameterAttribute.AllParameterSets)]
    //[OutputType(typeof(IPackage))]
    public class GetPackageCommand : NuGetPowerShellBaseCommand
    {
        private const int DefaultFirstValue = 50;
        private bool _enablePaging;
        private NuGetPackageManager _nugetPackageManager;

        [ImportMany]
        public Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>[] ResourceProviders;

        public GetPackageCommand() :
            base()
        {
            ISettings settings = Settings.LoadDefaultSettings(Environment.ExpandEnvironmentVariables("%systemdrive%"), null, null);
            SourceRepositoryProvider provider = new SourceRepositoryProvider(new PackageSourceProvider(settings), ResourceProviders);
            _nugetPackageManager = new NuGetPackageManager(provider);
        }

        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Filter { get; set; }

        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Project")]
        [ValidateNotNullOrEmpty]
        public string ProjectName { get; set; }

        [Parameter(ParameterSetName = "Remote")]
        [Parameter(ParameterSetName = "Updates")]
        [ValidateNotNullOrEmpty]
        public string Source { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Remote")]
        [Alias("Online", "Remote")]
        public SwitchParameter ListAvailable { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Updates")]
        public SwitchParameter Updates { get; set; }

        [Parameter(ParameterSetName = "Remote")]
        [Parameter(ParameterSetName = "Updates")]
        public SwitchParameter AllVersions { get; set; }

        [Parameter(ParameterSetName = "Remote")]
        public int PageSize { get; set; }

        [Parameter]
        [Alias("Prerelease")]
        public SwitchParameter IncludePrerelease { get; set; }

        [Parameter]
        [ValidateRange(0, Int32.MaxValue)]
        public virtual int First { get; set; }

        [Parameter]
        [ValidateRange(0, Int32.MaxValue)]
        public int Skip { get; set; }

        /// <summary>
        /// Determines if local repository are not needed to process this command
        /// </summary>
        protected bool UseRemoteSourceOnly { get; set; }

        /// <summary>
        /// Determines if a remote repository will be used to process this command.
        /// </summary>
        protected bool UseRemoteSource { get; set; }

        protected virtual bool CollapseVersions { get; set; }

        protected override void Preprocess()
        {
            UseRemoteSourceOnly = ListAvailable.IsPresent || (!String.IsNullOrEmpty(Source) && !Updates.IsPresent);
            UseRemoteSource = ListAvailable.IsPresent || Updates.IsPresent || !String.IsNullOrEmpty(Source);
            CollapseVersions = !AllVersions.IsPresent && ListAvailable;
            base.Preprocess();
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            // If Remote & Updates set of parameters are not specified
            if (!UseRemoteSource)
            {
                IEnumerable<PackageReference> installedPackages = Project.GetInstalledPackages();
                WritePackages(installedPackages);
            }
            else
            {
                if (PageSize != 0)
                {
                    _enablePaging = true;
                    First = PageSize;
                }
                else if (First == 0)
                {
                    First = DefaultFirstValue;
                }

                // Find avaiable packages from the online sources and not taking targetframeworks into account. 
                if (UseRemoteSourceOnly)
                {

                }
                else
                {
                    // Get package updates from the remote source and take targetframeworks into account.

                }
            }
        }

        private void WritePackages(IEnumerable<PackageReference> installedPackages)
        {
            List<PackageIdentity> identities = new List<PackageIdentity>();
            foreach (PackageReference package in installedPackages)
            {
                identities.Add(package.PackageIdentity);
            }
            WriteObject(identities);
        }
    }
}
