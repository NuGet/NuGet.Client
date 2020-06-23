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
using StreamJsonRpc;

namespace NuGet.VisualStudio.Implementation.Extensibility
{
    internal class NuGetProjectService : INuGetProjectService
    {
        private readonly IProjectSystemCache _projectSystemCache;

        public NuGetProjectService(IProjectSystemCache projectSystemCache)
        {
            _projectSystemCache = projectSystemCache ?? throw new ArgumentNullException(nameof(projectSystemCache));
        }

        public async Task<InstalledPackagesResult> GetInstalledPackagesAsync(string project, CancellationToken cancellationToken)
        {
            try
            {
                var projectGuid = Guid.Parse(project);

                // normalize guid, just in case.
                project = projectGuid.ToString();
            }
            catch (Exception e) when (e is FormatException || e is ArgumentNullException)
            {
                throw new RemoteInvocationException(e.Message, NuGetServices.ArgumentException, errorData: null);
            }

            // Just in case we're on the UI thread, switch to background thread. Very low cost (does not schedule new task) if already on background thread.
            await TaskScheduler.Default;

            if (!_projectSystemCache.TryGetNuGetProject(project, out var nuGetProject))
            {
                return NuGetContractsFactory.CreateGetInstalledPackagesResult(InstalledPackageResultStatus.ProjectNotReady, packages: null);
            }

            var status = InstalledPackageResultStatus.Successful;

            if (_projectSystemCache.TryGetProjectRestoreInfo(project, out _, out var nominationMessages))
            {
                if (nominationMessages?.Any(m => m.Level == LogLevel.Error) == true)
                {
                    status = InstalledPackageResultStatus.ProjectInvalid;
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
