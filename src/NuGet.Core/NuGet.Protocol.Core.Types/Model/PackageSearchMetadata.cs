using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;

namespace NuGet.Protocol.Core.Types
{
    public class PackageSearchMetadata : IPackageSearchMetadata
    {
        public PackageIdentity Identity { get; set; }

        public string Description { get; set; }

        public string Summary { get; set; }

        public Uri IconUrl { get; set; }

        public IEnumerable<VersionInfo> Versions { get; set; }

        public PackageMetadata LatestPackageMetadata { get; set; }

        public string Title { get; set; }

        public long? DownloadCount { get; set; }

        public string[] Tags { get; set; }
    }
}
