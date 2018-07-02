// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.VisualStudio
{
    // Implementation of IVsPathContext without support of reference resolving.
    // Used when project is not managed by NuGet or given project doesn't have any packages installed.
    internal class VsPathContext : IVsPathContext2
    {
        public string UserPackageFolder { get; }

        public IEnumerable FallbackPackageFolders { get; }

        public string SolutionPackageFolder { get; }

        public VsPathContext(NuGetPathContext pathContext, string solutionPackageFolder = null)
        {
            if (pathContext == null)
            {
                throw new ArgumentNullException(nameof(pathContext));
            }

            UserPackageFolder = pathContext.UserPackageFolder;
            FallbackPackageFolders = pathContext.FallbackPackageFolders;
            SolutionPackageFolder = solutionPackageFolder;
        }

        public VsPathContext(string userPackageFolder, IEnumerable<string> fallbackPackageFolders)
        {
            if (userPackageFolder == null)
            {
                throw new ArgumentNullException(nameof(userPackageFolder));
            }

            if (fallbackPackageFolders == null)
            {
                throw new ArgumentNullException(nameof(fallbackPackageFolders));
            }

            UserPackageFolder = userPackageFolder;
            FallbackPackageFolders = fallbackPackageFolders.ToList();
        }

        public bool TryResolvePackageAsset(string packageAssetPath, out string packageDirectoryPath)
        {
            // unable to resolve the reference file path without the index
            packageDirectoryPath = null;
            return false;
        }
    }
}
