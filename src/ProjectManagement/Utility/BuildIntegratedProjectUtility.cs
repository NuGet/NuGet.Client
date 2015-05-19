// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Utilities for project.json
    /// </summary>
    public static class BuildIntegratedProjectUtility
    {
        /// <summary>
        /// project.json
        /// </summary>
        public const string ProjectConfigFileName = "project.json";

        /// <summary>
        /// Lock file name
        /// </summary>
        public const string ProjectLockFileName = "project.lock.json";

        /// <summary>
        /// Get the root path of a package from the global folder.
        /// </summary>
        public static string GetPackagePathFromGlobalSource(PackageIdentity identity)
        {
            return Path.Combine(GetGlobalPackagesFolder(), identity.Id, identity.Version.ToNormalizedString());
        }

        /// <summary>
        /// nupkg path from the global cache folder
        /// </summary>
        public static string GetNupkgPathFromGlobalSource(PackageIdentity identity)
        {
            var nupkgName = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.nupkg", identity.Id, identity.Version.ToNormalizedString());

            return Path.Combine(GetPackagePathFromGlobalSource(identity), nupkgName);
        }

        /// <summary>
        /// Global package folder path
        /// </summary>
        public static string GetGlobalPackagesFolder()
        {
            var path = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

            if (string.IsNullOrEmpty(path))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                path = Path.Combine(userProfile, ".nuget\\packages\\");
            }

            return path;
        }

        /// <summary>
        /// Create the lock file path from the config file path.
        /// </summary>
        public static string GetLockFilePath(string configFilePath)
        {
            return Path.Combine(Path.GetDirectoryName(configFilePath), ProjectLockFileName);
        }

        /// <summary>
        /// BuildIntegratedProjectReference -> ExternalProjectReference
        /// </summary>
        public static ExternalProjectReference ConvertProjectReference(BuildIntegratedProjectReference reference)
        {
            return new ExternalProjectReference(reference.Name, reference.PackageSpecPath, reference.ExternalProjectReferences);
        }
    }
}
