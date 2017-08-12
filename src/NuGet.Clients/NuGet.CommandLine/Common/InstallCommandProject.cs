// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Project used for nuget.exe install
    /// </summary>
    internal class InstallCommandProject : FolderNuGetProject
    {
        private readonly NuGetFramework _framework;
        private readonly PackagePathResolver _packagePathResolver;

        public InstallCommandProject(string root, PackagePathResolver packagePathResolver, NuGetFramework targetFramework)
            : base(root, packagePathResolver, targetFramework)
        {
            _packagePathResolver = packagePathResolver;
            _framework = targetFramework;
        }

        /// <summary>
        /// Asynchronously gets installed packages.
        /// </summary>
        /// <remarks>This is used only for the install command, not in PM.</remarks>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IEnumerable{PackageReference}" />.</returns>
        public Task<IEnumerable<PackageReference>> GetFolderPackagesAsync(CancellationToken token)
        {
            var packages = Enumerable.Empty<LocalPackageInfo>();

            if (Directory.Exists(Root))
            {
                if (_packagePathResolver.UseSideBySidePaths)
                {
                    // Id.Version
                    packages = LocalFolderUtility.GetPackagesConfigFolderPackages(Root, NullLogger.Instance);
                }
                else
                {
                    // Id
                    packages = LocalFolderUtility.GetPackagesV2(Root, NullLogger.Instance);
                }
            }

            return Task.FromResult<IEnumerable<PackageReference>>(
                LocalFolderUtility.GetDistinctPackages(packages)
                    .Select(e => new PackageReference(e.Identity, _framework))
                    .ToList());
        }

        /// <summary>
        /// Asynchronously gets installed packages.
        /// </summary>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IEnumerable{PackageReference}" />.</returns>
        public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            if (!_packagePathResolver.UseSideBySidePaths)
            {
                // Without versions packages must be uninstalled to update them.
                return GetFolderPackagesAsync(token);
            }

            // For SxS scenarios PackageManagement should not read these references, this would cause uninstalls.
            return Task.FromResult(Enumerable.Empty<PackageReference>());
        }

        /// <summary>
        /// Asynchronously uninstalls a package.
        /// </summary>
        /// <param name="packageIdentity">A package identity.</param>
        /// <param name="nuGetProjectContext">A NuGet project context.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="bool" />
        /// indication successfulness of the operation.</returns>
        public override async Task<bool> UninstallPackageAsync(
            PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var installedPackagesList = await GetInstalledPackagesAsync(token);
            var packageReference = installedPackagesList.Where(p => p.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if (packageReference == null)
            {
                // Package does not exist
                return false;
            }

            // Delete the package for nuget.exe install/update scenarios
            return await DeletePackage(packageIdentity, nuGetProjectContext, token);
        }
    }
}
