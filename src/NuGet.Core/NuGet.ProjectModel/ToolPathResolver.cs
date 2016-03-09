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

        public ToolPathResolver(string packagesDirectory)
        {
            _packagesDirectory = packagesDirectory;
        }

        public string GetLockFilePath(string packageId, NuGetVersion version, NuGetFramework framework)
        {
            return Path.Combine(
                _packagesDirectory,
                ".tools",
                packageId,
                version.ToNormalizedString(),
                framework.GetShortFolderName(),
                LockFileFormat.LockFileName);
        }
    }
}
