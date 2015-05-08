// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.DependencyInfo;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Retrieves all packages and dependencies from a V3 source.
    /// </summary>
    public sealed class DependencyInfoResourceV3 : DependencyInfoResource
    {
        private readonly HttpClient _client;
        private readonly RegistrationResourceV3 _regResource;
        private readonly PackageSource _source;
        private readonly ConcurrentDictionary<Uri, JObject> _cache = new ConcurrentDictionary<Uri, JObject>();

        /// <summary>
        /// Dependency info resource
        /// </summary>
        /// <param name="client">Http client</param>
        /// <param name="regResource">Registration blob resource</param>
        public DependencyInfoResourceV3(HttpClient client, RegistrationResourceV3 regResource, PackageSource source)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (regResource == null)
            {
                throw new ArgumentNullException(nameof(regResource));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _client = client;
            _regResource = regResource;
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
        public override async Task<PackageDependencyInfo> ResolvePackage(PackageIdentity package, NuGetFramework projectFramework, CancellationToken token)
        {
            try
            {
                PackageDependencyInfo result = null;

                // Construct the registration index url
                var uri = _regResource.GetUri(package.Id);

                // Retrieve the registration blob
                var singleVersion = new VersionRange(minVersion: package.Version, includeMinVersion: true, maxVersion: package.Version, includeMaxVersion: true);
                var regInfo = await ResolverMetadataClient.GetRegistrationInfo(_client, uri, singleVersion, projectFramework, _cache);

                // regInfo is null if the server returns a 404 for the package to indicate that it does not exist
                if (regInfo != null)
                {
                    // Parse package and dependeny info from the blob
                    result = GetPackagesFromRegistration(regInfo, token).FirstOrDefault();
                }

                return result;
            }
            catch (Exception ex)
            {
                // Wrap exceptions coming from the server with a user friendly message
                var error = String.Format(CultureInfo.CurrentUICulture, Strings.Protocol_PackageMetadataError, package, _source.Source);

                throw new NuGetProtocolException(error, ex);
            }
        }

        /// <summary>
        /// Retrieve the available packages and their dependencies.
        /// </summary>
        /// <remarks>Includes prerelease packages</remarks>
        /// <param name="packageId">package Id to search</param>
        /// <param name="projectFramework">project target framework. This is used for finding the dependency group</param>
        /// <param name="token">cancellation token</param>
        /// <returns>available packages and their dependencies</returns>
        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(string packageId, NuGetFramework projectFramework, CancellationToken token)
        {
            try
            {
                var results = new List<PackageDependencyInfo>();

                // Construct the registration index url
                var uri = _regResource.GetUri(packageId);

                // Retrieve the registration blob
                var regInfo = await ResolverMetadataClient.GetRegistrationInfo(_client, uri, VersionRange.All, projectFramework, _cache);

                // regInfo is null if the server returns a 404 for the package to indicate that it does not exist
                if (regInfo != null)
                {
                    // Parse package and dependeny info from the blob
                    var packages = GetPackagesFromRegistration(regInfo, token);

                    // Filter on prerelease
                    results.AddRange(packages);
                }

                return results;
            }
            catch (Exception ex)
            {
                // Wrap exceptions coming from the server with a user friendly message
                var error = String.Format(CultureInfo.CurrentUICulture, Strings.Protocol_PackageMetadataError, packageId, _source.Source);

                throw new NuGetProtocolException(error, ex);
            }
        }

        /// <summary>
        /// Retrieve the available packages and their dependencies.
        /// </summary>
        /// <remarks>Includes prerelease packages</remarks>
        /// <param name="packageId">package Id to search</param>
        /// <param name="projectFramework">project target framework. This is used for finding the dependency group</param>
        /// <param name="token">cancellation token</param>
        /// <returns>available packages and their dependencies</returns>
        public override Task<IEnumerable<RemoteSourceDependencyInfo>> ResolvePackages(string packageId, CancellationToken token)
        {
            try
            {
                // Construct the registration index url
                var uri = _regResource.GetUri(packageId);

                // Retrieve the registration blob
                return ResolverMetadataClient.GetDependencies(_client, uri, VersionRange.All, _cache);
            }
            catch (Exception ex)
            {
                // Wrap exceptions coming from the server with a user friendly message
                var error = String.Format(CultureInfo.CurrentUICulture, Strings.Protocol_PackageMetadataError, packageId, _source.Source);

                throw new NuGetProtocolException(error, ex);
            }
        }

        /// <summary>
        /// Retrieve dependency info from a registration blob
        /// </summary>
        private static IEnumerable<PackageDependencyInfo> GetPackagesFromRegistration(RegistrationInfo registration, CancellationToken token)
        {
            foreach (var pkgInfo in registration.Packages)
            {
                var dependencies = pkgInfo.Dependencies.Select(dep => new PackageDependency(dep.Id, dep.Range));
                yield return new PackageDependencyInfo(registration.Id, pkgInfo.Version, dependencies);
            }

            yield break;
        }
    }
}
