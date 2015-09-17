// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Packaging.Build
{
    public class PhysicalPackageFile : IPackageFile
    {
        public PhysicalPackageFile(string sourcePath, string targetPath, int? version)
        {
            PhysicalPath = sourcePath;
            Path = targetPath;
            Version = version;
        }

        public int? Version { get; }

        public string Path { get; }

        public string PhysicalPath { get; }

        public Stream GetStream()
        {
            return File.OpenRead(PhysicalPath);
        }
    }
}
