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

        public override Task<IEnumerable<UIPackageMetadata>> GetMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            return Task.Factory.StartNew(() =>
            {
                return V2Client.FindPackagesById(packageId).Select(p => GetVisualStudioUIPackageMetadata(p));
            });
        }

        public override Task<IEnumerable<UIPackageMetadata>> GetMetadata(IEnumerable<PackagingCore.PackageIdentity> packages, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {

            return Task.Factory.StartNew(() =>
            {
                List<UIPackageMetadata> packageMetadataList = new List<Client.VisualStudio.UIPackageMetadata>();
                foreach (var identity in packages)
                {
                    var semver = new SemanticVersion(identity.Version.ToNormalizedString());
                    var package = V2Client.FindPackage(identity.Id, semver);

                    // Sometimes, V2 APIs seem to fail to return a value for Packages(Id=,Version=) requests...

                    if (package == null)
                    {
                        var packagesList = V2Client.FindPackagesById(identity.Id);
                        package = packagesList.FirstOrDefault(p => Equals(p.Version, semver));
                    }

                    // If still null, fail
                    if (package == null)
                    {
                        return null;
                    }

                    packageMetadataList.Add(GetVisualStudioUIPackageMetadata(package));
                }
                return packageMetadataList.AsEnumerable();
            });
        }

        private static UIPackageMetadata GetVisualStudioUIPackageMetadata(IPackage package)
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
            int DownloadCount = package.DownloadCount;
            IEnumerable<UIPackageDependencySet> DependencySets = package.DependencySets.Select(p => GetVisualStudioUIPackageDependencySet(p));
            bool requiresLiceneseAcceptance = package.RequireLicenseAcceptance;

            bool HasDependencies = DependencySets.Any(
                set => set.Dependencies != null && set.Dependencies.Count > 0);
            PackageIdentity identity = new PackageIdentity(package.Id, Version);

            return new UIPackageMetadata(identity, Summary, Description, Authors, Owners, IconUrl, LicenseUrl, ProjectUrl, Tags, DownloadCount, Published, DependencySets, HasDependencies, requiresLiceneseAcceptance);
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
