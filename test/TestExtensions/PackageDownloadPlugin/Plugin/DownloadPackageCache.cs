// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Reflection;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class DownloadPackageCache
    {
        internal DirectoryInfo Directory { get; }

        private DownloadPackageCache(DirectoryInfo directory)
        {
            Directory = directory;
        }

        internal static DownloadPackageCache Create()
        {
            var thisDirectory = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory;
            var cacheDirectory = new DirectoryInfo(Path.Combine(thisDirectory.FullName, "DownloadPackageCache"));

            if (cacheDirectory.Exists)
            {
                cacheDirectory.Delete(recursive: true);
            }

            cacheDirectory.Create();

            return new DownloadPackageCache(cacheDirectory);
        }
    }
}