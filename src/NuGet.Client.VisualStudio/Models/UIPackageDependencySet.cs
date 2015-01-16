using NuGet.Frameworks;
using NuGet.PackagingCore;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Client.VisualStudio
{
    public class UIPackageDependencySet
    {
        public NuGetFramework TargetFramework { get; private set; }
        public IReadOnlyCollection<PackageDependency> Dependencies { get; private set; }
        public UIPackageDependencySet(NuGetFramework targetFramework, IEnumerable<PackageDependency> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = dependencies.ToList().AsReadOnly();
        }
    }
}
