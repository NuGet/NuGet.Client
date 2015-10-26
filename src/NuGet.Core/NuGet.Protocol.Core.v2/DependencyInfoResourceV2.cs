// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using V3PackageDependency = NuGet.Packaging.Core.PackageDependency;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// A V2 dependency info gatherer.
    /// </summary>
    public class DependencyInfoResourceV2 : DependencyInfoResource
    {
        private readonly IPackageRepository V2Client;
        private readonly FrameworkReducer _frameworkReducer = new FrameworkReducer();
        private readonly SourceRepository _source;

        public DependencyInfoResourceV2(V2Resource resource, SourceRepository source)
        {
            V2Client = resource.V2Client;
            _source = source;
        }

        /// <summary>
        /// Retrieve dependency info for a single package.
        /// </summary>
        /// <param name="package">package id and version</param>
        /// <param name="projectFramework">project target framework. This is used for finding the dependency group</param>
        /// <param name="token">cancellation token</param>
        /// <returns>
        /// Returns dependency info for the given package if it exists. If the package is not found null is
        /// returned.
        /// </returns>
        public override Task<SourcePackageDependencyInfo> ResolvePackage(PackageIdentity package, NuGetFramework projectFramework, CancellationToken token)
        {
            if (package == null)
            {
                throw new ArgumentNullException(null, nameof(package));
            }

            if (projectFramework == null)
            {
                throw new ArgumentNullException(nameof(projectFramework));
            }

            SourcePackageDependencyInfo result = null;

            SemanticVersion legacyVersion;

            // attempt to parse the semver into semver 1.0.0, if this fails then the v2 client would
            // not be able to find it anyways and we should return null
            if (SemanticVersion.TryParse(package.Version.ToString(), out legacyVersion))
            {
                try
                {
                    // Retrieve all packages
                    var repoPackage = GetRepository().FindPackage(
                        package.Id,
                        legacyVersion,
                        allowPrereleaseVersions: true,
                        allowUnlisted: true);

                    if (repoPackage != null)
                    {
                        // convert to v3 type
                        result = CreateDependencyInfo(repoPackage, projectFramework);
                    }
                }
                catch (Exception ex)
                {
                    // Wrap exceptions coming from the server with a user friendly message
                    var error = String.Format(CultureInfo.CurrentUICulture, Strings.Protocol_PackageMetadataError, package, V2Client.Source);

                    throw new NuGetProtocolException(error, ex);
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Retrieve dependency info for a single package.
        /// </summary>
        /// <param name="package">package id and version</param>
        /// <param name="projectFramework">project target framework. This is used for finding the dependency group</param>
        /// <param name="token">cancellation token</param>
        /// <returns>
        /// Returns dependency info for the given package if it exists. If the package is not found null is
        /// returned.
        /// </returns>
        public override Task<IEnumerable<SourcePackageDependencyInfo>> ResolvePackages(string packageId, NuGetFramework projectFramework, CancellationToken token)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (projectFramework == null)
            {
                throw new ArgumentNullException(nameof(projectFramework));
            }

            List<SourcePackageDependencyInfo> results;

            try
            {
                var repo = GetRepository();

                // Retrieve all packages
                var repoPackages = repo.FindPackagesById(packageId);

                // Convert from v2 to v3 types and enumerate the list to finish all server requests before returning
                results = repoPackages.Select(p => CreateDependencyInfo(p, projectFramework)).ToList();
            }
            catch (Exception ex)
            {
                // Wrap exceptions coming from the server with a user friendly message
                var error = String.Format(CultureInfo.CurrentUICulture, Strings.Protocol_PackageMetadataError, packageId, V2Client.Source);

                throw new NuGetProtocolException(error, ex);
            }

            return Task.FromResult<IEnumerable<SourcePackageDependencyInfo>>(results);
        }

        /// <summary>
        /// Convert a V2 IPackage into a V3 PackageDependencyInfo
        /// </summary>
        private SourcePackageDependencyInfo CreateDependencyInfo(IPackage packageVersion, NuGetFramework projectFramework)
        {
            var deps = Enumerable.Empty<V3PackageDependency>();

            var identity = new PackageIdentity(packageVersion.Id, NuGetVersion.Parse(packageVersion.Version.ToString()));
            if (packageVersion.DependencySets != null
                && packageVersion.DependencySets.Any())
            {
                // Take only the dependency group valid for the project TFM
                var nearestFramework = _frameworkReducer.GetNearest(projectFramework, packageVersion.DependencySets.Select(GetFramework));

                if (nearestFramework != null)
                {
                    var matches = packageVersion.DependencySets.Where(e => (GetFramework(e).Equals(nearestFramework)));
                    IEnumerable<PackageDependency> dependencies = matches.First().Dependencies;
                    deps = dependencies.Select(item => GetPackageDependency(item));
                }
            }

            SourcePackageDependencyInfo result = null;

            var dataPackage = packageVersion as DataServicePackage;

            if (dataPackage != null)
            {
                // Online package
                result = new SourcePackageDependencyInfo(
                    identity,
                    deps,
                    PackageExtensions.IsListed(packageVersion),
                    _source,
                    dataPackage.DownloadUrl,
                    dataPackage.PackageHash);
            }
            else
            {
                // Offline package
                result = new SourcePackageDependencyInfo(
                    identity,
                    deps,
                    PackageExtensions.IsListed(packageVersion),
                    _source,
                    downloadUri: null,
                    packageHash: null);
            }

            return result;
        }

        private IPackageRepository GetRepository()
        {
            var repository = V2Client as DataServicePackageRepository;

            if (repository != null)
            {
                var sourceUri = new Uri(repository.Source);
                repository = new DataServicePackageRepository(sourceUri);
            }

            // If the repository is not a DataServicePackageRepository just return the current one.
            return repository ?? V2Client;
        }

        private static NuGetFramework GetFramework(PackageDependencySet dependencySet)
        {
            var fxName = NuGetFramework.AnyFramework;
            if (dependencySet.TargetFramework != null)
            {
                fxName = NuGetFramework.Parse(dependencySet.TargetFramework.FullName);
            }

            return fxName;
        }

        private static V3PackageDependency GetPackageDependency(PackageDependency dependency)
        {
            var id = dependency.Id;
            var versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new V3PackageDependency(id, versionRange);
        }
    }
}
