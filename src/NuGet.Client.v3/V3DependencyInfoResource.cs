using Newtonsoft.Json.Linq;
using NuGet.Client.DependencyInfo;
using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Retrieves all packages and dependencies from a V3 source.
    /// </summary>
    public sealed class V3DependencyInfoResource : DepedencyInfoResource
    {
        private readonly HttpClient _client;
        private readonly ConcurrentDictionary<Uri, JObject> _cache;
        private readonly V3RegistrationResource _regResource;
        private static readonly VersionRange AllVersions = new VersionRange(null, true, null, true, true);

        /// <summary>
        /// Dependency info resource
        /// </summary>
        /// <param name="client">Http client</param>
        /// <param name="regResource">Registration blob resource</param>
        public V3DependencyInfoResource(HttpClient client, V3RegistrationResource regResource)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (regResource == null)
            {
                throw new ArgumentNullException("regResource");
            }

            _client = client;
            _cache = new ConcurrentDictionary<Uri, JObject>();
            _regResource = regResource;
        }

        /// <summary>
        /// Retrieves all the package and all dependant packages
        /// </summary>
        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<PackageIdentity> packages, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            // compare results based on the id/version
            HashSet<PackageDependencyInfo> results = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);

            foreach (var package in packages)
            {
                var range = new VersionRange(package.Version, true, package.Version, true);

                foreach (var result in await GetPackagesFromRegistration(package.Id, range, projectFramework, token))
                {
                    results.Add(result);
                }
            }

            return results.Where(e => includePrerelease || !e.Version.IsPrerelease);
        }

        /// <summary>
        /// Gives all packages for an Id, and all dependencies recursively.
        /// </summary>
        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<string> packageIds, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            HashSet<PackageDependencyInfo> result = new HashSet<PackageDependencyInfo>(PackageIdentityComparer.Default);

            foreach (string packageId in packageIds)
            {
                result.UnionWith(await GetPackagesFromRegistration(packageId, AllVersions, projectFramework, token));
            }

            return result.Where(e => includePrerelease || !e.Version.IsPrerelease);
        }

        /// <summary>
        /// Helper for finding all versions of a package and all dependencies.
        /// </summary>
        private async Task<IEnumerable<PackageDependencyInfo>> GetPackagesFromRegistration(string packageId, VersionRange range, NuGetFramework projectFramework, CancellationToken cancellationToken)
        {
            HashSet<PackageDependencyInfo> results = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);

            Uri uri = await _regResource.GetUriAsync(packageId, cancellationToken);

            try
            {
                var regInfo = await ResolverMetadataClient.GetRegistrationInfo(_client, uri, range, projectFramework, _cache);

                var result = await ResolverMetadataClient.GetTree(_client, regInfo, projectFramework, _cache);

                foreach (var currentPackage in GetPackagesFromRegistration(result, cancellationToken))
                {
                    results.Add(currentPackage);
                }
            }
            catch (ArgumentException)
            {
                // ignore missing packages
                // TODO: add an exception type for missing packages to be thrown in the metadata client
            }

            return results;
        }

        /// <summary>
        /// Walk the RegistrationInfo tree to find all package instances and their dependencies.
        /// </summary>
        private static IEnumerable<PackageDependencyInfo> GetPackagesFromRegistration(RegistrationInfo registration, CancellationToken token)
        {
            foreach (var pkgInfo in registration.Packages)
            {
                var dependencies = pkgInfo.Dependencies.Select(e => new PackageDependency(e.Id, e.Range));
                yield return new PackageDependencyInfo(registration.Id, pkgInfo.Version, dependencies);

                foreach (var dep in pkgInfo.Dependencies)
                {
                    foreach (var depPkg in GetPackagesFromRegistration(dep.RegistrationInfo, token))
                    {
                        // check if we have been cancelled
                        token.ThrowIfCancellationRequested();

                        yield return depPkg;
                    }
                }
            }

            yield break;
        }
    }
}
