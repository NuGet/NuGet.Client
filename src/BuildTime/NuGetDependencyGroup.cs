using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class NuGetDependencyGroup
    {
        private readonly FrameworkName _targetFramework;
        private readonly IEnumerable<NuGetDependency> _dependencies;

        public NuGetDependencyGroup(FrameworkName targetFramework, IEnumerable<NuGetDependency> dependencies)
        {
            _targetFramework = targetFramework;
            _dependencies = dependencies;
        }

        public FrameworkName TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        public IEnumerable<NuGetDependency> Dependencies
        {
            get
            {
                return _dependencies;
            }
        }
    }
}
