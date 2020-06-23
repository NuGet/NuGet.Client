// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
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

            if (project is BuildIntegratedNuGetProject buildIntegratedNuGetProject)
            {
                var cacheContext = new DependencyGraphCacheContext();
                var (_, messages) = await buildIntegratedNuGetProject.GetPackageSpecsAndAdditionalMessagesAsync(cacheContext);
                if (messages?.Any(m => m.Level == LogLevel.Error) == true)
                {
                    status = InstalledPackageResultStatus.ProjectInvalid;
                }
            }

            var packageReferences = await project.GetInstalledPackagesAsync(cancellationToken);

            var installedPackages = packageReferences.Select(ToNuGetInstalledPackage)
                .ToList();

            return NuGetContractsFactory.CreateGetInstalledPackagesResult(status, installedPackages);
        }

        private NuGetInstalledPackage ToNuGetInstalledPackage(Packaging.PackageReference packageReference)
        {
            var id = packageReference.PackageIdentity.Id;

            var versionRange = packageReference.AllowedVersions;
            string requestedRange;
            string requestedVersion;
            if (versionRange != null)
            {
                requestedRange =
                    packageReference.AllowedVersions.OriginalString // most packages
                    ?? packageReference.AllowedVersions.ToShortString(); // implicit packages
                requestedVersion = versionRange.MinVersion.OriginalVersion ?? versionRange.MinVersion.ToNormalizedString();
            }
            else
            {
                // packages.config project
                requestedRange = packageReference.PackageIdentity.Version.OriginalVersion;
                requestedVersion = requestedRange;
            }

            return NuGetContractsFactory.CreateNuGetInstalledPackage(id, requestedRange, requestedVersion);
        }
    }
}
