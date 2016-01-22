using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;

namespace NuGet.Protocol.Core.Types
{
    public interface IPackageSearchMetadata
    {
        PackageIdentity Identity { get; }
        string Description { get; }
        string Summary { get; }
        Uri IconUrl { get; }
        IEnumerable<VersionInfo> Versions { get; }
        PackageMetadata LatestPackageMetadata { get; }
        string Title { get; }
        long? DownloadCount { get; }
        string[] Tags { get; }
    }
}
