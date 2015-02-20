extern alias Legacy;
using EnvDTE;
using NuGet.Client;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Versioning;
using NuGet.VisualStudio.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using LegacyNuGet = Legacy.NuGet;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstallerServices))]
    public class VsPackageInstallerServices : IVsPackageInstallerServices
    {
        private ISolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly ISettings _settings;
        private NuGetPackageManager _packageManager;
        private string _packageFolderPath;

        [ImportingConstructor]
        public VsPackageInstallerServices(ISolutionManager solutionManager, ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings)
        {
            _solutionManager = solutionManager;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
            _packageManager = new NuGetPackageManager(_sourceRepositoryProvider, _settings, _solutionManager);
            _packageFolderPath = _packageManager.PackagesFolderSourceRepository.PackageSource.Source;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages()
        {
            foreach (var project in _solutionManager.GetNuGetProjects())
            {
                var task = System.Threading.Tasks.Task.Run(async () => await project.GetInstalledPackagesAsync(CancellationToken.None));
                task.Wait();

                foreach (var package in task.Result)
                {
                    // Get the install path for package
                    string installPath = Path.Combine(_packageFolderPath, package.PackageIdentity.ToString());
                    yield return new VsPackageMetadata(package.PackageIdentity, installPath);
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
                        // Get the install path for package
                        string installPath = Path.Combine(_packageFolderPath, package.PackageIdentity.ToString());
                        yield return new VsPackageMetadata(package.PackageIdentity, installPath);
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