using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    public class PackageDependency
    {
        private readonly string _id;
        private readonly VersionRange _versionRange;

        public PackageDependency(string id, VersionRange versionRange)
        {
            _id = id;
            _versionRange = versionRange;
        }

        public string Id
        {
            get
            {
                return _id;
            }
        }

        public VersionRange VersionRange
        {
            get
            {
                return _versionRange;
            }
        }
    }
}
