using System;
using System.IO;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    public class LocalPackageInfo
    {
        public LocalPackageInfo(string packageId, NuGetVersion version, string path)
        {
            Id = packageId;
            Version = version;
            ExpandedPath = path;
            ManifestPath = Path.Combine(path, string.Format("{0}.nuspec", Id));
            ZipPath = Path.Combine(path, string.Format("{0}.{1}.nupkg", Id, Version));
        }

        public string Id { get; }

        public NuGetVersion Version { get; }

        public string ExpandedPath { get; set; }

        public string ManifestPath { get; }

        public string ZipPath { get; }

        public override string ToString()
        {
            return Id + " " + Version + " (" + (ManifestPath ?? ZipPath) + ")";
        }
    }
}