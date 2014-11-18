using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Versioning
{
    public partial class SemanticVersion : SimpleVersion
    {
        internal readonly IEnumerable<string> _releaseLabels;
        internal readonly string _metadata;
        internal Version _version;

        public SemanticVersion(SemanticVersion version)
            : this(version.Major, version.Minor, version.Patch, version.ReleaseLabels, version.Metadata)
        {

        }

        public SemanticVersion(Version version, string releaseLabel = null, string metadata = null)
            : this(version, ParseReleaseLabels(releaseLabel), metadata)
        {

        }

        public SemanticVersion(int major, int minor, int patch, string releaseLabel, string metadata)
            :this(major, minor, patch, ParseReleaseLabels(releaseLabel), metadata)
        {

        }

        public SemanticVersion(int major, int minor, int patch, IEnumerable<string> releaseLabels, string metadata)
            :this(new Version(major, minor, patch), releaseLabels, metadata)
        {

        }

        public SemanticVersion(int major, int minor, int patch, int revision, string releaseLabel, string metadata)
            :this(major, minor, patch, revision, ParseReleaseLabels(releaseLabel), metadata)
        {

        }

        public SemanticVersion(int major, int minor, int patch, int revision, IEnumerable<string> releaseLabels, string metadata)
            : this(new Version(major, minor, patch, revision), releaseLabels, metadata)
        {

        }

        public SemanticVersion(Version version, IEnumerable<string> releaseLabels, string metadata)
        {
            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            _version = version;
            _metadata = metadata;

            if (releaseLabels != null)
            {
                // enumerate the list
                _releaseLabels = releaseLabels.ToArray();
            }
        }

        /// <summary>
        /// Major version X (X.y.z)
        /// </summary>
        public int Major { get { return _version.Major; } }

        /// <summary>
        /// Minor version Y (x.Y.z)
        /// </summary>
        public int Minor { get { return _version.Minor; } }

        /// <summary>
        /// Patch version Z (x.y.Z)
        /// </summary>
        public int Patch { get { return _version.Build; } }

        /// <summary>
        /// A collection of pre-release labels attached to the version.
        /// </summary>
        public IEnumerable<string> ReleaseLabels
        {
            get
            {
                return _releaseLabels ?? Enumerable.Empty<string>();
            }
        }

        /// <summary>
        /// The full pre-release label for the version.
        /// </summary>
        public string Release
        {
            get
            {
                if (_releaseLabels != null)
                {
                    return String.Join(".", _releaseLabels);
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// True if pre-release labels exist for the version.
        /// </summary>
        public virtual bool IsPrerelease
        {
            get
            {
                if (ReleaseLabels != null)
                {
                    var enumerator = ReleaseLabels.GetEnumerator();
                    return (enumerator.MoveNext() && !String.IsNullOrEmpty(enumerator.Current));
                }

                return false;
            }
        }

        /// <summary>
        /// True if metadata exists for the version.
        /// </summary>
        public virtual bool HasMetadata
        {
            get
            {
                return !String.IsNullOrEmpty(Metadata);
            }
        }

        /// <summary>
        /// Build metadata attached to the version.
        /// </summary>
        public virtual string Metadata
        {
            get
            {
                return _metadata;
            }
        }

        public override int GetHashCode()
        {
            return ToString("V-R", new VersionFormatter()).ToUpperInvariant().GetHashCode();
        }
    }
}
