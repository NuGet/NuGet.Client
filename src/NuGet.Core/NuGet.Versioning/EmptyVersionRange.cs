// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Versioning
{
    /// <summary>
    /// Represents a <see cref="VersionRange"/> that has empty normalized version.
    /// </summary>
    public class EmptyVersionRange : VersionRange
    {
        /// <summary>
        /// 
        /// </summary>
        public EmptyVersionRange()
            : base(minVersion: null,
            includeMinVersion: false,
            maxVersion: null,
            includeMaxVersion: false,
            floatRange: null,
            originalString: null)
        {
        }

        /// <summary>
        /// Normalized range string.
        /// </summary>
        public override string ToString()
        {
            return ToNormalizedString();
        }

        /// <summary>
        /// Normalized range string.
        /// </summary>
        public override string ToNormalizedString()
        {
            return string.Empty;
        }

        /// <summary>
        /// A legacy version range.
        /// </summary>
        public override string ToLegacyString()
        {
            return string.Empty;
        }

        /// <summary>
        /// A short legacy version range.
        /// </summary>
        public override string ToLegacyShortString()
        {
            return string.Empty;
        }
    }
}
