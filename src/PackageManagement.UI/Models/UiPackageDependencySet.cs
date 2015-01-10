using NuGet.Client.VisualStudio;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace NuGet.PackageManagement.UI
{
    public class UiPackageDependencySet
    {
        public UiPackageDependencySet(UIPackageDependencySet serverData)
            : this(serverData.TargetFramework, serverData.Dependencies.Select(e => new UiPackageDependency(e)))
        {

        }

        public UiPackageDependencySet(NuGetFramework targetFramework, IEnumerable<UiPackageDependency> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = dependencies.ToList().AsReadOnly();
        }

        public NuGetFramework TargetFramework { get; private set; }
        public IReadOnlyCollection<UiPackageDependency> Dependencies { get; private set; }

    }
}
