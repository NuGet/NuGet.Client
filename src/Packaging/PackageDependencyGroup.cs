using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public class PackageDependencyGroup
    {
        private readonly string _targetFramework;
        private readonly IEnumerable<PackageDependency> _packages;

        public PackageDependencyGroup(string targetFramework, IEnumerable<PackageDependency> packages)
        {
            if (String.IsNullOrEmpty(targetFramework))
            {
                _targetFramework = PackagingConstants.AnyFramework;
            }
            else
            {
                _targetFramework = targetFramework;
            }

            _packages = packages;
        }

        public string TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        public IEnumerable<PackageDependency> Packages
        {
            get
            {
                return _packages;
            }
        }
    }
}
