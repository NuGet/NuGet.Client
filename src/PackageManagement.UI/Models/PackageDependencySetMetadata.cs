using NuGet.Client.VisualStudio;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace NuGet.PackageManagement.UI
{
    internal class PackageDependencySetMetadata
    {
        public PackageDependencySetMetadata(UIPackageDependencySet serverData)
            : this(serverData.TargetFramework, serverData.Dependencies.Select(e => new PackageDependencyMetadata(e)))
        {

        }

        public PackageDependencySetMetadata(NuGetFramework targetFramework, IEnumerable<PackageDependencyMetadata> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = dependencies.ToList().AsReadOnly();
        }

        public NuGetFramework TargetFramework { get; private set; }
        public IReadOnlyCollection<PackageDependencyMetadata> Dependencies { get; private set; }

    }
}
