using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public class PackageDependency
    {
        private readonly string _id;
        private readonly string _versionRange;

        public PackageDependency(string id, string versionRange)
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

        public string VersionRange
        {
            get
            {
                return _versionRange;
            }
        }
    }
}
