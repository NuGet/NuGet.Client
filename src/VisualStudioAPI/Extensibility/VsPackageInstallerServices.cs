extern alias Legacy;
using LegacyNuGet = Legacy.NuGet;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using NuGet.VisualStudio.Resources;
using NuGet.PackageManagement;
using NuGet.Versioning;
using System.Threading;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstallerServices))]
    public class VsPackageInstallerServices : IVsPackageInstallerServices
    {
        //private readonly IVsPackageManagerFactory _packageManagerFactory;

        private ISolutionManager _solutionManager;

        [ImportingConstructor]
        public VsPackageInstallerServices(ISolutionManager solutionManager)
        {
            _solutionManager = solutionManager;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages()
        {
            foreach (var project in _solutionManager.GetNuGetProjects())
            {
                var task = System.Threading.Tasks.Task.Run(async () => await project.GetInstalledPackagesAsync(CancellationToken.None));
                task.Wait();

                foreach (var package in task.Result)
                {
                    // TODO: populate the install path
                    yield return new VsPackageMetadata(package.PackageIdentity, null);
                }
            }

            yield break;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            foreach (var curProject in _solutionManager.GetNuGetProjects())
            {
                if (StringComparer.Ordinal.Equals(_solutionManager.GetNuGetProjectSafeName(curProject), project.UniqueName))
                {
                    var task = System.Threading.Tasks.Task.Run(async () => await curProject.GetInstalledPackagesAsync(CancellationToken.None));
                    task.Wait();
                    foreach (var package in task.Result)
                    {
                        yield return new VsPackageMetadata(package.PackageIdentity, null);
                    }
                }
            }

            yield break;
        }

        public bool IsPackageInstalled(Project project, string packageId)
        {
            return IsPackageInstalled(project, packageId, version: null);
        }

        public bool IsPackageInstalledEx(Project project, string packageId, string versionString)
        {
            LegacyNuGet.SemanticVersion version;
            if (versionString == null)
            {
                version = null;
            }
            else if (!LegacyNuGet.SemanticVersion.TryParse(versionString, out version))
            {
                throw new ArgumentException(VsResources.InvalidSemanticVersionString, "versionString");
            }

            return IsPackageInstalled(project, packageId, version);
        }

        public bool IsPackageInstalled(Project project, string packageId, LegacyNuGet.SemanticVersion version)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageId");
            }

            var packages = GetInstalledPackages(project).Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, packageId));

            if (version != null)
            {
                NuGetVersion semVer = null;
                if (!NuGetVersion.TryParse(version.ToString(), out semVer))
                {
                    throw new ArgumentException(VsResources.InvalidSemanticVersionString, "version");
                }

                packages = packages.Where(p => VersionComparer.VersionRelease.Equals(NuGetVersion.Parse(p.VersionString), semVer));
            }

            return packages.Any();
        }
    }
}