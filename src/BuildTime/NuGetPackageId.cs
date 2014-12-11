using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class NuGetPackageId
    {
        private readonly string _id;
        private readonly NuGetVersion _version;
        private readonly string _path;

        public NuGetPackageId(string id, NuGetVersion version, string path)
        {
            _id = id;
            _version = version;
            _path = path;
        }

        public string Id
        {
            get
            {
                return _id;
            }
        }

        public NuGetVersion Version
        {
            get
            {
                return _version;
            }
        }

        public string Path
        {
            get
            {
                return _path;
            }
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
