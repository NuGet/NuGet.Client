using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class NuGetDependencyInfo
    {
        private readonly NuGetPackageId _package;
        private readonly List<NuGetDependencyGroup> _dependencyGroups;

        public NuGetDependencyInfo(NuGetPackageId package, List<NuGetDependencyGroup> dependencyGroups)
        {
            _package = package;
            _dependencyGroups = dependencyGroups;
        }

        public NuGetPackageId Package
        {
            get
            {
                return _package;
            }
        }

        public IEnumerable<NuGetDependencyGroup> DependencyGroups
        {
            get
            {
                return _dependencyGroups;
            }
        }
    }
}
