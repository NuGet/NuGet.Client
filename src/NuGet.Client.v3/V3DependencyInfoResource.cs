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

        // TODO: use the discovery client instead of hardcoding this
        private const string RegistrationTemplate = "https://az320820.vo.msecnd.net/registrations-1/{0}/index.json";

        public V3DependencyInfoResource(HttpClient client)
        {
            _client = client;
            _cache = new ConcurrentDictionary<Uri, JObject>();
        }

        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<PackageIdentity> packages, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            // compare results based on the id/version
            HashSet<PackageDependencyInfo> results = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);

            foreach (var package in packages)
            {
                Uri uri = new Uri(String.Format(CultureInfo.InvariantCulture, RegistrationTemplate, package.Id.ToLowerInvariant()));

                var regInfo = await ResolverMetadataClient.GetRegistrationInfo(_client, uri, new VersionRange(package.Version, true, package.Version, true), projectFramework, _cache);

                // TODO: add filtering support
                var result = await ResolverMetadataClient.GetTree(_client, regInfo, projectFramework, _cache);

                foreach (var curPkg in GetPackagesFromRegistration(result))
                {
                    results.Add(curPkg);
                }
            }

            return results;
        }

        /// <summary>
        /// Walk the RegistrationInfo tree to find all package instances and their dependencies.
        /// </summary>
        private static IEnumerable<PackageDependencyInfo> GetPackagesFromRegistration(RegistrationInfo registration)
        {
            foreach (var pkgInfo in registration.Packages)
            {
                var dependencies = pkgInfo.Dependencies.Select(e => new PackageDependency(e.Id, e.Range));
                yield return new PackageDependencyInfo(registration.Id, pkgInfo.Version, dependencies);

                foreach (var dep in pkgInfo.Dependencies)
                {
                    foreach (var depPkg in GetPackagesFromRegistration(dep.RegistrationInfo))
                    {
                        yield return depPkg;
                    }
                }
            }

            yield break;
        }

        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(string packageId, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
