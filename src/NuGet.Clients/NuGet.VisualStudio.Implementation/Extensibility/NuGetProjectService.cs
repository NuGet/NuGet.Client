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
using StreamJsonRpc;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    internal class NuGetProjectService : INuGetProjectService
    {
        private readonly IVsSolutionManager _solutionManager;

        public NuGetProjectService(IVsSolutionManager solutionManager)
        {
            _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        }

        public async Task<InstalledPackagesResult> GetInstalledPackagesAsync(string projectId, CancellationToken cancellationToken)
        {
            try
            {
                var projectGuid = Guid.Parse(projectId);

                // normalize guid, just in case.
                projectId = projectGuid.ToString();
            }
            catch (Exception e) when (e is FormatException || e is ArgumentNullException)
            {
                throw new RemoteInvocationException(e.Message, NuGetServices.ArgumentException, errorData: null);
            }

            // Just in case we're on the UI thread, switch to background thread. Very low cost (does not schedule new task) if already on background thread.
            await TaskScheduler.Default;

            NuGetProject project = await _solutionManager.GetNuGetProjectAsync(projectId);
            if (project == null)
            {
                return NuGetContractsFactory.CreateGetInstalledPackagesResult(InstalledPackageResultStatus.ProjectNotReady, packages: null);
            }

            var status = InstalledPackageResultStatus.Successful;
            List<NuGetInstalledPackage> installedPackages;

            if (project is BuildIntegratedNuGetProject packageReferenceProject)
            {
                var cacheContext = new DependencyGraphCacheContext();
                var (packageSpecs, messages) = await packageReferenceProject.GetPackageSpecsAndAdditionalMessagesAsync(cacheContext);
                if (messages?.Any(m => m.Level == LogLevel.Error) == true)
                {
                    status = InstalledPackageResultStatus.ProjectInvalid;
                }

                var packageSpec = packageSpecs.Single(s => s.RestoreMetadata.ProjectStyle == ProjectModel.ProjectStyle.PackageReference || s.RestoreMetadata.ProjectStyle == ProjectModel.ProjectStyle.ProjectJson);
                var packagesPath = VSRestoreSettingsUtilities.GetPackagesPath(NullSettings.Instance, packageSpec);
                FallbackPackagePathResolver pathResolver = new FallbackPackagePathResolver(packagesPath, VSRestoreSettingsUtilities.GetFallbackFolders(NullSettings.Instance, packageSpec));

                var packageReferences = await project.GetInstalledPackagesAsync(cancellationToken);

                installedPackages = packageReferences.Select(p => ToNuGetInstalledPackage(p, pathResolver))
                    .ToList();
            }
            else if (project is MSBuildNuGetProject packagesConfigProject)
            {
                FolderNuGetProject packagesFolder = packagesConfigProject.FolderNuGetProject;

                var packageReferences = await project.GetInstalledPackagesAsync(cancellationToken);

                installedPackages = packageReferences.Select(p => ToNuGetInstalledPackage(p, packagesFolder))
                    .ToList();
            }
            else
            {
                // unknown/unsupported project type
                installedPackages = new List<NuGetInstalledPackage>(0);
            }

            return NuGetContractsFactory.CreateGetInstalledPackagesResult(status, installedPackages);
        }

        private NuGetInstalledPackage ToNuGetInstalledPackage(Packaging.PackageReference packageReference, FallbackPackagePathResolver pathResolver)
        {
            var id = packageReference.PackageIdentity.Id;

            var versionRange = packageReference.AllowedVersions;
            string requestedRange =
                packageReference.AllowedVersions.OriginalString // most packages
                ?? packageReference.AllowedVersions.ToShortString();
            string version = versionRange.MinVersion.OriginalVersion ?? versionRange.MinVersion.ToNormalizedString();
            var installPath = pathResolver.GetPackageDirectory(id, version);

            return NuGetContractsFactory.CreateNuGetInstalledPackage(id, requestedRange, version, installPath);
        }

        private NuGetInstalledPackage ToNuGetInstalledPackage(Packaging.PackageReference packageReference, FolderNuGetProject packagesFolder)
        {
            var id = packageReference.PackageIdentity.Id;
            string requestedRange = packageReference.PackageIdentity.Version.OriginalVersion;
            string version = requestedRange;
            var installPath = packagesFolder.GetInstalledPath(packageReference.PackageIdentity);

            return NuGetContractsFactory.CreateNuGetInstalledPackage(id, requestedRange, version, installPath);
        }
    }
}
