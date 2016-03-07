// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public class VersionFolderPathResolver
    {
        private readonly string _path;
        private readonly bool _normalizePackageId;

        public VersionFolderPathResolver(string path, bool normalizePackageId = false)
        {
            _path = path;
            _normalizePackageId = normalizePackageId;
        }

        public virtual string GetInstallPath(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(_path, GetPackageDirectory(packageId, version));
        }

        public string GetPackageFilePath(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(GetInstallPath(packageId, version),
                GetPackageFileName(packageId, version));
        }

        public string GetManifestFilePath(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(GetInstallPath(packageId, version),
                GetManifestFileName(packageId, version));
        }

        public string GetHashPath(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(GetInstallPath(packageId, version),
                $"{packageId}.{version.ToNormalizedString()}.nupkg.sha512");
        }

        public virtual string GetPackageDirectory(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(packageId, version.ToNormalizedString());
        }

        public virtual string GetPackageFileName(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return $"{packageId}.{version.ToNormalizedString()}.nupkg";
        }

        public virtual string GetManifestFileName(string packageId, NuGetVersion version)
        {
            packageId = Normalize(packageId);
            return packageId + ".nuspec";
        }

        private string Normalize(string packageId)
        {
            if (_normalizePackageId)
            {
                packageId = packageId.ToLowerInvariant();
            }

            return packageId;
        }
    }
}
