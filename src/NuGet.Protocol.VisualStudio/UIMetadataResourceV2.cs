using NuGet;
using NuGet.Protocol;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.v2;

namespace NuGet.Protocol.VisualStudio
{

    public class UIMetadataResourceV2 : UIMetadataResource
    {
        private readonly IPackageRepository V2Client;
        public UIMetadataResourceV2(V2Resource resource)
        {
            V2Client = resource.V2Client;
        }

        public override async Task<IEnumerable<UIPackageMetadata>> GetMetadata(IEnumerable<PackageIdentity> packages, CancellationToken token)
        {
            List<UIPackageMetadata> results = new List<UIPackageMetadata>();

            foreach (var group in packages.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() == 1)
                {
                    // optimization for a single package
                    var package = group.Single();

                    IPackage result = V2Client.FindPackage(package.Id, SemanticVersion.Parse(package.Version.ToString()));

                    if (result != null)
                    {
                        results.Add(GetVisualStudioUIPackageMetadata(result));
                    }
                }
                else
                {
                    // batch mode
                    var foundPackages = V2Client.FindPackagesById(group.Key)
                        .Where(p => group.Any(e => VersionComparer.VersionRelease.Equals(e.Version, NuGetVersion.Parse(p.Version.ToString()))))
                        .Select(p => GetVisualStudioUIPackageMetadata(p));

                    results.AddRange(foundPackages);
                }
            }

            return results;
        }

        public override async Task<IEnumerable<UIPackageMetadata>> GetMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            return await Task.Run(() =>
                {
                    return V2Client.FindPackagesById(packageId)
                        .Where(p => includeUnlisted || !p.Published.HasValue || p.Published.Value.Year > 1901)
                        .Where(p => includePrerelease || String.IsNullOrEmpty(p.Version.SpecialVersion))
                        .Select(p => GetVisualStudioUIPackageMetadata(p));
                });
        }

        internal static UIPackageMetadata GetVisualStudioUIPackageMetadata(IPackage package)
        {
            var parsed = ParsePackageMetadataV2.Parse(package);

            // TODO: fetch this
            Uri reportAbuse = null;
            string tags = String.Join(" ", parsed.Tags);
            string authors = String.Join(" ", parsed.Authors);
            string owners = String.Join(" ", parsed.Owners);

            return new UIPackageMetadata(new PackageIdentity(parsed.Id, parsed.Version), parsed.Title, parsed.Summary, parsed.Description, authors, owners, parsed.IconUrl, parsed.LicenseUrl, parsed.ProjectUrl, reportAbuse, tags, parsed.Published, parsed.DependencySets, parsed.RequireLicenseAcceptance);
        }
    }
}
