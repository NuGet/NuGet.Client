// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Reflection;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class OfflinePackageCache
    {
        internal DirectoryInfo Directory { get; }

        private OfflinePackageCache(DirectoryInfo directory)
        {
            Directory = directory;
        }

        internal static OfflinePackageCache Create()
        {
            var thisDirectory = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
            var cacheDirectory = new DirectoryInfo(Path.Combine(thisDirectory.FullName, "OfflinePackageCache"));

            if (!cacheDirectory.Exists)
            {
                cacheDirectory.Create();
            }

            return new OfflinePackageCache(cacheDirectory);
        }
    }
}