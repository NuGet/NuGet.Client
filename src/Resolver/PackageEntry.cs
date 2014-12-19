using NuGet.Versioning;
using System;

namespace NuGet.Resolver
{
    public class PackageEntry
    {
        public string Id { get; set; }
        public NuGetVersion Version { get; set; }
        public VersionRange Allowed { get; set; }
        public Uri PackageContent { get; set; }

        public PackageEntry(string id, NuGetVersion version = null, VersionRange allowed = null, Uri packageContent = null)
        {
            Id = id;
            Version = version;
            Allowed = allowed;
            PackageContent = packageContent;
        }
    }
}
