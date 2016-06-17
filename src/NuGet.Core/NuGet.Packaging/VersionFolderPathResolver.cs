// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public class VersionFolderPathResolver
    {
        private readonly string _path;

        public VersionFolderPathResolver(string path)
        {
            _path = path;
        }

        public string GetInstallPath(string packageId, NuGetVersion version)
        {
            return Path.Combine(
                _path,
                GetPackageDirectory(packageId, version));
        }

        public string GetVersionListPath(string packageId)
        {
            return Path.Combine(
                _path,
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
                $"{Normalize(packageId)}.{Normalize(version)}.nupkg.sha512");
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
            return version.ToNormalizedString().ToLowerInvariant();
        }

        private string Normalize(string packageId)
        {
            return packageId.ToLowerInvariant();
        }
    }
}
