using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v2
{
    public class MetadataResourceV2 : MetadataResource
    {
        private readonly IPackageRepository V2Client;
        public MetadataResourceV2(IPackageRepository repo)
        {
            V2Client = repo;
        }

        public MetadataResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override async Task<IEnumerable<KeyValuePair<string, bool>>> ArePackagesSatellite(IEnumerable<string> packageId, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override async Task<IEnumerable<KeyValuePair<string, NuGetVersion>>> GetLatestVersions(IEnumerable<string> packageIds, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            List<KeyValuePair<string, NuGetVersion>> results = new List<KeyValuePair<string, NuGetVersion>>();

            var tasks = new Stack<KeyValuePair<string, Task<IEnumerable<NuGetVersion>>>>();

            // fetch all ids in parallel
            foreach (var id in packageIds)
            {
                var task = new KeyValuePair<string, Task<IEnumerable<NuGetVersion>>>(id, GetVersions(id, includePrerelease, includeUnlisted, token));
                tasks.Push(task);
            }

            foreach (var pair in tasks)
            {
                // wait for the query to finish
                var versions = await pair.Value;

                if (versions == null || !versions.Any())
                {
                    results.Add(new KeyValuePair<string, NuGetVersion>(pair.Key, null));
                }
                else
                {
                    // sort and take only the highest version
                    NuGetVersion latestVersion = versions.OrderByDescending(p => p, VersionComparer.VersionRelease).FirstOrDefault();

                    results.Add(new KeyValuePair<string, NuGetVersion>(pair.Key, latestVersion));
                }
            }

            return results;
        }

        public override async Task<IEnumerable<NuGetVersion>> GetVersions(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            return await Task.Run(() =>
                // year check workaround for p.Listed showing as False for online packages
                V2Client.FindPackagesById(packageId).Where(p => includeUnlisted || !p.Published.HasValue || p.Published.Value.Year > 1901)
                .Select(p => new NuGetVersion(p.Version.Version, p.Version.SpecialVersion))
                .Where(v => includePrerelease || !v.IsPrerelease).ToArray());
        }

        public override async Task<bool> Exists(PackageIdentity identity, bool includeUnlisted, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            bool exists = false;
            SemanticVersion version = SemanticVersion.Parse(identity.Version.ToString());

            if (V2Client is LocalPackageRepository)
            {
                LocalPackageRepository lrepo = V2Client as LocalPackageRepository;
                //Using Path resolver doesnt work. It doesnt consider the subfolders present inside the source directory. Hence using PackageLookupPaths.
                //return new Uri(Path.Combine(V2Client.Source, lrepo.PathResolver.GetPackageFileName(identity.Id, semVer)));
                //Using version.ToString() as version.Version gives the normalized string even if the nupkg has unnormalized version in its path.
                List<string> paths = lrepo.GetPackageLookupPaths(identity.Id, new SemanticVersion(identity.Version.ToString())).ToList();

                exists = paths.Any(path => File.Exists(Path.Combine(V2Client.Source, path)));
            }
            else if (V2Client is UnzippedPackageRepository)
            {
                UnzippedPackageRepository repo = V2Client as UnzippedPackageRepository;

                // only works for exact version string matches
                if (repo.Exists(identity.Id, version))
                {
                    exists = true;
                }
                else
                {
                    // check for non-exact version string matches
                    exists = repo.FindPackagesById(identity.Id).Any(p => p.Version == version);
                }
            }
            else
            {
                // perform a normal exists check
                exists = V2Client.Exists(identity.Id, version);
            }

            return exists;
        }

        public override async Task<bool> Exists(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            return V2Client.Exists(packageId);
        }
    }
}
