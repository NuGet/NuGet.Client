using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using NuGet.Packaging;

namespace NuGet.PackageManagement.UI
{
    internal class PackageDependencySetMetadata
    {
        public PackageDependencySetMetadata(PackageDependencyGroup dependencyGroup)            
        {
            TargetFramework = dependencyGroup.TargetFramework;
            Dependencies = dependencyGroup.Packages
                .Select(d => new PackageDependencyMetadata(d))
                .ToList()
                .AsReadOnly();
        }

        public NuGetFramework TargetFramework { get; private set; }
        public IReadOnlyCollection<PackageDependencyMetadata> Dependencies { get; private set; }

    }
}
