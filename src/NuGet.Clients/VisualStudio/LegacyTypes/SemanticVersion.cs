using System;

namespace NuGet
{
    [Serializable]
    public sealed class SemanticVersion : IComparable, IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        private readonly string _versionString;

        public SemanticVersion(string version)
        {
            _versionString = version;
        }

        /// <summary>
        /// Gets the normalized version portion.
        /// </summary>
        public Version Version
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the optional special version.
        /// </summary>
        public string SpecialVersion
        {
            get;
            private set;
        }

        public string[] GetOriginalVersionComponents()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed by an optional special version.
        /// </summary>
        public static SemanticVersion Parse(string version)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed by an optional special version.
        /// </summary>
        public static bool TryParse(string version, out SemanticVersion value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Parses a version string using strict semantic versioning rules that allows exactly 3 components and an optional special version.
        /// </summary>
        public static bool TryParseStrict(string version, out SemanticVersion value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Attempts to parse the version token as a SemanticVersion.
        /// </summary>
        /// <returns>An instance of SemanticVersion if it parses correctly, null otherwise.</returns>
        public static SemanticVersion ParseOptionalVersion(string version)
        {
            throw new NotSupportedException();
        }

        public int CompareTo(object obj)
        {
            throw new NotSupportedException();
        }

        public int CompareTo(SemanticVersion other)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Returns the normalized string representation of this instance of <see cref="SemanticVersion"/>.
        /// If the instance can be strictly parsed as a <see cref="SemanticVersion"/>, the normalized version
        /// string if of the format {major}.{minor}.{build}[-{special-version}]. If the instance has a non-zero
        /// value for <see cref="Version.Revision"/>, the format is {major}.{minor}.{build}.{revision}[-{special-version}].
        /// </summary>
        /// <returns>The normalized string representation.</returns>
        public string ToNormalizedString()
        {
            return _versionString;
        }

        public override string ToString()
        {
            return _versionString;
        }

        public bool Equals(SemanticVersion other)
        {
            throw new NotSupportedException();
        }
    }
}
