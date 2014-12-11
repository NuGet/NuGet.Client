using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class NuGetDependency
    {
        private readonly VersionRange _versionRange;
        private readonly string _id;

        public NuGetDependency(string id, VersionRange versionRange)
        {
            _versionRange = versionRange;
            _id = id;
        }

        public VersionRange VersionRange
        {
            get
            {
                return _versionRange;
            }
        }

        public string Id
        {
            get
            {
                return _id;
            }
        }
    }
}
