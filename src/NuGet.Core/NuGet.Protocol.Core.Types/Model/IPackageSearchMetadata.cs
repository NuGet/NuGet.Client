using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Package metadata only containing select fields relevant to search results processing and presenting.
    /// Immutable.
    /// </summary>
    public interface IPackageSearchMetadata
    {
        string Authors { get; }
        IEnumerable<PackageDependencyGroup> DependencySets { get; }
        string Description { get; }
        long? DownloadCount { get; }
        Uri IconUrl { get; }
        PackageIdentity Identity { get; }
        Uri LicenseUrl { get; }
        Uri ProjectUrl { get; }
        Uri ReportAbuseUrl { get; }
        DateTimeOffset? Published { get; }
        string Owners { get; }
        bool RequireLicenseAcceptance { get; }
        string Summary { get; }
        string Tags { get; }
        string Title { get; }

        Task<IEnumerable<VersionInfo>> GetVersionsAsync();
    }
}
