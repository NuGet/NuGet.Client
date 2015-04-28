extern alias Legacy;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio.Resources;
using LegacyNuGet = Legacy.NuGet;
using TaskIEnumerablePackageReference = System.Threading.Tasks.Task<System.Collections.Generic.IEnumerable<NuGet.Packaging.PackageReference>>;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstallerServices))]
    public class VsPackageInstallerServices : IVsPackageInstallerServices
    {
        private ISolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly ISettings _settings;
        private NuGetPackageManager _packageManager;
        private string _packageFolderPath = string.Empty;

        [ImportingConstructor]
        public VsPackageInstallerServices(ISolutionManager solutionManager, ISourceRepositoryProvider sourceRepositoryProvider, ISettings settings)
        {
            _solutionManager = solutionManager;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _settings = settings;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages()
        {
            List<IVsPackageMetadata> packages = new List<IVsPackageMetadata>();

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Debug.Assert(_solutionManager.SolutionDirectory != null, "SolutionDir is null");

                // Calls may occur in the template wizard before the solution is actually created, in that case return no projects
                if (_solutionManager != null && !String.IsNullOrEmpty(_solutionManager.SolutionDirectory))
                {
                    InitializePackageManagerAndPackageFolderPath();

                    foreach (var project in _solutionManager.GetNuGetProjects())
                    {
                        var installedPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                        foreach (var package in installedPackages)
                        {
                            // find packages using the solution level packages folder
                            string installPath = _packageManager.PackagesFolderNuGetProject.GetInstalledPath(package.PackageIdentity);

                            var metadata = new VsPackageMetadata(package.PackageIdentity, installPath);
                            packages.Add(metadata);
                        }
                    }
                }

                return packages;
            });
        }

        private async TaskIEnumerablePackageReference GetInstalledPackageReferencesAsync(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            List<PackageReference> packages = new List<PackageReference>();

            if (_solutionManager != null && !String.IsNullOrEmpty(_solutionManager.SolutionDirectory))
            {
                InitializePackageManagerAndPackageFolderPath();

                var nuGetProject = PackageManagementHelpers.GetProject(_solutionManager, project, new VSAPIProjectContext());
                var installedPackages = await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None);
                packages.AddRange(installedPackages);
            }

            return packages;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                List<IVsPackageMetadata> packages = new List<IVsPackageMetadata>();

                // Debug.Assert(_solutionManager.SolutionDirectory != null, "SolutionDir is null");

                if (_solutionManager != null && !String.IsNullOrEmpty(_solutionManager.SolutionDirectory))
                {
                    InitializePackageManagerAndPackageFolderPath();

                    var nuGetProject = PackageManagementHelpers.GetProject(_solutionManager, project, new VSAPIProjectContext());
                    var installedPackages = await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None);

                    foreach (var package in installedPackages)
                    {
                        // Get the install path for package
                        string installPath = _packageManager.PackagesFolderNuGetProject.GetInstalledPath(package.PackageIdentity);

                        if (!String.IsNullOrEmpty(installPath))
                        {
                            // normalize the path and take the dir if the nupkg path was given
                            var dir = new DirectoryInfo(installPath);
                            installPath = dir.FullName;
                        }

                        var metadata = new VsPackageMetadata(package.PackageIdentity, installPath);
                        packages.Add(metadata);
                    }
                }

                return packages;
            });
        }

        private void InitializePackageManagerAndPackageFolderPath()
        {
            // Initialize package manager here since _solutionManager may be targeting different project now.
            _packageManager = new NuGetPackageManager(_sourceRepositoryProvider, _settings, _solutionManager);
            if (_packageManager != null && _packageManager.PackagesFolderSourceRepository != null)
            {
                _packageFolderPath = _packageManager.PackagesFolderSourceRepository.PackageSource.Source;
            }
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

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var installedPackageReferences = await GetInstalledPackageReferencesAsync(project);
                var packages = installedPackageReferences.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, packageId));

                if (version != null)
                {
                    NuGetVersion semVer = null;
                    if (!NuGetVersion.TryParse(version.ToString(), out semVer))
                    {
                        throw new ArgumentException(VsResources.InvalidSemanticVersionString, "version");
                    }

                    packages = packages.Where(p => VersionComparer.VersionRelease.Equals(p.PackageIdentity.Version, semVer));
                }

                return packages.Any();
            });
        }
    }
}