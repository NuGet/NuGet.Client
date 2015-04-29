using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.RuntimeModel
{
    public class RuntimePackageDependency
    {
        public string Id { get; }
        public VersionRange VersionRange { get; }

        public RuntimePackageDependency(string id, VersionRange versionRange)
        {
            Id = id;
            VersionRange = versionRange;
        }

        public RuntimePackageDependency Clone()
        {
            return new RuntimePackageDependency(Id, VersionRange);
        }

        public override string ToString()
        {
            return $"{Id} {VersionRange}";
        }
    }
}
