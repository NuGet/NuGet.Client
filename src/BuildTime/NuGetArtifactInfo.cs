using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class NuGetArtifactInfo
    {
        private readonly NuGetPackageId _package;
        private readonly IEnumerable<NuGetArtifactGroup> _groups;

        public NuGetArtifactInfo(NuGetPackageId packageId, IEnumerable<NuGetArtifactGroup> groups)
        {
            _package = packageId;
            _groups = groups;
        }

        public NuGetPackageId Package
        {
            get
            {
                return _package;
            }
        }

        public IEnumerable<NuGetArtifactGroup> Groups
        {
            get
            {
                return _groups;
            }
        }
    }
}
