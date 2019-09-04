// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents a version that will be displayed on the UI.
    /// </summary>
    public class DisplayVersion
    {
        public readonly string AdditionalInfo;

        private readonly string _toString;

        public DisplayVersion(
            NuGetVersion version,
            string additionalInfo,
            bool isValidVersion = true,
            bool isCurrentInstalled = false,
            bool autoReferenced = false,
            bool isDeprecated = false,
            string versionFormat = "N")
            : this(GetRange(version), additionalInfo, isValidVersion, isCurrentInstalled, autoReferenced, isDeprecated, versionFormat)
        {
        }

        public DisplayVersion(
            VersionRange range,
            string additionalInfo,
            bool isValidVersion = true,
            bool isCurrentInstalled = false,
            bool autoReferenced = false,
            bool isDeprecated = false,
            string versionFormat = "N")
        {
            if (versionFormat == null)
            {
                // default to normalized version
                versionFormat = "N";
            }

            Range = range;
            AdditionalInfo = additionalInfo;

            IsValidVersion = isValidVersion;

            Version = range.MinVersion;
            IsCurrentInstalled = isCurrentInstalled;
            AutoReferenced = autoReferenced;

            // Display a single version if the range is locked
            // If range is unlocked, display it and use the original value for floating ranges
            var versionString = range.HasLowerAndUpperBounds && range.MinVersion == range.MaxVersion ?
                Version.ToString(versionFormat, VersionFormatter.Instance)
                : Range.OriginalString;

            _toString = "";
            if (!string.IsNullOrEmpty(AdditionalInfo))
            {
                _toString += AdditionalInfo + " ";
            }

            _toString += versionString;

            if (isDeprecated)
            {
                _toString += string.Format(
                    CultureInfo.CurrentCulture,
                    "    ({0})",
                    Resources.Label_Deprecated);
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
            return other != null && other.Version == Version && string.Equals(other.AdditionalInfo, AdditionalInfo, StringComparison.Ordinal);
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
