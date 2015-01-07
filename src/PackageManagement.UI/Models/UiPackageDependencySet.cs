using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace NuGet.PackageManagement.UI
{
    public class UiPackageDependencySet
    {
        public FrameworkName TargetFramework { get; private set; }
        public IReadOnlyCollection<UiPackageDependency> Dependencies { get; private set; }
        public UiPackageDependencySet(FrameworkName targetFramework, IEnumerable<UiPackageDependency> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = dependencies.ToList().AsReadOnly();
        }
    }
}
