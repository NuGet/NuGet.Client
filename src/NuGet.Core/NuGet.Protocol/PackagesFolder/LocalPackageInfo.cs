// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    public class LocalPackageInfo
    {
        private readonly Lazy<NuspecReader> _nuspec;
        private readonly Lazy<IReadOnlyList<string>> _files;

        public LocalPackageInfo(
            string packageId,
            NuGetVersion version,
            string path,
            string manifestPath,
            string zipPath,
            Lazy<NuspecReader> nuspec)
        {
            Id = packageId;
            Version = version;
            ExpandedPath = path;
            ManifestPath = manifestPath;
            ZipPath = zipPath;
            _nuspec = nuspec;
            _files = new Lazy<IReadOnlyList<string>>(() => GetFiles());
        }

        public string Id { get; }

        public NuGetVersion Version { get; }

        public string ExpandedPath { get; set; }

        public string ManifestPath { get; }

        public string ZipPath { get; }

        /// <summary>
        /// Caches the nuspec reader.
        /// If the nuspec does not exist this will throw a friendly exception.
        /// </summary>
        public NuspecReader Nuspec => _nuspec.Value;

        /// <summary>
        /// Package files with OPC files filtered out.
        /// Cached to avoid reading the same files multiple times.
        /// </summary>
        public IReadOnlyList<string> Files => _files.Value;

        public override string ToString()
        {
            return Id + " " + Version + " (" + (ManifestPath ?? ZipPath) + ")";
        }

        /// <summary>
        /// Read files from a package folder.
        /// </summary>
        private IReadOnlyList<string> GetFiles()
        {
            using (var packageReader = new PackageFolderReader(ExpandedPath))
            {
                // Get package files, excluding directory entries and OPC files
                // This is sorted before it is written out
                return packageReader.GetFiles()
                    .Where(file => IsAllowedLibraryFile(file))
                    .ToList();
            }
        }

        /// <summary>
        /// True if the file should be added to the lock file library
        /// Fale if it is an OPC file or empty directory
        /// </summary>
        private static bool IsAllowedLibraryFile(string path)
        {
            switch (path)
            {
                case "_rels/.rels":
                case "[Content_Types].xml":
                    return false;
            }

            if (path.EndsWith("/", StringComparison.Ordinal)
                || path.EndsWith(".psmdcp", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}
