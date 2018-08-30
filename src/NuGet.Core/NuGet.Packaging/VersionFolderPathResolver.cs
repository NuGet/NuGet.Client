// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public class VersionFolderPathResolver
    {
        /// <summary>
        /// Packages directory root folder.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// True if package id and versions are made lowercase.
        /// </summary>
        public bool IsLowerCase { get; }

        /// <summary>
        /// VersionFolderPathResolver
        /// </summary>
        /// <param name="rootPath">Packages directory root folder.</param>
        public VersionFolderPathResolver(string rootPath) : this(rootPath, isLowercase: true)
        {
        }

        /// <summary>
        /// VersionFolderPathResolver
        /// </summary>
        /// <param name="rootPath">Packages directory root folder.</param>
        /// <param name="isLowercase">True if package id and versions are made lowercase.</param>
        public VersionFolderPathResolver(string rootPath, bool isLowercase)
        {
            RootPath = rootPath;
            IsLowerCase = isLowercase;
        }

        public string GetInstallPath(string packageId, NuGetVersion version)
        {
            return Path.Combine(
                RootPath,
                GetPackageDirectory(packageId, version));
        }

        public string GetVersionListPath(string packageId)
        {
            return Path.Combine(
                RootPath,
                GetVersionListDirectory(packageId));
        }

        public string GetPackageFilePath(string packageId, NuGetVersion version)
        {
            return Path.Combine(
                GetInstallPath(packageId, version),
                GetPackageFileName(packageId, version));
        }

        public string GetManifestFilePath(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(
                GetInstallPath(packageId, version),
                GetManifestFileName(packageId, version));
        }

        public string GetHashPath(string packageId, NuGetVersion version)
        {
            return Path.Combine(
                GetInstallPath(packageId, version),
                GetHashFileName(packageId, version));
        }

        public string GetHashFileName(string packageId, NuGetVersion version)
        {
            return $"{Normalize(packageId)}.{Normalize(version)}.nupkg.sha512";
        }

        public string GetVersionListDirectory(string packageId)
        {
            return Normalize(packageId);
        }

        public string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            return Path.Combine(
                GetVersionListDirectory(packageId),
                Normalize(version));
        }

        public string GetPackageFileName(string packageId, NuGetVersion version)
        {
            return $"{Normalize(packageId)}.{Normalize(version)}.nupkg";
        }

        public string GetManifestFileName(string packageId, NuGetVersion version)
        {
            return $"{Normalize(packageId)}.nuspec";
        }

        private string Normalize(NuGetVersion version)
        {
            var versionString = version.ToNormalizedString();

            if (IsLowerCase)
            {
                versionString = versionString.ToLowerInvariant();
            }

            return versionString;
        }

        private string Normalize(string packageId)
        {
            if (IsLowerCase)
            {
                packageId = packageId.ToLowerInvariant();
            }

            return packageId;
        }
    }
}
