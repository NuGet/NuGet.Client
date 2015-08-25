// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio.Implementation.Resources;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstallerServices))]
    public class VsPackageInstallerServices : IVsPackageInstallerServices
    {
        private readonly ISolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;
        private readonly Configuration.ISettings _settings;
        private NuGetPackageManager _packageManager;
        private string _packageFolderPath = string.Empty;

        [ImportingConstructor]
        public VsPackageInstallerServices(
            ISolutionManager solutionManager,
            ISourceRepositoryProvider sourceRepositoryProvider,
            Configuration.ISettings settings,
            IDeleteOnRestartManager deleteOnRestartManager)
        {
            _solutionManager = solutionManager;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _deleteOnRestartManager = deleteOnRestartManager;
            _settings = settings;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages()
        {
            var packages = new HashSet<IVsPackageMetadata>(new VsPackageMetadataComparer());

            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    // Calls may occur in the template wizard before the solution is actually created,
                    // in that case return no projects
                    if (_solutionManager != null
                        && !string.IsNullOrEmpty(_solutionManager.SolutionDirectory))
                    {
                        InitializePackageManagerAndPackageFolderPath();

                        var effectiveGlobalPackagesFolder =
                            BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                                _solutionManager.SolutionDirectory,
                                _settings);

                        foreach (var project in _solutionManager.GetNuGetProjects())
                        {
                            var installedPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                            var buildIntegratedProject = project as BuildIntegratedNuGetProject;

                            foreach (var package in installedPackages)
                            {
                                if (!package.PackageIdentity.HasVersion)
                                {
                                    // Currently we are not supporting floating versions 
                                    // because of that we will skip this package
                                    continue;
                                }

                                // find packages using the solution level packages folder
                                string installPath;
                                if (buildIntegratedProject != null)
                                {
                                    installPath = BuildIntegratedProjectUtility.GetPackagePathFromGlobalSource(
                                        effectiveGlobalPackagesFolder,
                                        package.PackageIdentity);
                                }
                                else
                                {
                                    installPath = _packageManager.PackagesFolderNuGetProject.GetInstalledPath(
                                        package.PackageIdentity);
                                }

                                var metadata = new VsPackageMetadata(package.PackageIdentity, installPath);
                                packages.Add(metadata);
                            }
                        }
                    }

                    return packages;
                });
        }

        private async Task<IEnumerable<Packaging.PackageReference>> GetInstalledPackageReferencesAsync(
            Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            var packages = new List<Packaging.PackageReference>();

            if (_solutionManager != null
                && !string.IsNullOrEmpty(_solutionManager.SolutionDirectory))
            {
                InitializePackageManagerAndPackageFolderPath();

                var nuGetProject = await PackageManagementHelpers.GetProjectAsync(
                                    _solutionManager,
                                    project,
                                    new VSAPIProjectContext());

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
                    var packages = new List<IVsPackageMetadata>();

                    if (_solutionManager != null
                        && !string.IsNullOrEmpty(_solutionManager.SolutionDirectory))
                    {
                        InitializePackageManagerAndPackageFolderPath();

                        var nuGetProject = await PackageManagementHelpers.GetProjectAsync(
                                            _solutionManager,
                                            project,
                                            new VSAPIProjectContext());

                        var installedPackages = await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None);

                        foreach (var package in installedPackages)
                        {
                            // Get the install path for package
                            string installPath = _packageManager.PackagesFolderNuGetProject.GetInstalledPath(
                                                    package.PackageIdentity);

                            if (!string.IsNullOrEmpty(installPath))
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
            _packageManager = new NuGetPackageManager(
                _sourceRepositoryProvider,
                _settings,
                _solutionManager,
                _deleteOnRestartManager);

            if (_packageManager != null
                && _packageManager.PackagesFolderSourceRepository != null)
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
            SemanticVersion version;
            if (versionString == null)
            {
                version = null;
            }
            else if (!SemanticVersion.TryParse(versionString, out version))
            {
                throw new ArgumentException(VsResources.InvalidSemanticVersionString, "versionString");
            }

            return IsPackageInstalled(project, packageId, version);
        }

        public bool IsPackageInstalled(Project project, string packageId, SemanticVersion version)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageId");
            }

            // We simply use ThreadHelper.JoinableTaskFactory.Run instead of PumpingJTF.Run, unlike,
            // VsPackageInstaller and VsPackageUninstaller. Because, no powershell scripts get executed
            // as part of the operations performed below. Powershell scripts need to be executed on the
            // pipeline execution thread and they might try to access DTE. Doing that under
            // ThreadHelper.JoinableTaskFactory.Run will consistently result in a hang
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    var installedPackageReferences = await GetInstalledPackageReferencesAsync(project);
                    var packages = installedPackageReferences.Where(p =>
                                        StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, packageId));

                    if (version != null)
                    {
                        NuGetVersion semVer = null;
                        if (!NuGetVersion.TryParse(version.ToString(), out semVer))
                        {
                            throw new ArgumentException(VsResources.InvalidSemanticVersionString, "version");
                        }

                        packages = packages.Where(p =>
                                        VersionComparer.VersionRelease.Equals(p.PackageIdentity.Version, semVer));
                    }

                    return packages.Any();
                });
        }
    }
}
