// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;

namespace NuGet.Packaging
{
    public static class ManifestVersionUtility
    {
        public const int DefaultVersion = 1;
        public const int SemverVersion = 3;

        public const int TargetFrameworkSupportForDependencyContentsAndToolsVersion = 4;
        public const int TargetFrameworkSupportForReferencesVersion = 5;
        public const int XdtTransformationVersion = 6;

        // IMPORTANT:
        // Whenever adding a new ManifestVersion constant, make sure to update
        // ManifestVersionUtility.GetVersionFromObject(...) accordingly.
        // The version calculation logic is explicit (no reflection),
        // so new versioned properties must be handled there as well.
        internal static class ManifestVersions
        {
            public const int ReleaseNotes = 2;
            public const int Copyright = 2;
            public const int PackageAssemblyReferences = 2;
            public const int MinClientVersionString = 5;
        }

        public static int GetManifestVersion(ManifestMetadata metadata)
        {
            return Math.Max(GetVersionFromObject(metadata), GetMaxVersionFromMetadata(metadata));
        }

        private static int GetMaxVersionFromMetadata(ManifestMetadata metadata)
        {
            // Important: always add newer version checks at the top

            bool referencesHasTargetFramework =
              metadata.PackageAssemblyReferences != null &&
              metadata.PackageAssemblyReferences.Any(r => r.TargetFramework != null && r.TargetFramework.IsSpecificFramework);

            if (referencesHasTargetFramework)
            {
                return TargetFrameworkSupportForReferencesVersion;
            }

            bool dependencyHasTargetFramework =
                metadata.DependencyGroups != null &&
                metadata.DependencyGroups.Any(d => d.TargetFramework != null && d.TargetFramework.IsSpecificFramework);
            if (dependencyHasTargetFramework)
            {
                return TargetFrameworkSupportForDependencyContentsAndToolsVersion;
            }

            if (metadata.Version != null && metadata.Version.IsPrerelease)
            {
                return SemverVersion;
            }

            return DefaultVersion;
        }

        private static int GetVersionFromObject(ManifestMetadata metadata)
        {
            if (metadata == null)
            {
                return DefaultVersion;
            }

            int version = DefaultVersion;

            if (!string.IsNullOrEmpty(metadata.MinClientVersionString))
            {
                version = Math.Max(version, ManifestVersions.MinClientVersionString);
            }

            if (!string.IsNullOrEmpty(metadata.ReleaseNotes))
            {
                version = Math.Max(version, ManifestVersions.ReleaseNotes);
            }

            if (!string.IsNullOrEmpty(metadata.Copyright))
            {
                version = Math.Max(version, ManifestVersions.Copyright);
            }

            if (metadata.PackageAssemblyReferences != null && metadata.PackageAssemblyReferences.Any())
            {
                version = Math.Max(version, ManifestVersions.PackageAssemblyReferences);
            }

            return version;
        }
    }
}
