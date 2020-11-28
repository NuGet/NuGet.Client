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
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio.Implementation.Resources;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsPackageInstallerServices))]
    public class VsPackageInstallerServices : IVsPackageInstallerServices
    {
        private readonly IVsSolutionManager _solutionManager;
        private readonly ISourceRepositoryProvider _sourceRepositoryProvider;
        private readonly IDeleteOnRestartManager _deleteOnRestartManager;
        private readonly ISettings _settings;
        private readonly IVsProjectThreadingService _threadingService;
        private readonly INuGetTelemetryProvider _telemetryProvider;

        [ImportingConstructor]
        public VsPackageInstallerServices(
            IVsSolutionManager solutionManager,
            ISourceRepositoryProvider sourceRepositoryProvider,
            ISettings settings,
            IDeleteOnRestartManager deleteOnRestartManager,
            IVsProjectThreadingService threadingService,
            INuGetTelemetryProvider telemetryProvider)
        {
            _solutionManager = solutionManager;
            _sourceRepositoryProvider = sourceRepositoryProvider;
            _deleteOnRestartManager = deleteOnRestartManager;
            _settings = settings;
            _threadingService = threadingService;
            _telemetryProvider = telemetryProvider;
        }

        public IEnumerable<IVsPackageMetadata> GetInstalledPackages()
        {
            try
            {
                var packages = new HashSet<IVsPackageMetadata>(new VsPackageMetadataComparer());

                return _threadingService.JoinableTaskFactory.Run(async delegate
                    {
                        // Calls may occur in the template wizard before the solution is actually created,
                        // in that case return no projects
                        if (_solutionManager != null
                                && !string.IsNullOrEmpty(_solutionManager.SolutionDirectory))
                        {
                            //switch to background thread
                            await TaskScheduler.Default;

                            NuGetPackageManager nuGetPackageManager = CreateNuGetPackageManager();

                            foreach (var project in (await _solutionManager.GetNuGetProjectsAsync()))
                            {
                                FallbackPackagePathResolver pathResolver = null;
                                var buildIntegratedProject = project as BuildIntegratedNuGetProject;
                                if (buildIntegratedProject != null)
                                {
                                    pathResolver = await GetPackagesPathResolverAsync(buildIntegratedProject);
                                }

                                var installedPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                                foreach (var package in installedPackages)
                                {
                                    var identity = package.PackageIdentity;

                                    if (!identity.HasVersion)
                                    {
                                        // Currently we are not supporting floating versions 
                                        // because of that we will skip this package
                                        continue;
                                    }

                                    // find packages using the solution level packages folder
                                    string installPath;
                                    if (buildIntegratedProject != null)
                                    {
                                        installPath = pathResolver.GetPackageDirectory(identity.Id, identity.Version);
                                    }
                                    else
                                    {
                                        installPath = nuGetPackageManager
                                            .PackagesFolderNuGetProject
                                            .GetInstalledPath(identity);
                                    }

                                    var metadata = new VsPackageMetadata(package.PackageIdentity, installPath);

                                    packages.Add(metadata);
                                }
                            }
                        }

                        return packages;
                    });
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsPackageInstallerServices).FullName);
                throw;
            }
        }

        private async Task<FallbackPackagePathResolver> GetPackagesPathResolverAsync(BuildIntegratedNuGetProject project)
        {
            // To get packagesPath for build integrated projects, first read the packageSpec to know if
            // RestorePackagesPath property was specified. If yes, then use that property to get packages path
            // otherwise use global user cache folder from _settings.
            var context = new DependencyGraphCacheContext();
            var packageSpecs = await project.GetPackageSpecsAsync(context);
            var packageSpec = packageSpecs.Single(e => e.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference
                || e.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson);

            var packagesPath = VSRestoreSettingsUtilities.GetPackagesPath(_settings, packageSpec);

            return new FallbackPackagePathResolver(packagesPath, VSRestoreSettingsUtilities.GetFallbackFolders(_settings, packageSpec));
        }

        private async Task<IEnumerable<PackageReference>> GetInstalledPackageReferencesAsync(
            Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            var packages = new List<PackageReference>();

            if (_solutionManager != null
                && !string.IsNullOrEmpty(_solutionManager.SolutionDirectory))
            {
                var projectContext = new VSAPIProjectContext
                {
                    PackageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        ClientPolicyContext.GetClientPolicy(_settings, NullLogger.Instance),
                        NullLogger.Instance)
                };

                var nuGetProject = await _solutionManager.GetOrCreateProjectAsync(
                                    project,
                                    projectContext);

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

            try
            {
                return _threadingService.JoinableTaskFactory.Run(async delegate
                    {
                        var packages = new List<IVsPackageMetadata>();

                        if (_solutionManager != null
                            && !string.IsNullOrEmpty(_solutionManager.SolutionDirectory))
                        {
                            //switch to background thread
                            await TaskScheduler.Default;

                            NuGetPackageManager nuGetPackageManager = CreateNuGetPackageManager();

                            var projectContext = new VSAPIProjectContext
                            {
                                PackageExtractionContext = new PackageExtractionContext(
                                    PackageSaveMode.Defaultv2,
                                    PackageExtractionBehavior.XmlDocFileSaveMode,
                                    ClientPolicyContext.GetClientPolicy(_settings, NullLogger.Instance),
                                    NullLogger.Instance)
                            };

                            var nuGetProject = await _solutionManager.GetOrCreateProjectAsync(
                                                project,
                                                projectContext);

                            if (nuGetProject != null)
                            {
                                FallbackPackagePathResolver pathResolver = null;
                                var buildIntegratedProject = nuGetProject as BuildIntegratedNuGetProject;
                                if (buildIntegratedProject != null)
                                {
                                    pathResolver = await GetPackagesPathResolverAsync(buildIntegratedProject);
                                }

                                var installedPackages = await nuGetProject.GetInstalledPackagesAsync(CancellationToken.None);

                                foreach (var package in installedPackages)
                                {
                                    if (!package.PackageIdentity.HasVersion)
                                    {
                                        // Currently we are not supporting floating versions 
                                        // because of that we will skip this package so that it doesn't throw ArgumentNullException
                                        continue;
                                    }

                                    string installPath;
                                    if (buildIntegratedProject != null)
                                    {
                                        installPath = pathResolver.GetPackageDirectory(package.PackageIdentity.Id, package.PackageIdentity.Version);
                                    }
                                    else
                                    {
                                        // Get the install path for package
                                        installPath = nuGetPackageManager.PackagesFolderNuGetProject.GetInstalledPath(
                                                                    package.PackageIdentity);

                                        if (!string.IsNullOrEmpty(installPath))
                                        {
                                            // normalize the path and take the dir if the nupkg path was given
                                            var dir = new DirectoryInfo(installPath);
                                            installPath = dir.FullName;
                                        }
                                    }

                                    var metadata = new VsPackageMetadata(package.PackageIdentity, installPath);
                                    packages.Add(metadata);
                                }
                            }
                        }

                        return packages;
                    });
            }
            catch (Exception exception)
            {
                _telemetryProvider.PostFault(exception, typeof(VsPackageInstallerServices).FullName);
                throw;
            }
        }

        private NuGetPackageManager CreateNuGetPackageManager()
        {
            // Initialize package manager here since _solutionManager may be targeting different project now.
            return new NuGetPackageManager(
                _sourceRepositoryProvider,
                _settings,
                _solutionManager,
                _deleteOnRestartManager);
        }

        public bool IsPackageInstalled(Project project, string packageId)
        {
            return IsPackageInstalled(project, packageId, nugetVersion: null);
        }

        public bool IsPackageInstalledEx(Project project, string packageId, string versionString)
        {
            NuGetVersion version;
            if (versionString == null)
            {
                version = null;
            }
            else if (!NuGetVersion.TryParse(versionString, out version))
            {
                throw new ArgumentException(VsResources.InvalidNuGetVersionString, "versionString");
            }

            return IsPackageInstalled(project, packageId, version);
        }

        public bool IsPackageInstalled(Project project, string packageId, SemanticVersion version)
        {
            NuGetVersion nugetVersion;
            if (NuGetVersion.TryParse(version.ToString(), out nugetVersion))
            {
                return IsPackageInstalled(project, packageId, nugetVersion);
            }
            else
            {
                throw new ArgumentException(VsResources.InvalidNuGetVersionString, nameof(version));
            }
        }

        private bool IsPackageInstalled(Project project, string packageId, NuGetVersion nugetVersion)
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
            return _threadingService.JoinableTaskFactory.Run(async delegate
            {
                try
                {
                    IEnumerable<PackageReference> installedPackageReferences = await GetInstalledPackageReferencesAsync(project);

                    return PackageServiceUtilities.IsPackageInList(installedPackageReferences, packageId, nugetVersion);
                }
                catch (Exception exception)
                {
                    await _telemetryProvider.PostFaultAsync(exception, typeof(VsPackageInstallerServices).FullName);
                    throw;
                }
            });
        }
    }
}
