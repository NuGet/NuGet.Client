using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Versioning
{
    /// <summary>
    /// A hybrid implementation of SemVer that supports semantic versioning as described at http://semver.org while not strictly enforcing it to 
    /// allow older 4-digit versioning schemes to continue working.
    /// </summary>
    public partial class NuGetVersion : SemanticVersion
    {
        private readonly string _originalString;

        public NuGetVersion(string version)
            : this(Parse(version))
        {

        }

        public NuGetVersion(NuGetVersion version)
            : this(version.Version, version.ReleaseLabels, version.Metadata, version.ToString())
        {

        }

        public NuGetVersion(Version version, string releaseLabel = null, string metadata = null)
            : this(version, ParseReleaseLabels(releaseLabel), metadata, GetLegacyString(version, ParseReleaseLabels(releaseLabel), metadata))
        {

        }

        /// <summary>
        /// Creates a NuGetVersion X.Y.Z
        /// </summary>
        /// <param name="major">X.y.z</param>
        /// <param name="minor">x.Y.z</param>
        /// <param name="patch">x.y.Z</param>
        public NuGetVersion(int major, int minor, int patch)
            : this(major, minor, patch, Enumerable.Empty<string>(), null)
        {

        }

        /// <summary>
        /// Creates a NuGetVersion X.Y.Z-alpha
        /// </summary>
        /// <param name="major">X.y.z</param>
        /// <param name="minor">x.Y.z</param>
        /// <param name="patch">x.y.Z</param>
        /// <param name="releaseLabel">Prerelease label</param>
        public NuGetVersion(int major, int minor, int patch, string releaseLabel)
            : this(major, minor, patch, ParseReleaseLabels(releaseLabel), null)
        {

        }

        /// <summary>
        /// Creates a NuGetVersion X.Y.Z-alpha#build01
        /// </summary>
        /// <param name="major">X.y.z</param>
        /// <param name="minor">x.Y.z</param>
        /// <param name="patch">x.y.Z</param>
        /// <param name="releaseLabel">Prerelease label</param>
        /// <param name="metadata">Build metadata</param>
        public NuGetVersion(int major, int minor, int patch, string releaseLabel, string metadata)
            : this(major, minor, patch, ParseReleaseLabels(releaseLabel), metadata)
        {

        }

        public NuGetVersion(int major, int minor, int patch, IEnumerable<string> releaseLabels, string metadata)
            : this(new Version(major, minor, patch), releaseLabels, metadata, null)
        {

        }

        public NuGetVersion(int major, int minor, int patch, int revision)
            : this(major, minor, patch, revision, Enumerable.Empty<string>(), null)
        {

        }

        public NuGetVersion(int major, int minor, int patch, int revision, string releaseLabel, string metadata)
            : this(major, minor, patch, revision, ParseReleaseLabels(releaseLabel), metadata)
        {

        }

        public NuGetVersion(int major, int minor, int patch, int revision, IEnumerable<string> releaseLabels, string metadata)
            : this(new Version(major, minor, patch, revision), releaseLabels, metadata, null)
        {

        }

        public NuGetVersion(Version version, IEnumerable<string> releaseLabels, string metadata, string originalVersion)
            : base(version, releaseLabels, metadata)
        {
            _originalString = originalVersion;
        }

        /// <summary>
        /// Returns the version string.
        /// </summary>
        /// <remarks>This method includes legacy behavior. Use ToNormalizedString() instead.</remarks>
        public override string ToString()
        {
            if (String.IsNullOrEmpty(_originalString))
            {
                return ToNormalizedString();
            }

            return _originalString;
        }

        /// <summary>
        /// A System.Version representation of the version without metadata or release labels.
        /// </summary>
        public Version Version
        {
            get { return _version; }
        }

        /// <summary>
        /// True if the NuGetVersion is using legacy behavior.
        /// </summary>
        public virtual bool IsLegacyVersion
        {
            get { return Version.Revision > 0; }
        }
    }
}
