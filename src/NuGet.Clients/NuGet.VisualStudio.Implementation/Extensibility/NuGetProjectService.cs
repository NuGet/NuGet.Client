// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.VisualStudio.Contracts;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    internal class NuGetProjectService : INuGetProjectService
    {
        private readonly IProjectSystemCache _projectSystemCache;

        public NuGetProjectService(IProjectSystemCache projectSystemCache)
        {
            _projectSystemCache = projectSystemCache ?? throw new ArgumentNullException(nameof(projectSystemCache));
        }

        public async Task<GetInstalledPackagesResult> GetInstalledPackagesAsync(string project, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(project))
            {
                throw new ArgumentNullException(paramName: nameof(project));
            }

            await TaskScheduler.Default;

            if (!_projectSystemCache.TryGetNuGetProject(project, out var nuGetProject))
            {
                return NuGetContractsFactory.CreateGetInstalledPackagesResult(GetInstalledPackageResultStatus.ProjectNotReady, packages: null);
            }

            var status = GetInstalledPackageResultStatus.Successful;

            if (_projectSystemCache.TryGetProjectRestoreInfo(project, out _, out var nominationMessages))
            {
                if (nominationMessages?.Any(m => m.Level == LogLevel.Error) == true)
                {
                    status = GetInstalledPackageResultStatus.ProjectInvalid;
                }
            }

            var packageReferences = await nuGetProject.GetInstalledPackagesAsync(cancellationToken);

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
