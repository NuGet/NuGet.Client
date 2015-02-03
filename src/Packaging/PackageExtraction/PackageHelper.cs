using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    internal static class PackageHelper
    {
        private static readonly string[] ExcludePaths = new[] { "_rels", "package" };
        public static bool IsManifest(string path)
        {
            return Path.GetExtension(path).Equals(PackagingConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPackageFile(string packageFileName, PackageSaveModes packageSaveMode)
        {
            if (String.IsNullOrEmpty(packageFileName) || String.IsNullOrEmpty(Path.GetFileName(packageFileName)))
            {
                // This is to ignore archive entries that are not really files
                return false;
            }
            if (packageSaveMode.HasFlag(PackageSaveModes.Nuspec))
            {
                return !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                return !IsManifest(packageFileName) && !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// A package is deemed to be a satellite package if it has a language property set, the id of the package is of the format [.*].[Language]
        /// and it has at least one dependency with an id that maps to the runtime package .
        /// </summary>
        public static bool IsSatellitePackage(NuspecReader nuspecReader, out PackageIdentity runtimePackageIdentity, out string packageLanguage)
        {
            // A satellite package has the following properties:
            //     1) A package suffix that matches the package's language, with a dot preceding it
            //     2) A dependency on the package with the same Id minus the language suffix
            //     3) The dependency can be found by Id in the repository (as its path is needed for installation)
            // Example: foo.ja-jp, with a dependency on foo

            string packageId = nuspecReader.GetId();
            packageLanguage = nuspecReader.GetLanguage();
            bool result = false;
            string localruntimePackageId = null;

            if (!String.IsNullOrEmpty(packageLanguage) &&
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
                            && dependencyPackage.VersionRange.IsMaxInclusive && dependencyPackage.VersionRange.IsMinInclusive)
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

        public static async Task CreatePackageFiles(IEnumerable<ZipArchiveEntry> packageFiles, string packageDirectory,
            PackageSaveModes packageSaveMode, CancellationToken token)
        {
            foreach (var entry in packageFiles)
            {
                if (PackageHelper.IsPackageFile(entry.FullName, packageSaveMode))
                {
                    var packageFileFullPath = Path.Combine(packageDirectory, entry.FullName);
                    using (var inputStream = entry.Open())
                    {
                        await CreatePackageFile(packageFileFullPath, inputStream, token);
                    }
                }
            }
        }

        public static async Task CreatePackageFile(string packageFileFullPath, Stream inputStream, CancellationToken token)
        {
            string directory = Path.GetDirectoryName(packageFileFullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(packageFileFullPath))
            {
                // Log and skip adding file
                return;
            }

            using (Stream outputStream = File.Create(packageFileFullPath))
            {
                await inputStream.CopyToAsync(outputStream);
            }
        }
    }

}
