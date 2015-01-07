using System;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class VersionForDisplay
    {
        private string _additionalInfo;

        private string _toString;

        public VersionForDisplay(
            NuGetVersion version,
            string additionalInfo)
        {
            Version = version;
            _additionalInfo = additionalInfo;

            _toString = string.IsNullOrEmpty(_additionalInfo) ?
                Version.ToNormalizedString() :
                _additionalInfo + " " + Version.ToNormalizedString();
        }

        public NuGetVersion Version
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return _toString;
        }

        public override bool Equals(object obj)
        {
            var other = obj as VersionForDisplay;
            return other != null && other.Version == Version;
        }

        public override int GetHashCode()
        {
            return Version.GetHashCode();
        }
    }
}
