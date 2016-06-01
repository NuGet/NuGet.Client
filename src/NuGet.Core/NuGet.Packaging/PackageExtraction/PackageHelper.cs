// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public static class PackageHelper
    {
        private static readonly string[] ExcludePaths = new[]
        {
            "_rels/",
            "package/",
            @"_rels\",
            @"package\",
            "[Content_Types].xml"
        };

        private const string ExcludeExtension = ".nupkg.sha512";

        public static bool IsAssembly(string path)
        { 
            return path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || 
                   path.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase) || 
                   path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase); 
        }

        public static bool IsManifest(string path)
        {
            return Path.GetExtension(path).Equals(PackagingCoreConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPackageFile(string packageFileName, PackageSaveMode packageSaveMode)
        {
            if (String.IsNullOrEmpty(packageFileName)
                || String.IsNullOrEmpty(Path.GetFileName(packageFileName)))
            {
                // This is to ignore archive entries that are not really files
                return false;
            }

            if (IsManifest(packageFileName))
            {
                return (packageSaveMode & PackageSaveMode.Nuspec) == PackageSaveMode.Nuspec;
            }

            if ((packageSaveMode & PackageSaveMode.Files) == PackageSaveMode.Files)
            {
                return !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                    !packageFileName.EndsWith(ExcludeExtension, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// A package is deemed to be a satellite package if it has a language property set, the id of the package is
        /// of the format [.*].[Language]
        /// and it has at least one dependency with an id that maps to the runtime package .
        /// </summary>
        public static bool IsSatellitePackage(IPackageCoreReader packageReader, out PackageIdentity runtimePackageIdentity, out string packageLanguage)
        {
            // A satellite package has the following properties:
            //     1) A package suffix that matches the package's language, with a dot preceding it
            //     2) A dependency on the package with the same Id minus the language suffix
            //     3) The dependency can be found by Id in the repository (as its path is needed for installation)
            // Example: foo.ja-jp, with a dependency on foo

            var nuspecReader = new NuspecReader(packageReader.GetNuspec());
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

        public static IEnumerable<string> GetSatelliteFiles(
            PackageReaderBase packageReader,
            PackagePathResolver packagePathResolver,
            out string runtimePackageDirectory)
        {
            var satelliteFileEntries = new List<string>();
            runtimePackageDirectory = null;

            PackageIdentity runtimePackageIdentity = null;
            string packageLanguage = null;
            if (IsSatellitePackage(packageReader, out runtimePackageIdentity, out packageLanguage))
            {
                // Now, we know that the package is a satellite package and that the runtime package is 'runtimePackageId'
                // Check, if the runtimePackage is installed and get the folder to copy over files

                var runtimePackageFilePath = packagePathResolver.GetInstalledPackageFilePath(runtimePackageIdentity);
                if (File.Exists(runtimePackageFilePath))
                {
                    // Existence of the package file is the validation that the package exists
                    runtimePackageDirectory = Path.GetDirectoryName(runtimePackageFilePath);
                    satelliteFileEntries.AddRange(packageReader.GetSatelliteFiles(packageLanguage));
                }
            }

            return satelliteFileEntries;
        }

        /// <summary>
        /// This returns all the installed package files (does not include satellite files)
        /// </summary>
        public static IEnumerable<ZipFilePair> GetInstalledPackageFiles(
            PackageArchiveReader packageReader,
            PackageIdentity packageIdentity,
            PackagePathResolver packagePathResolver,
            PackageSaveMode packageSaveMode)
        {
            var installedPackageFiles = Enumerable.Empty<ZipFilePair>();

            var packageDirectory = packagePathResolver.GetInstalledPath(packageIdentity);
            if (!string.IsNullOrEmpty(packageDirectory))
            {
                var packageFiles = packageReader.GetPackageFiles(packageSaveMode);
                var entries = packageReader.EnumeratePackageEntries(packageFiles, packageDirectory);
                installedPackageFiles = entries.Where(e => e.IsInstalled());
            }

            return installedPackageFiles.ToList();
        }

        public static Tuple<string, IEnumerable<ZipFilePair>> GetInstalledSatelliteFiles(
            PackageArchiveReader packageReader,
            PackagePathResolver packagePathResolver,
            PackageSaveMode packageSaveMode)
        {
            var installedSatelliteFiles = Enumerable.Empty<ZipFilePair>();
            string runtimePackageDirectory;
            var satelliteFiles = GetSatelliteFiles(packageReader, packagePathResolver, out runtimePackageDirectory);
            if (satelliteFiles.Any())
            {
                var satelliteFileEntries = packageReader.EnumeratePackageEntries(
                    satelliteFiles.Where(f => IsPackageFile(f, packageSaveMode)),
                    runtimePackageDirectory);
                installedSatelliteFiles = satelliteFileEntries.Where(e => e.IsInstalled());
            }

            return new Tuple<string, IEnumerable<ZipFilePair>>(runtimePackageDirectory, installedSatelliteFiles.ToList());
        }
    }
}
