// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents a version that will be displayed on the UI.
    /// </summary>
    public class DisplayVersion
    {
        private readonly string _additionalInfo;

        private readonly string _toString;

        public DisplayVersion(
            NuGetVersion version,
            string additionalInfo,
            bool isValidVersion = true,
            bool isCurrentInstalled = false,
            bool autoReferenced = false)
            : this(GetRange(version), additionalInfo, isValidVersion, isCurrentInstalled, autoReferenced)
        {
        }

        public DisplayVersion(
            VersionRange range,
            string additionalInfo,
            bool isValidVersion = true,
            bool isCurrentInstalled = false,
            bool autoReferenced = false)
        {
            Range = range;
            _additionalInfo = additionalInfo;

            IsValidVersion = isValidVersion;

            Version = range.MinVersion;
            IsCurrentInstalled = isCurrentInstalled;
            AutoReferenced = autoReferenced;

            // Display a single version if the range is locked
            if (range.HasLowerAndUpperBounds && range.MinVersion == range.MaxVersion)
            {
                _toString = string.IsNullOrEmpty(_additionalInfo) ?
                    Version.ToNormalizedString() :
                    _additionalInfo + " " + Version.ToNormalizedString();
            }
            else
            {
                // Display the range, use the original value for floating ranges
                _toString = string.IsNullOrEmpty(_additionalInfo) ?
                    Range.OriginalString :
                    _additionalInfo + " " + Range.OriginalString;
            }
        }

        public bool IsCurrentInstalled { get; set; }

        public NuGetVersion Version { get; }

        public VersionRange Range { get; }

        public bool IsValidVersion { get; set; }

        public bool AutoReferenced { get; set; }

        public override string ToString()
        {
            return _toString;
        }

        public override bool Equals(object obj)
        {
            var other = obj as DisplayVersion;
            return other != null && other.Version == Version;
        }

        public override int GetHashCode()
        {
            return Version.GetHashCode();
        }

        private static VersionRange GetRange(NuGetVersion version)
        {
            return new VersionRange(minVersion: version, includeMinVersion: true, maxVersion: version, includeMaxVersion: true);
        }
    }
}