using NuGet.Client.VisualStudio;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This command lists the available packages which are either from a package source or installed in the current solution.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "Package", DefaultParameterSetName = ParameterAttribute.AllParameterSets)]
    [OutputType(typeof(PowerShellPackage))]
    public class GetPackageCommand : NuGetPowerShellBaseCommand
    {
        private const int DefaultFirstValue = 50;
        private bool _enablePaging;

        public GetPackageCommand()
            : base()
        {
        }

        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty]
        public string Filter { get; set; }

        [Parameter(Position = 1, Mandatory = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "Project")]
        [ValidateNotNullOrEmpty]
        public override string ProjectName { get; set; }

        [Parameter(ParameterSetName = "Remote")]
        [Parameter(ParameterSetName = "Updates")]
        [ValidateNotNullOrEmpty]
        public override string Source { get; set; }

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

        public List<NuGetProject> Projects { get; set; }

        protected override void Preprocess()
        {
            UseRemoteSourceOnly = ListAvailable.IsPresent || (!String.IsNullOrEmpty(Source) && !Updates.IsPresent);
            UseRemoteSource = ListAvailable.IsPresent || Updates.IsPresent || !String.IsNullOrEmpty(Source);
            CollapseVersions = !AllVersions.IsPresent && ListAvailable;
            base.Preprocess();
            if (string.IsNullOrEmpty(ProjectName))
            {
                Projects = VSSolutionManager.GetProjects().ToList();
            }
            else
            {
                Projects = new List<NuGetProject>() { Project };
            }
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

                if (Filter == null)
                {
                    Filter = string.Empty;
                }

                // Find avaiable packages from the online sources and not taking targetframeworks into account. 
                if (UseRemoteSourceOnly)
                {
                    IEnumerable<PSSearchMetadata> remotePackages = GetPackagesFromRemoteSource(Filter, Enumerable.Empty<string>(), IncludePrerelease.IsPresent,  Skip, First);
                    if (CollapseVersions)
                    {
                        WritePackages(remotePackages, VersionType.latest);
                    }
                    else
                    {
                        WritePackages(remotePackages, VersionType.all);
                    }
                }
                else
                {
                    CheckForSolutionOpen();

                    foreach (NuGetProject project in Projects)
                    {
                        string framework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework).Framework;
                        IEnumerable<PackageReference> installedPackages = project.GetInstalledPackages();
                        Dictionary<PSSearchMetadata, NuGetVersion> remoteUpdates = GetPackageUpdatesFromRemoteSource(installedPackages, new List<string> { framework }, IncludePrerelease.IsPresent, Skip, First);
                        if (CollapseVersions)
                        {
                            WritePackages(remoteUpdates, VersionType.latest);
                        }
                        else
                        {
                            WritePackages(remoteUpdates, VersionType.updates);
                        }
                    }
                }
            }
        }

        private void WritePackages(IEnumerable<PSSearchMetadata> packages, VersionType versionType, bool outputWarning = false)
        {
            // Write warning message for Get-Package -ListAvaialble -Filter being obsolete
            // and will be replaced by Find-Package [-Id] 
            string message;
            if (!CollapseVersions)
            {
                versionType = VersionType.all;
                message = "Find-Package [-Id] -ListAll";
            }
            else
            {
                versionType = VersionType.latest;
                message = "Find-Package [-Id]";
            }

            // Output list of PowerShellPackages
            if (outputWarning && !string.IsNullOrEmpty(Filter))
            {
                LogCore(MessageLevel.Warning, string.Format(Resources.Cmdlet_CommandObsolete, message));
            }

            WritePackages(packages, versionType);
        }
    }
}
