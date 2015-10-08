﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using ZipFilePair = System.Tuple<string, System.IO.Compression.ZipArchiveEntry>;

namespace NuGet.Packaging
{
    public static class PackageHelper
    {
        private static readonly string[] ExcludePaths = new[] { "_rels", "package", "[Content_Types].xml" };
        private static readonly string[] ExcludeExtension = new[] { ".nupkg.sha512" };

        public static bool IsManifest(string path)
        {
            return Path.GetExtension(path).Equals(PackagingCoreConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPackageFile(string packageFileName, PackageSaveModes packageSaveMode)
        {
            if (String.IsNullOrEmpty(packageFileName)
                || String.IsNullOrEmpty(Path.GetFileName(packageFileName)))
            {
                // This is to ignore archive entries that are not really files
                return false;
            }
            if (packageSaveMode.HasFlag(PackageSaveModes.Nuspec))
            {
                return !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase)) && 
                    !ExcludeExtension.Any(p => packageFileName.EndsWith(p, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                return !IsManifest(packageFileName) && !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase)) && 
                    !ExcludeExtension.Any(p => packageFileName.EndsWith(p, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// A package is deemed to be a satellite package if it has a language property set, the id of the package is
        /// of the format [.*].[Language]
        /// and it has at least one dependency with an id that maps to the runtime package .
        /// </summary>
        public static bool IsSatellitePackage(NuspecReader nuspecReader, out PackageIdentity runtimePackageIdentity, out string packageLanguage)
        {
            // A satellite package has the following properties:
            //     1) A package suffix that matches the package's language, with a dot preceding it
            //     2) A dependency on the package with the same Id minus the language suffix
            //     3) The dependency can be found by Id in the repository (as its path is needed for installation)
            // Example: foo.ja-jp, with a dependency on foo

            var packageId = nuspecReader.GetId();
            packageLanguage = nuspecReader.GetLanguage();
            string localruntimePackageId = null;

            if (!String.IsNullOrEmpty(packageLanguage)
                &&
                packageId.EndsWith('.' + packageLanguage, StringComparison.OrdinalIgnoreCase))
            {
                // The satellite pack's Id is of the format <Core-Package-Id>.<Language>. Extract the core package id using this.
                // Additionally satellite packages have a strict dependency on the core package
                localruntimePackageId = packageId.Substring(0, packageId.Length - packageLanguage.Length - 1);

                foreach (var group in nuspecReader.GetDependencyGroups())
                {
                    foreach (var dependencyPackage in group.Packages)
                    {
                        if (dependencyPackage.Id.Equals(localruntimePackageId, StringComparison.OrdinalIgnoreCase)
                            && dependencyPackage.VersionRange != null
                            && dependencyPackage.VersionRange.MaxVersion == dependencyPackage.VersionRange.MinVersion
                            && dependencyPackage.VersionRange.IsMaxInclusive
                            && dependencyPackage.VersionRange.IsMinInclusive)
                        {
                            var runtimePackageVersion = new NuGetVersion(dependencyPackage.VersionRange.MinVersion.ToNormalizedString());
                            runtimePackageIdentity = new PackageIdentity(dependencyPackage.Id, runtimePackageVersion);
                            return true;
                        }
                    }
                }
            }

            runtimePackageIdentity = null;
            return false;
        }

        public static bool GetSatelliteFiles(Stream packageStream, PackageIdentity packageIdentity, PackagePathResolver packagePathResolver,
            out string language, out string runtimePackageDirectory, out IEnumerable<ZipArchiveEntry> satelliteFiles)
        {
            var zipArchive = new ZipArchive(packageStream);
            var packageReader = new PackageReader(zipArchive);
            var nuspecReader = new NuspecReader(packageReader.GetNuspec());

            PackageIdentity runtimePackageIdentity = null;
            string packageLanguage = null;
            if (IsSatellitePackage(nuspecReader, out runtimePackageIdentity, out packageLanguage))
            {
                // Now, we know that the package is a satellite package and that the runtime package is 'runtimePackageId'
                // Check, if the runtimePackage is installed and get the folder to copy over files

                var runtimePackageFilePath = packagePathResolver.GetInstalledPackageFilePath(runtimePackageIdentity);
                if (File.Exists(runtimePackageFilePath))
                {
                    runtimePackageDirectory = Path.GetDirectoryName(runtimePackageFilePath);
                    // Existence of the package file is the validation that the package exists
                    var libItemGroups = packageReader.GetLibItems();
                    var satelliteFileEntries = new List<ZipArchiveEntry>();
                    foreach (var libItemGroup in libItemGroups)
                    {
                        var satelliteFilesInGroup = libItemGroup.Items.Where(item => Path.GetDirectoryName(item).Split(Path.DirectorySeparatorChar)
                            .Contains(packageLanguage, StringComparer.OrdinalIgnoreCase));

                        foreach (var satelliteFile in satelliteFilesInGroup)
                        {
                            var zipArchiveEntry = zipArchive.GetEntry(satelliteFile);
                            if (zipArchiveEntry != null)
                            {
                                satelliteFileEntries.Add(zipArchiveEntry);
                            }
                        }
                    }

                    if (satelliteFileEntries.Count > 0)
                    {
                        language = packageLanguage;
                        satelliteFiles = satelliteFileEntries;
                        return true;
                    }
                }
            }

            language = null;
            runtimePackageDirectory = null;
            satelliteFiles = null;
            return false;
        }

        public static Task<IEnumerable<ZipFilePair>> GetPackageFiles(IEnumerable<ZipArchiveEntry> packageFiles, string packageDirectory,
            PackageSaveModes packageSaveMode, CancellationToken token)
        {
            var effectivePackageFiles = new List<ZipFilePair>();
            foreach (var entry in packageFiles)
            {
                var path = ZipArchiveHelper.UnescapePath(entry.FullName);

                if (IsPackageFile(path, packageSaveMode))
                {
                    var packageFileFullPath = Path.Combine(packageDirectory, path);
                    effectivePackageFiles.Add(new ZipFilePair(packageFileFullPath, entry));
                }
            }

            return Task.FromResult<IEnumerable<ZipFilePair>>(effectivePackageFiles);
        }

        public static IEnumerable<ZipFilePair> GetInstalledPackageFiles(IEnumerable<ZipFilePair> packageFiles)
        {
            var installedPackageFiles = new List<ZipFilePair>();
            foreach (var packageFile in packageFiles)
            {
                if (packageFile != null
                    && packageFile.Item1 != null
                    && packageFile.Item2 != null
                    && File.Exists(packageFile.Item1))
                {
                    installedPackageFiles.Add(packageFile);
                }
            }

            return installedPackageFiles;
        }

        /// <summary>
        /// This returns all the installed package files (does not include satellite files)
        /// </summary>
        /// <param name="packageIdentity"></param>
        /// <param name="packagePathResolver"></param>
        /// <param name="packageDirectory"></param>
        /// <param name="packageSaveMode"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<ZipFilePair>> GetInstalledPackageFiles(Stream packageStream,
            PackageIdentity packageIdentity,
            PackagePathResolver packagePathResolver,
            PackageSaveModes packageSaveMode,
            CancellationToken token)
        {
            var installedPackageFiles = new List<ZipFilePair>();
            var packageDirectory = packagePathResolver.GetInstalledPath(packageIdentity);
            if (!String.IsNullOrEmpty(packageDirectory))
            {
                var zipArchive = new ZipArchive(packageStream);
                var packageFiles = await GetPackageFiles(zipArchive.Entries, packageDirectory, packageSaveMode, token);
                installedPackageFiles.AddRange(GetInstalledPackageFiles(packageFiles));
            }

            return installedPackageFiles;
        }

        public static async Task<Tuple<string, IEnumerable<ZipFilePair>>> GetInstalledSatelliteFiles(Stream packageStream,
            PackageIdentity packageIdentity,
            PackagePathResolver packagePathResolver,
            PackageSaveModes packageSaveMode,
            CancellationToken token)
        {
            var installedSatelliteFiles = new List<ZipFilePair>();
            string language;
            string runtimePackageDirectory;
            IEnumerable<ZipArchiveEntry> satelliteFileEntries;
            if (GetSatelliteFiles(packageStream, packageIdentity, packagePathResolver, out language, out runtimePackageDirectory, out satelliteFileEntries))
            {
                var satelliteFiles = await GetPackageFiles(satelliteFileEntries, runtimePackageDirectory, packageSaveMode, token);
                installedSatelliteFiles.AddRange(GetInstalledPackageFiles(satelliteFiles));
            }

            return new Tuple<string, IEnumerable<ZipFilePair>>(runtimePackageDirectory, installedSatelliteFiles);
        }

        internal static async Task<string> CreatePackageFile(string packageFileFullPath, Stream inputStream, CancellationToken token)
        {
            var directory = Path.GetDirectoryName(packageFileFullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(packageFileFullPath))
            {
                // Log and skip adding file
                return packageFileFullPath;
            }

            using (Stream outputStream = File.Create(packageFileFullPath))
            {
                await inputStream.CopyToAsync(outputStream);
            }

            return packageFileFullPath;
        }

        internal static async Task<IEnumerable<string>> CreatePackageFiles(IEnumerable<ZipArchiveEntry> packageFiles, string packageDirectory,
            PackageSaveModes packageSaveMode, CancellationToken token)
        {
            var effectivePackageFiles = await GetPackageFiles(packageFiles, packageDirectory, packageSaveMode, token);
            foreach (var effectivePackageFile in effectivePackageFiles)
            {
                var packageFileFullPath = effectivePackageFile.Item1;
                var entry = effectivePackageFile.Item2;
                using (var inputStream = entry.Open())
                {
                    await CreatePackageFile(packageFileFullPath, inputStream, token);
                }
            }

            return effectivePackageFiles.Select(pf => pf.Item1);
        }
    }
}
