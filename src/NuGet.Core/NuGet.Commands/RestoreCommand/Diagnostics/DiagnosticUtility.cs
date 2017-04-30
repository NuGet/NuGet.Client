// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.LibraryModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    /// <summary>
    /// Warning and error logging helpers.
    /// </summary>
    public static class DiagnosticUtility
    {
        /// <summary>
        /// Format an id and include the version only if it exists.
        /// Ignore versions for projects.
        /// </summary>
        public static string FormatIdentity(LibraryIdentity identity)
        {
            // Display the version if it exists
            // Ignore versions for projects
            if (identity.Version != null && identity.Type == LibraryType.Package)
            {
                return $"{identity.Name} {identity.Version.ToNormalizedString()}";
            }

            return identity.Name;
        }

        /// <summary>
        /// Format an id and include the range only if it has bounds.
        /// </summary>
        public static string FormatDependency(string id, VersionRange range)
        {
            if (range == null || !(range.HasLowerBound || range.HasUpperBound))
            {
                return id;
            }

            return $"{id} {range.ToNonSnapshotRange().PrettyPrint()}";
        }

        /// <summary>
        /// Format an id and include the lower bound only if it has one.
        /// </summary>
        public static string FormatExpectedIdentity(string id, VersionRange range)
        {
            if (range == null || !range.HasLowerBound || !range.IsMinInclusive)
            {
                return id;
            }

            return $"{id} {range.MinVersion.ToNormalizedString()}";
        }
    }
}
