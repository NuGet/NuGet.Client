// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.VisualStudio.Contracts;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    public sealed class NuGetProjectService : INuGetProjectService
    {
        private readonly IVsSolutionManager _solutionManager;
        private readonly ISettings _settings;

        public NuGetProjectService(IVsSolutionManager solutionManager, ISettings settings)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<InstalledPackagesResult> GetInstalledPackagesAsync(Guid projectId, CancellationToken cancellationToken)
        {
            // Just in case we're on the UI thread, switch to background thread. Very low cost (does not schedule new task) if already on background thread.
            await TaskScheduler.Default;

            NuGetProject project = await _solutionManager.GetNuGetProjectAsync(projectId.ToString());
            if (project == null)
            {
                return NuGetContractsFactory.CreateInstalledPackagesResult(InstalledPackageResultStatus.ProjectNotReady, packages: null);
            }

            InstalledPackageResultStatus status;
            IReadOnlyCollection<NuGetInstalledPackage> installedPackages;

            switch (project)
            {
                case BuildIntegratedNuGetProject packageReferenceProject:
                    (status, installedPackages) = await GetInstalledPackagesAsync(packageReferenceProject, cancellationToken);
                    break;

                case MSBuildNuGetProject packagesConfigProject:
                    (status, installedPackages) = await GetInstalledPackagesAsync(packagesConfigProject, cancellationToken);
                    break;

                default:
                    (status, installedPackages) = await GetInstalledPackagesAsync(project, cancellationToken);
                    break;
            }

            return NuGetContractsFactory.CreateInstalledPackagesResult(status, installedPackages);
        }

        private async Task<(InstalledPackageResultStatus, IReadOnlyCollection<NuGetInstalledPackage>)> GetInstalledPackagesAsync(BuildIntegratedNuGetProject project, CancellationToken cancellationToken)
        {
            NuGetInstalledPackage ToNuGetInstalledPackage(PackageReference packageReference, FallbackPackagePathResolver pathResolver)
            {
                var id = packageReference.PackageIdentity.Id;

                var versionRange = packageReference.AllowedVersions;
                string requestedRange =
                    packageReference.AllowedVersions.OriginalString // most packages
                    ?? packageReference.AllowedVersions.ToShortString();
                string version = versionRange.MinVersion.OriginalVersion ?? versionRange.MinVersion.ToNormalizedString();
                var installPath = pathResolver.GetPackageDirectory(id, version);
                bool directDependency = true;

                return NuGetContractsFactory.CreateNuGetInstalledPackage(id, requestedRange, version, installPath, directDependency);
            }

            InstalledPackageResultStatus status;
            IReadOnlyCollection<NuGetInstalledPackage> installedPackages;

            var cacheContext = new DependencyGraphCacheContext();
            var (packageSpecs, messages) = await project.GetPackageSpecsAndAdditionalMessagesAsync(cacheContext);
            if (messages?.Any(m => m.Level == LogLevel.Error) == true)
            {
                // Although we know that the project will fail to restore, we may still know about some direct dependencies, so let's return the packages that we know about.
                status = InstalledPackageResultStatus.ProjectInvalid;
            }
            else
            {
                status = InstalledPackageResultStatus.Successful;
            }

            var packageSpec = packageSpecs.Single(s => s.RestoreMetadata.ProjectStyle == ProjectModel.ProjectStyle.PackageReference || s.RestoreMetadata.ProjectStyle == ProjectModel.ProjectStyle.ProjectJson);
            var packagesPath = VSRestoreSettingsUtilities.GetPackagesPath(_settings, packageSpec);
            FallbackPackagePathResolver pathResolver = new FallbackPackagePathResolver(packagesPath, VSRestoreSettingsUtilities.GetFallbackFolders(_settings, packageSpec));

            var packageReferences = await project.GetInstalledPackagesAsync(cancellationToken);

            installedPackages = packageReferences.Select(p => ToNuGetInstalledPackage(p, pathResolver))
                .ToList();

            return (status, installedPackages);
        }

        private async Task<(InstalledPackageResultStatus, IReadOnlyCollection<NuGetInstalledPackage>)> GetInstalledPackagesAsync(MSBuildNuGetProject project, CancellationToken cancellationToken)
        {
            NuGetInstalledPackage ToNuGetInstalledPackage(Packaging.PackageReference packageReference, FolderNuGetProject packagesFolder)
            {
                var id = packageReference.PackageIdentity.Id;
                string requestedRange = packageReference.PackageIdentity.Version.OriginalVersion;
                string version = requestedRange;
                var installPath = packagesFolder.GetInstalledPath(packageReference.PackageIdentity);
                bool directDependency = true;

                return NuGetContractsFactory.CreateNuGetInstalledPackage(id, requestedRange, version, installPath, directDependency);
            }

            FolderNuGetProject packagesFolder = project.FolderNuGetProject;

            var packageReferences = await project.GetInstalledPackagesAsync(cancellationToken);

            var installedPackages = packageReferences.Select(p => ToNuGetInstalledPackage(p, packagesFolder))
                .ToList();
            var status = InstalledPackageResultStatus.Successful;

            return (status, installedPackages);
        }

        private async Task<(InstalledPackageResultStatus, IReadOnlyCollection<NuGetInstalledPackage>)> GetInstalledPackagesAsync(NuGetProject project, CancellationToken cancellationToken)
        {
            // NuGetProject type that doesn't extend MSBuildNuGetProject or BuildIntegratedNuGetProject?
            // At the time of writing, this codepath is impossible to reach.

            NuGetInstalledPackage ToNuGetInstalledPackage(PackageReference package)
            {
                string id = package.PackageIdentity.Id;
                string version = package.PackageIdentity.Version?.OriginalVersion
                    ?? package.PackageIdentity.Version?.ToNormalizedString()
                    ?? package.AllowedVersions?.MinVersion?.OriginalVersion
                    ?? package.AllowedVersions?.MinVersion?.ToNormalizedString();
                string requestedRange = package.AllowedVersions.OriginalString ?? version;
                string installPath = null;
                bool directDependency = true;

                return NuGetContractsFactory.CreateNuGetInstalledPackage(id, requestedRange, version, installPath, directDependency);
            }

            var status = InstalledPackageResultStatus.Unknown;

            var notImplementedException = new NotImplementedException($"Project type {project.GetType().Name} is not implemented");
            await TelemetryUtility.PostFaultAsync(notImplementedException, nameof(NuGetProjectService));

            IEnumerable<PackageReference> projectPackages = await project.GetInstalledPackagesAsync(cancellationToken);

            List<NuGetInstalledPackage> installedPackages =
                projectPackages.Select(ToNuGetInstalledPackage).ToList();

            return (status, installedPackages);
        }
    }
}
