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

        public virtual string GetInstallPath(string packageId, SemanticVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(_path, GetPackageDirectory(packageId, version));
        }

        public string GetPackageFilePath(string packageId, SemanticVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(GetInstallPath(packageId, version),
                GetPackageFileName(packageId, version));
        }

        public string GetManifestFilePath(string packageId, SemanticVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(GetInstallPath(packageId, version),
                GetManifestFileName(packageId, version));
        }

        public string GetHashPath(string packageId, SemanticVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(GetInstallPath(packageId, version),
                string.Format("{0}.{1}.nupkg.sha512", packageId, version.ToNormalizedString()));
        }

        public virtual string GetPackageDirectory(string packageId, SemanticVersion version)
        {
            packageId = Normalize(packageId);
            return Path.Combine(packageId, version.ToNormalizedString());
        }

        public virtual string GetPackageFileName(string packageId, SemanticVersion version)
        {
            packageId = Normalize(packageId);
            return string.Format("{0}.{1}.nupkg", packageId, version.ToNormalizedString());
        }

        public virtual string GetManifestFileName(string packageId, SemanticVersion version)
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
