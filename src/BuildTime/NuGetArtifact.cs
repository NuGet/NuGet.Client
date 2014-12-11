using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public class NuGetArtifact
    {
        private readonly string _artifactType;
        private readonly string _path;

        public NuGetArtifact(string artifactType, string path)
        {
            _artifactType = artifactType;
            _path = path;
        }

        public string ArtifactType
        {
            get
            {
                return _artifactType;
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
            return String.Format(CultureInfo.InvariantCulture, "Type: {0} Path: {1}", ArtifactType, Path);
        }
    }
}
