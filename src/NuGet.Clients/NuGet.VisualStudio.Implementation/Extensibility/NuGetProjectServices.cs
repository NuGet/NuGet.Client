// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.VisualStudio.Contracts;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    internal class NuGetProjectServices : INuGetProjectServices
    {
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<IProjectSystemCache> _projectSystemCache;

        public NuGetProjectServices(
            Microsoft.VisualStudio.Threading.AsyncLazy<IProjectSystemCache> projectSystemCache)
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

            var projectSystemCache = await _projectSystemCache.GetValueAsync(cancellationToken);

            if (!projectSystemCache.TryGetNuGetProject(project, out var nuGetProject))
            {
                return ContractsFactory.CreateGetInstalledPackagesResult(GetInstalledPackageResultStatus.ProjectNotReady, packages: null);
            }

            var status = GetInstalledPackageResultStatus.Successful;

            if (projectSystemCache.TryGetProjectRestoreInfo(project, out _, out var nominationMessages))
            {
                if (nominationMessages?.Any(m => m.Level == LogLevel.Error) == true)
                {
                    status = GetInstalledPackageResultStatus.ProjectInvalid;
                }
            }

            var packageReferences = await nuGetProject.GetInstalledPackagesAsync(cancellationToken);

            var installedPackages = packageReferences.Select(ToNuGetInstalledPackage)
                .ToList();

            return ContractsFactory.CreateGetInstalledPackagesResult(status, installedPackages);
        }

        private NuGetInstalledPackage ToNuGetInstalledPackage(Packaging.PackageReference packageReference)
        {
            var id = packageReference.PackageIdentity.Id;
            var version = packageReference.AllowedVersions.MinVersion.ToNormalizedString();

            return ContractsFactory.CreateNuGetInstalledPackage(id, version);
        }
    }
}
