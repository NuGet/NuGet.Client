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
            PerformPackageUpdatesOrReinstalls();
            UnsubscribeFromProgressEvents();
        }

        /// <summary>
        /// Perform package updates
        /// </summary>
        private void PerformPackageUpdatesOrReinstalls()
        {
            // Update-Package without ID specified
            if (!_idSpecified)
            {
                foreach (NuGetProject project in Projects)
                {
                    // Get the list of package identities to be updated for PackageManager
                    IEnumerable<PackageIdentity> identitiesToUpdate = Enumerable.Empty<PackageIdentity>();
                    if (Reinstall.IsPresent)
                    {
                        // Update-Package -Reinstall -> get list of installed package identities
                        identitiesToUpdate = project.GetInstalledPackages().Select(v => v.PackageIdentity);
                    }
                    else
                    {
                        // Update-Packages -> get list of package identities with Id and null version.
                        identitiesToUpdate = GeneratePackageIdentityListForUpdate(project);
                    }

                    // Preview update actions
                    IEnumerable<NuGetProjectAction> actions = PackageManager.PreviewUpdatePackagesAsync(identitiesToUpdate, project, ResolutionContext, this, ActiveSourceRepository).Result;
                    if (actions.Any())
                    {
                        // Execute project actions by Package Manager
                        ExecutePackageUpdates(actions, project);
                        WaitAndLogFromMessageQueue();
                    }
                    else
                    {
                        Log(MessageLevel.Info, Resources.Cmdlet_NoPackageUpdates);
                    }
                }
            }
            // Update-Package with Id specified
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
                        PackageIdentity update = null;
                        // If -Version switch is specified
                        if (!string.IsNullOrEmpty(Version))
                        {
                            // If Highest/HighestMinor/HighestPatch/Lowest is given after -version switch
                            if (IsVersionEnum)
                            {
                                update = GetPackageUpdate(installedPackage, project, _allowPrerelease, false, null, true, _updateVersionEnum);
                            }
                            // If a NuGetVersion format is given after -version switch
                            else
                            {
                                update = GetPackageUpdate(installedPackage, project, _allowPrerelease, false, Version);
                            }

                            if (update != null)
                            {
                                // Update by package identity
                                UpdatePackageByIdentity(update, project);
                                WaitAndLogFromMessageQueue();
                            }
                            else
                            {
                                Log(MessageLevel.Info, Resources.Cmdlet_NoPackageUpdates);
                            }
                        }
                        else
                        {
                            if (Reinstall.IsPresent)
                            {
                                // Update-Package Id -Reinstall
                                PackageIdentity identity = installedPackage.PackageIdentity;
                                UpdatePackageByIdentity(identity, project);
                            }
                            else
                            {
                                // Update-Package Id
                                UpdatePackageById(project);
                            }
                            WaitAndLogFromMessageQueue();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Async call for Execute package update actions from the list of identities
        /// </summary>
        /// <param name="identities"></param>
        /// <param name="project"></param>
        private async void ExecutePackageUpdates(IEnumerable<NuGetProjectAction> actions, NuGetProject project)
        {
            try
            {
                await PackageManager.ExecuteNuGetProjectActionsAsync(project, actions, this);
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
        /// Async call for update a package by Identity.
        /// </summary>
        /// <param name="identities"></param>
        private async void UpdatePackageByIdentity(PackageIdentity identity, NuGetProject project)
        {
            try
            {
                await InstallPackageByIdentityAsync(Project, identity, ResolutionContext, this, WhatIf.IsPresent, Reinstall.IsPresent, UninstallContext);
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
        /// Async call for update a package by Id.
        /// </summary>
        /// <param name="identities"></param>
        private async void UpdatePackageById(NuGetProject project)
        {
            try
            {
                await InstallPackageByIdAsync(Project, Id, ResolutionContext, this, WhatIf.IsPresent, Reinstall.IsPresent, UninstallContext);
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
        /// Get a list of package identities with installed package Ids but null versions
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        private IEnumerable<PackageIdentity> GeneratePackageIdentityListForUpdate(NuGetProject project)
        {
            List<PackageIdentity> identityList = new List<PackageIdentity>();
            IEnumerable<string> packageIds = project.GetInstalledPackages().Select(v => v.PackageIdentity.Id);
            foreach (string id in packageIds)
            {
                identityList.Add(new PackageIdentity(id, null));
            }
            return identityList;
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

        /// <summary>
        /// Return dependecy behavior for Update-Package command. Scenarios include Update-Package and Update-Package -Safe.
        /// </summary>
        /// <returns></returns>
        protected override DependencyBehavior GetDependencyBehavior()
        {
            // Return DependencyBehavior.HighestPatch for -Safe switch
            if (Safe.IsPresent)
            {
                return DependencyBehavior.HighestPatch;
            }

            // Return DependencyBehavior.Highest for Update-Package
            if (!_idSpecified && !Reinstall.IsPresent)
            {
                return DependencyBehavior.Highest;
            }

            return base.GetDependencyBehavior();
        }
    }
}
