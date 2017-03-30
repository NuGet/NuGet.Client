﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Protocol
{
    /// <summary>
    /// Packages.config packages folder reader
    /// </summary>
    public class FindLocalPackagesResourcePackagesConfig : FindLocalPackagesResource
    {
        public FindLocalPackagesResourcePackagesConfig(string root)
        {
            Root = root;
        }

        public override IEnumerable<LocalPackageInfo> FindPackagesById(string id, ILogger logger, CancellationToken token)
        {
            var packages = LocalFolderUtility.GetPackagesConfigFolderPackages(Root, id, logger);

            // Filter out any duplicates that may appear in the folder multiple times.
            return LocalFolderUtility.GetDistinctPackages(packages);
        }

        public override LocalPackageInfo GetPackage(Uri path, ILogger logger, CancellationToken token)
        {
            return LocalFolderUtility.GetPackage(path, logger);
        }

        public override LocalPackageInfo GetPackage(PackageIdentity identity, ILogger logger, CancellationToken token)
        {
            return LocalFolderUtility.GetPackagesConfigFolderPackage(Root, identity, logger);
        }

        public override IEnumerable<LocalPackageInfo> GetPackages(ILogger logger, CancellationToken token)
        {
            var packages = LocalFolderUtility.GetPackagesConfigFolderPackages(Root, logger);

            // Filter out any duplicates that may appear in the folder multiple times.
            return LocalFolderUtility.GetDistinctPackages(packages);
        }
    }
}
