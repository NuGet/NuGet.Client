using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public class SourcePackageDependencyInfo : PackageDependencyInfo
    {
        public SourcePackageDependencyInfo(string id, NuGetVersion version, bool listed, SourceRepository source)
            : base(id, version)
        {
            Listed = listed;
            Source = source;
        }

        public SourcePackageDependencyInfo(PackageIdentity identity, IEnumerable<PackageDependency> dependencies, bool listed, SourceRepository source)
            : base(identity, dependencies)
        {
            Listed = listed;
            Source = source;
        }

        public SourcePackageDependencyInfo(string id, NuGetVersion version, IEnumerable<PackageDependency> dependencies, bool listed, SourceRepository source)
            : base(id, version, dependencies)
        {
            Listed = listed;
            Source = source;
        }

        public bool Listed { get; }

        public SourceRepository Source { get; }
    }
}
