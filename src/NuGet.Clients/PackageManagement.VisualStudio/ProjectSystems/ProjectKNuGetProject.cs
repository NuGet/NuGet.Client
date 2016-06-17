// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.Interop;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio
{
    public class NuGetPackageMoniker : INuGetPackageMoniker
    {
        public string Id { get; set; }

        public string Version { get; set; }
    }

    public class ProjectKNuGetProject : ProjectKNuGetProjectBase
    {
        private INuGetPackageManager _project;

        public ProjectKNuGetProject(INuGetPackageManager project, string projectName, string uniqueName)
        {
            _project = project;
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, uniqueName);

            var supportedFrameworks = _project.GetSupportedFrameworksAsync(CancellationToken.None)
                .Result
                .Select(f => NuGetFramework.Parse(f.FullName));

            InternalMetadata.Add(NuGetProjectMetadataKeys.SupportedFrameworks, supportedFrameworks);
        }

        private static bool IsCompatible(
            NuGetFramework projectFrameworkName,
            IEnumerable<NuGetFramework> packageSupportedFrameworks)
        {
            if (packageSupportedFrameworks.Any())
            {
                return packageSupportedFrameworks.Any(packageSupportedFramework =>
                    DefaultCompatibilityProvider.Instance.IsCompatible(
                        projectFrameworkName,
                        packageSupportedFramework));
            }

            // No supported frameworks means that everything is supported.
            return true;
        }

        public override async Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            if (downloadResourceResult == null)
            {
                throw new ArgumentNullException(nameof(downloadResourceResult));
            }

            var packageStream = downloadResourceResult.PackageStream;
            if (!packageStream.CanSeek)
            {
                throw new ArgumentException(ProjectManagement.Strings.PackageStreamShouldBeSeekable);
            }

            nuGetProjectContext.Log(MessageLevel.Info, Strings.InstallingPackage, packageIdentity);

            packageStream.Seek(0, SeekOrigin.Begin);
            
            // Get additional information from the package that the INuGetPackageManager can act on.
            IEnumerable<NuGetFramework> supportedFrameworks;
            IEnumerable<PackageType> packageTypes;
            using (var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                supportedFrameworks = packageReader.GetSupportedFrameworks();
                packageTypes = packageReader.GetPackageTypes();
            }

            var args = new Dictionary<string, object>();

            args["Frameworks"] = supportedFrameworks
                .Where(f => f.IsSpecificFramework)
                .ToArray();

            args["PackageTypes"] = packageTypes
                .ToArray();

            await _project.InstallPackageAsync(
                new NuGetPackageMoniker
                {
                    Id = packageIdentity.Id,
                    Version = packageIdentity.Version.ToNormalizedString()
                },
                args,
                logger: null,
                progress: null,
                cancellationToken: token);

            return true;
        }

        public override async Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            nuGetProjectContext.Log(MessageLevel.Info, Strings.UninstallingPackage, packageIdentity);

            var args = new Dictionary<string, object>();
            await _project.UninstallPackageAsync(
                new NuGetPackageMoniker
                {
                    Id = packageIdentity.Id,
                    Version = packageIdentity.Version.ToNormalizedString()
                },
                args,
                logger: null,
                progress: null,
                cancellationToken: token);
            return true;
        }

        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            var result = new List<PackageReference>();
            foreach (object item in await _project.GetInstalledPackagesAsync(token))
            {
                PackageIdentity identity = null;

                var moniker = item as INuGetPackageMoniker;
                if (moniker != null)
                {
                    identity = new PackageIdentity(
                        moniker.Id,
                        NuGetVersion.Parse(moniker.Version));
                }
                else
                {
                    // otherwise, item is the file name of the nupkg file
                    var fileName = item as string;
                    using (var packageReader = new PackageArchiveReader(fileName))
                    {
                        identity = packageReader.GetIdentity();
                    }
                }

                result.Add(new PackageReference(
                    identity,
                    targetFramework: null));
            }

            return result;
        }
    }
}
