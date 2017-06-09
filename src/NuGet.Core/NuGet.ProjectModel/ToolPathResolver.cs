// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            return GetLockFilePath(GetLockFileDirectory(packageId, version, framework));
        }

        public string GetLockFilePath(string toolDirectory)
        {
            return Path.Combine(toolDirectory, LockFileFormat.AssetsFileName);
        }

        public string GetLockFileDirectory(string packageId, NuGetVersion version, NuGetFramework framework)
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

        /// <summary>
        /// Returns the directory (packagesFolder/.tools/id/version for example) for the best matching version if any. 
        /// </summary>
        /// <returns></returns>
        public string GetBestToolDirectory(string packageId, VersionRange versionRange, NuGetFramework framework)
        {
            var availableToolVersions = GetAvailableToolVersions(packageId);
            
            var bestVersion = versionRange.FindBestMatch(availableToolVersions);
            if (bestVersion == null)
            {
                return null;
            }

            return GetLockFileDirectory(packageId, bestVersion, framework);
        }

        private IEnumerable<NuGetVersion> GetAvailableToolVersions(string packageId)
        {
            var availableVersions = new List<NuGetVersion>();

            var toolBase = Path.Combine(GetToolsBasePath(), _isLowercase ? packageId.ToLowerInvariant() : packageId);
            if (!Directory.Exists(toolBase))
            {
                return Enumerable.Empty<NuGetVersion>();
            }

            var versionDirectories = Directory.EnumerateDirectories(toolBase);

            foreach (var versionDirectory in versionDirectories)
            {
                var version = Path.GetFileName(versionDirectory);

                NuGetVersion nugetVersion = null;
                NuGetVersion.TryParse(version, out nugetVersion);

                if (nugetVersion != null)
                {
                    availableVersions.Add(nugetVersion);
                }
            }

            return availableVersions;
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

    }

}
