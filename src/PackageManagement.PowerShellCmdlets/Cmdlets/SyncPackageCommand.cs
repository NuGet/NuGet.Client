using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This command consolidates the specified package into the specified project.
    /// </summary>
    [Cmdlet(VerbsData.Sync, "Package")]
    public class SyncPackageCommand : PackageActionBaseCommand
    {
        private ResolutionContext _context;
        private bool _allowPrerelease;

        public SyncPackageCommand()
            : base()
        {
        }

        private List<NuGetProject> Projects = new List<NuGetProject>();

        protected override void Preprocess()
        {
            base.Preprocess();
            if (string.IsNullOrEmpty(ProjectName))
            {
                ProjectName = VsSolutionManager.DefaultNuGetProjectName;
            }
            // Get the projects in the solution that's not the current default or specified project to sync the package identity to.
            Projects = VsSolutionManager.GetNuGetProjects()
                .Where(p => !StringComparer.OrdinalIgnoreCase.Equals(p.GetMetadata<string>(NuGetProjectMetadataKeys.Name), ProjectName))
                .ToList();
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            PackageIdentity identity = GetPackageIdentity().Result;

            SubscribeToProgressEvents();
            if (Projects.Count == 0)
            {
                LogCore(MessageLevel.Info, string.Format(Resources.Cmdlets_NoProjectsToSyncPackage, Id));
            }
            else if (identity == null)
            {
                LogCore(MessageLevel.Info, string.Format(Resources.Cmdlet_PackageNotInstalled, Id));
            }
            else
            {
                _allowPrerelease = IncludePrerelease.IsPresent || identity.Version.IsPrerelease;
                Task.Run(() => SyncPackages(Projects, identity));
                WaitAndLogPackageActions();
            }
            UnsubscribeFromProgressEvents();
        }

        /// <summary>
        /// Async call for sync package to the version installed to the specified or current project.
        /// </summary>
        /// <param name="projects"></param>
        /// <param name="identity"></param>
        private async Task SyncPackages(IEnumerable<NuGetProject> projects, PackageIdentity identity)
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
                Log(MessageLevel.Error, ex.Message);
            }
            finally
            {
                blockingCollection.Add(new ExecutionCompleteMessage());
            }
        }

        /// <summary>
        /// Returns single package identity for resolver when Id is specified
        /// </summary>
        /// <returns></returns>
        private async Task<PackageIdentity> GetPackageIdentity()
        {
            PackageIdentity identity = null;
            if (!string.IsNullOrEmpty(Version))
            {
                NuGetVersion nVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(Version);
                identity = new PackageIdentity(Id, nVersion);
            }
            else
            {
                identity = (await Project.GetInstalledPackagesAsync(CancellationToken.None))
                    .Where(p => string.Equals(p.PackageIdentity.Id, Id, StringComparison.OrdinalIgnoreCase))
                    .Select(v => v.PackageIdentity).FirstOrDefault();
            }
            return identity;
        }

        /// <summary>
        /// Resolution Context for Sync-Package command
        /// </summary>
        public ResolutionContext ResolutionContext
        {
            get
            {
                _context = new ResolutionContext(GetDependencyBehavior(), _allowPrerelease, false);
                return _context;
            }
        }
    }
}
