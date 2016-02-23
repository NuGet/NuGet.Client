using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v2
{
    public class PackageSearchMetadata : IPackageSearchMetadata
    {
        private readonly IPackage _package;

        public PackageSearchMetadata(IPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _package = package;
        }

        public string Authors => string.Join(", ", _package.Authors);

        public IEnumerable<PackageDependencyGroup> DependencySets => _package.DependencySets.Select(Convert);

        public string Description => _package.Description;

        public long? DownloadCount => _package.DownloadCount;

        public Uri IconUrl => _package.IconUrl;

        public PackageIdentity Identity => new PackageIdentity(_package.Id, V2Utilities.SafeToNuGetVer(_package.Version));

        public Uri LicenseUrl => _package.LicenseUrl;

        public string Owners => string.Join(", ", _package.Owners);

        public Uri ProjectUrl => _package.ProjectUrl;

        public DateTimeOffset? Published => _package.Published;

        public Uri ReportAbuseUrl => _package.ReportAbuseUrl;

        public bool RequireLicenseAcceptance => _package.RequireLicenseAcceptance;

        public string Summary => !string.IsNullOrEmpty(_package.Summary) ? _package.Summary : Description;

        public string Tags
        {
            get
            {
                var tags = _package.Tags?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[] { };
                return string.Join(" ", tags);
            }
        }

        public string Title => !string.IsNullOrEmpty(_package.Title) ? _package.Title : _package.Id;

        public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => Task.FromResult(Enumerable.Empty<VersionInfo>());

        private static PackageDependencyGroup Convert(PackageDependencySet dependencySet)
        {
            var visualStudioUIPackageDependencies = dependencySet.Dependencies.Select(Convert);
            var fxName = NuGetFramework.AnyFramework;
            if (dependencySet.TargetFramework != null)
            {
                fxName = NuGetFramework.Parse(dependencySet.TargetFramework.FullName);
            }
            return new PackageDependencyGroup(fxName, visualStudioUIPackageDependencies);
        }

        private static Packaging.Core.PackageDependency Convert(PackageDependency dependency)
        {
            var id = dependency.Id;
            var versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new Packaging.Core.PackageDependency(id, versionRange);
        }
    }
}
