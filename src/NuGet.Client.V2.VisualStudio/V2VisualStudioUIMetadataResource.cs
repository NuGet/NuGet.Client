using NuGet;
using NuGet.Client;
using NuGet.Client.V2;
using NuGet.Client.VisualStudio;
using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V2.VisualStudio
{

    public class V2UIMetadataResource :  UIMetadataResource
    {
        private readonly IPackageRepository V2Client;
        public V2UIMetadataResource(V2Resource resource)
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
            NuGetVersion Version = NuGetVersion.Parse(package.Version.ToString());
            DateTimeOffset? Published = package.Published;
            string Summary = package.Summary;
            string Description = package.Description;
            //*TODOs: Check if " " is the separator in the case of V3 jobjects ...
            string Authors = string.Join(" ",package.Authors.ToArray());
            string Owners = string.Join(" ",package.Owners.ToArray());
            Uri IconUrl = package.IconUrl;
            Uri LicenseUrl = package.LicenseUrl;
            Uri ProjectUrl = package.ProjectUrl;
            string Tags = package.Tags;
            IEnumerable<UIPackageDependencySet> DependencySets = package.DependencySets.Select(p => GetVisualStudioUIPackageDependencySet(p));
            bool requiresLiceneseAcceptance = package.RequireLicenseAcceptance;

            PackageIdentity identity = new PackageIdentity(package.Id, Version);

            return new UIPackageMetadata(
                identity, Summary, Description, Authors, Owners, IconUrl, LicenseUrl, 
                ProjectUrl, package.ReportAbuseUrl,
                Tags, Published, DependencySets, requiresLiceneseAcceptance);
        }

        private static NuGet.PackagingCore.PackageDependency GetVisualStudioUIPackageDependency(PackageDependency dependency)
        {
            string id = dependency.Id;
            VersionRange versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new NuGet.PackagingCore.PackageDependency(id, versionRange);
        }

        private static UIPackageDependencySet GetVisualStudioUIPackageDependencySet(PackageDependencySet dependencySet)
        {
            IEnumerable<NuGet.PackagingCore.PackageDependency> visualStudioUIPackageDependencies = dependencySet.Dependencies.Select(d => GetVisualStudioUIPackageDependency(d));
            NuGetFramework fxName = NuGetFramework.AnyFramework;
            if(dependencySet.TargetFramework != null)
             fxName = NuGetFramework.Parse(dependencySet.TargetFramework.FullName);            
            return new UIPackageDependencySet(fxName, visualStudioUIPackageDependencies);
        }
    }
}
