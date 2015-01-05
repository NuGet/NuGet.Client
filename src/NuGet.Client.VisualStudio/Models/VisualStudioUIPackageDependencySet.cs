using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public class VisualStudioUIPackageDependencySet
    {
        public FrameworkName TargetFramework { get; private set; }
        public IReadOnlyCollection<VisualStudioUIPackageDependency> Dependencies { get; private set; }
        public VisualStudioUIPackageDependencySet(FrameworkName targetFramework, IEnumerable<VisualStudioUIPackageDependency> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = dependencies.ToList().AsReadOnly();
        }
    }
}
