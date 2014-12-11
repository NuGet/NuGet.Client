using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class NuGetArtifactGroup
    {
        private readonly KeyValuePair<string, string>[] _properties;
        private readonly NuGetArtifact[] _artifacts;

        public NuGetArtifactGroup(IEnumerable<KeyValuePair<string, string>> properties, IEnumerable<NuGetArtifact> artifacts)
        {
            _properties = properties.ToArray();
            _artifacts = artifacts.ToArray();
        }

        public IEnumerable<KeyValuePair<string, string>> Properties
        {
            get
            {
                return _properties;
            }
        }

        public IEnumerable<NuGetArtifact> Artifacts
        {
            get
            {
                return _artifacts;
            }
        }
    }
}
