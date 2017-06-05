// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class ToolPathResolver
    {
        private readonly string _packagesDirectory;
        private readonly bool _isLowercase;

        public ToolPathResolver(string packagesDirectory)
            : this(packagesDirectory, isLowercase: true)
        {
        }

        public ToolPathResolver(string packagesDirectory, bool isLowercase)
        {
            _packagesDirectory = packagesDirectory;
            _isLowercase = isLowercase;
        }

        public string GetLockFilePath(string packageId, NuGetVersion version, NuGetFramework framework)
        {
            var versionString = version.ToNormalizedString();
            var frameworkString = framework.GetShortFolderName();

            if (_isLowercase)
            {
                packageId = packageId.ToLowerInvariant();
                versionString = versionString.ToLowerInvariant();
                frameworkString = frameworkString.ToLowerInvariant();
            }

            var basePath = GetToolsBasePath();

            return Path.Combine(
                basePath,
                packageId,
                versionString,
                frameworkString,
                LockFileFormat.AssetsFileName);
        }

            public string GetCacheFilePath(string packageId, NuGetVersion version, NuGetFramework framework)
            {
                var versionString = version.ToNormalizedString();
                var frameworkString = framework.GetShortFolderName();

                if (_isLowercase)
                {
                    packageId = packageId.ToLowerInvariant();
                    versionString = versionString.ToLowerInvariant();
                    frameworkString = frameworkString.ToLowerInvariant();
                }

                var basePath = GetToolsBasePath();

                return Path.Combine(
                    basePath,
                    packageId,
                    versionString,
                    frameworkString);
            }

            public string GetToolsBasePath()
        {
            return Path.Combine(
                _packagesDirectory,
                ".tools");
        }
    }
}
