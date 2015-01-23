using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This command consolidates the specified package into the specified project.
    /// </summary>
    [Cmdlet(VerbsData.Sync, "Package")]
    public class SyncPackageCommand : PackageActionBaseCommand
    {
        private ResolutionContext _context;

        public SyncPackageCommand()
            : base()
        {
        }

        public List<NuGetProject> Projects;

        protected override void Preprocess()
        {
            base.Preprocess();
            Projects = VsSolutionManager.GetNuGetProjects().ToList();
        }

        protected override void ProcessRecordCore()
        {
            base.ProcessRecordCore();
            PackageIdentity identity = GetPackageIdentity();

            SubscribeToProgressEvents();
            SyncPackages(Projects, identity);
            WaitAndLogFromMessageQueue();
            UnsubscribeFromProgressEvents();
        }

        private async void SyncPackages(IEnumerable<NuGetProject> projects, PackageIdentity identity)
        {
            try
            {
                foreach (NuGetProject project in projects)
                {
                    await InstallPackageByIdentityAsync(project, identity, ResolutionContext, this, WhatIf.IsPresent);
                }
            }
            catch (Exception ex)
            {
                LogCore(MessageLevel.Error, ex.Message);
            }
            completeEvent.Set();
        }

        /// <summary>
        /// Returns single package identity for resolver when Id is specified
        /// </summary>
        /// <returns></returns>
        private PackageIdentity GetPackageIdentity()
        {
            PackageIdentity identity = null;
            if (!string.IsNullOrEmpty(Version))
            {
                NuGetVersion nVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(Version);
                identity = new PackageIdentity(Id, nVersion);
            }
            else
            {
                identity = Project.GetInstalledPackages()
                    .Where(p => string.Equals(p.PackageIdentity.Id, Id, StringComparison.OrdinalIgnoreCase))
                    .Select(v => v.PackageIdentity).FirstOrDefault();
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
                _context = new ResolutionContext(GetDependencyBehavior(), IncludePrerelease.IsPresent, false);
                return _context;
            }
        }
    }
}
