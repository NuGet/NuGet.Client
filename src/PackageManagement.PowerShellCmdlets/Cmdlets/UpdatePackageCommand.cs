using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsData.Update, "Package", DefaultParameterSetName = "All")]
    public class UpdatePackageCommand : PackageActionBaseCommand
    {
        private ResolutionContext _context;
        private UninstallationContext _uninstallcontext;
        private string _id;
        private string _projectName;
        private bool _idSpecified;
        private bool _projectSpecified;
        private bool _versionSpecifiedPrerelease;
        private bool _allowPrerelease;
        private DependencyBehavior _updateVersionEnum;
        private NuGetVersion _nugetVersion;

        public UpdatePackageCommand()
            : base()
        {
        }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "Project")]
        [Parameter(ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "All")]
        [Parameter(ValueFromPipelineByPropertyName = true, Position = 0, ParameterSetName = "Reinstall")]
        public override string Id
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
        public override string ProjectName
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
        public override string Version { get; set; }

        [Parameter]
        public SwitchParameter Safe { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "Reinstall")]
        [Parameter(ParameterSetName = "All")]
        public SwitchParameter Reinstall { get; set; }

        public List<NuGetProject> Projects;

        public bool IsVersionEnum { get; set; }

        protected override void Preprocess()
        {
            ParseUserInputForVersion();
            base.Preprocess();
            if (_projectSpecified)
            {
                Projects = VsSolutionManager.GetNuGetProjects().ToList();
            }
            else
            {
                Projects = new List<NuGetProject>() { Project };
            }
        }

        protected override void ProcessRecordCore()
        {
            base.ProcessRecordCore();

            SubscribeToProgressEvents();
            if (!Reinstall.IsPresent)
            {
                PerformPackageUpdates();
            }
            else
            {
                PerformPackageReinstalls();
            }
            UnsubscribeFromProgressEvents();
        }

        /// <summary>
        /// Perform package updates
        /// </summary>
        private void PerformPackageUpdates()
        {
            // Update-Package without ID specified
            if (!_idSpecified)
            {
                foreach (NuGetProject project in Projects)
                {
                    string framework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework).Framework;
                    IEnumerable<PackageReference> installedPackages = Project.GetInstalledPackages();
                    IEnumerable<PackageIdentity> updates = GetPackageUpdates(installedPackages, project, IncludePrerelease.IsPresent, Safe.IsPresent);
                    if (updates.Any())
                    {
                        UpdatePackages(updates, project);
                        WaitAndLogFromMessageQueue();
                    }
                    else
                    {
                        Log(MessageLevel.Info, Resources.Cmdlet_NoPackageUpdates);
                    }
                }
            }
            else
            {
                foreach (NuGetProject project in Projects)
                {
                    string framework = project.GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework).Framework;
                    PackageReference installedPackage = Project.GetInstalledPackages().Where(p => string.Equals(p.PackageIdentity.Id, Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    IEnumerable<PackageReference> installedPackages = new List<PackageReference>() { installedPackage };
                    IEnumerable<PackageIdentity> updates = Enumerable.Empty<PackageIdentity>();

                    // If package Id exists in Packages folder but is not actually installed to the current project, throw.
                    if (installedPackage == null)
                    {
                        WriteError(string.Format(Resources.PackageNotInstalledInAnyProject, Id));
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(Version))
                        {
                            // If Highest/HighestMinor/HighestPatch/Lowest is given after -version switch
                            if (IsVersionEnum)
                            {
                                updates = GetPackageUpdates(installedPackages, project, _allowPrerelease, false, null, true, _updateVersionEnum);
                            }
                            // If a NuGetVersion format is given after -version switch
                            else
                            {
                                updates = GetPackageUpdates(installedPackages, project, _allowPrerelease, false, Version);
                            }
                        }
                        // Find the latest update from server
                        else
                        {
                            updates = GetPackageUpdates(installedPackages, project, _allowPrerelease, Safe.IsPresent);
                        }
                    }

                    if (updates.Any())
                    {
                        UpdatePackages(updates, project);
                        WaitAndLogFromMessageQueue();
                    }
                    else
                    {
                        Log(MessageLevel.Info, Resources.Cmdlet_NoPackageUpdates);
                    }
                }
            }
        }

        /// <summary>
        /// Performs package reinstall
        /// </summary>
        private void PerformPackageReinstalls()
        {
            // Update-Package -reinstall
            if (!_idSpecified)
            {
                foreach (NuGetProject project in Projects)
                {
                    IEnumerable<PackageReference> installedPackages = Project.GetInstalledPackages();
                    UpdatePackages(installedPackages.Select(v => v.PackageIdentity), project);
                    WaitAndLogFromMessageQueue();
                }
            }
            else
            {
                foreach (NuGetProject project in Projects)
                {
                    PackageReference installedPackage = Project.GetInstalledPackages().Where(p => string.Equals(p.PackageIdentity.Id, Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                    // If package Id exists in Packages folder but is not actually installed to the current project, throw.
                    if (installedPackage == null)
                    {
                        WriteError(string.Format(Resources.PackageNotInstalledInAnyProject, Id));
                    }
                    else
                    {
                        List<PackageIdentity> identities = new List<PackageIdentity>() { installedPackage.PackageIdentity };
                        UpdatePackages(identities, project);
                        WaitAndLogFromMessageQueue();
                    }
                }
            }
        }

        /// <summary>
        /// Async call for Update packages from the list of identities
        /// </summary>
        /// <param name="identities"></param>
        /// <param name="project"></param>
        private async void UpdatePackages(IEnumerable<PackageIdentity> identities, NuGetProject project)
        {
            try
            {
                foreach (PackageIdentity identity in identities)
                {
                    await InstallPackageByIdentityAsync(project, identity, ResolutionContext, this, WhatIf.IsPresent, Reinstall.IsPresent, UninstallContext);
                }
            }
            catch (Exception ex)
            {
                Log(MessageLevel.Error, ex.Message);
            }
            finally
            {
                completeEvent.Set();
            }
        }

        /// <summary>
        /// Parse user input for -Version switch
        /// </summary>
        private void ParseUserInputForVersion()
        {
            if (!string.IsNullOrEmpty(Version))
            {
                DependencyBehavior updateVersion;
                IsVersionEnum = Enum.TryParse<DependencyBehavior>(Version, true, out updateVersion);
                if (IsVersionEnum)
                {
                    _updateVersionEnum = updateVersion;
                }
                // If Version is prerelease, automatically allow prerelease (i.e. append -Prerelease switch).
                else
                {
                    _nugetVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(Version);
                    if (_nugetVersion.IsPrerelease)
                    {
                        _versionSpecifiedPrerelease = true;
                    }
                }
            }
        }

        /// <summary>
        /// Resolution Context for Update-Package command
        /// </summary>
        public ResolutionContext ResolutionContext
        {
            get
            {
                _allowPrerelease = IncludePrerelease.IsPresent || _versionSpecifiedPrerelease;
                _context = new ResolutionContext(GetDependencyBehavior(), _allowPrerelease, false);
                return _context;
            }
        }

        /// <summary>
        /// Uninstallation Context for Update-Package -Reinstall command
        /// </summary>
        public UninstallationContext UninstallContext
        {
            get
            {
                _uninstallcontext = new UninstallationContext(false , Reinstall.IsPresent);
                return _uninstallcontext;
            }
        }
    }
}
