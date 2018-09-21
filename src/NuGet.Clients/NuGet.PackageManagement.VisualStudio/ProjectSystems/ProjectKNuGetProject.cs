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
        private const string StateKey = "PackageUpgradeState";

        private INuGetPackageManager _project;

        /// <summary>
        /// When performing an update operation from the UI, this action is converted to two lower
        /// level actions: uninstall then install. Unfortunately, this means that any state removed
        /// during the uninstall that is not included in the install operation is lost. This
        /// dictionary is used to maintain this state between uninstall and install operations. The
        /// state is cleared during the <see cref="PreProcessAsync"/> and
        /// <see cref="PostProcessAsync"/> steps.
        /// </summary>
        private readonly Dictionary<string, object> _packageUpdateState
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public override Task PostProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            // Technically we only need to clear state during the PreProcessAsync step, but clearing
            // here as well means we're not holding on to information that will never be used thus
            // using less memory.
            _packageUpdateState.Clear();

            return base.PostProcessAsync(nuGetProjectContext, token);
        }

        public override Task PreProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            _packageUpdateState.Clear();

            return base.PreProcessAsync(nuGetProjectContext, token);
        }

        public ProjectKNuGetProject(INuGetPackageManager project, string projectName, string uniqueName, string projectId)
        {
            _project = project;
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, uniqueName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);

            var supportedFrameworks = _project.GetSupportedFrameworksAsync(CancellationToken.None)
                .Result
                .Select(f => NuGetFramework.Parse(f.FullName));

            InternalMetadata.Add(NuGetProjectMetadataKeys.SupportedFrameworks, supportedFrameworks);
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

            // Uninstall the package if it is already installed. This should only happen when an
            // update occurred from Install-Package PMC command, the Browse tab in the UI, or the
            // Installed tab in the UI. An update from the Updates tab has an explicit Uninstall
            // action before the install.
            var installedPackages = await GetInstalledPackagesAsync(token);
            var packageToReplace = installedPackages
                .Where(pr => StringComparer.OrdinalIgnoreCase.Equals(pr.PackageIdentity.Id, packageIdentity.Id))
                .FirstOrDefault();

            if (packageToReplace != null)
            {
                await UninstallPackageAsync(packageToReplace.PackageIdentity, nuGetProjectContext, token);
            }

            nuGetProjectContext.Log(MessageLevel.Info, Strings.InstallingPackage, packageIdentity);

            // Get additional information from the package that the INuGetPackageManager can act on.
            packageStream.Seek(0, SeekOrigin.Begin);
            
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

            object state;
            if (_packageUpdateState.TryGetValue(packageIdentity.Id, out state))
            {
                args[StateKey] = state;
            }

            // Perform the actual installation by delegating to INuGetPackageManager.
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

            // It's possible that the underlying project is trying to pass us back some state to be
            // returned to it in a subsequent install of the same package identity.
            object state;
            if (args.TryGetValue(StateKey, out state))
            {
                _packageUpdateState[packageIdentity.Id] = state;
            }

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
                    // As with build integrated projects (UWP project.json), treat the version as a
                    // version range and use the minimum version of that range. Eventually, this
                    // method should return VersionRange instances, not NuGetVersion so that the
                    // UI can express the full project.json intent. This improvement is tracked
                    // here: https://github.com/NuGet/Home/issues/3101
                    var versionRange = VersionRange.Parse(moniker.Version);
                    var version = versionRange.MinVersion;

                    identity = new PackageIdentity(moniker.Id, version);
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
