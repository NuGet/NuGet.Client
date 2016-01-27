// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    public class LocalPackageInfo
    {
        public LocalPackageInfo(
            string packageId,
            NuGetVersion version,
            string path,
            string manifestPath,
            string zipPath)
        {
            Id = packageId;
            Version = version;
            ExpandedPath = path;
            ManifestPath = manifestPath;
            ZipPath = zipPath;
        }

        public string Id { get; }

        public NuGetVersion Version { get; }

        public string ExpandedPath { get; set; }

        public string ManifestPath { get; }

        public string ZipPath { get; }

        public override string ToString()
        {
            return Id + " " + Version + " (" + (ManifestPath ?? ZipPath) + ")";
        }
    }
}
