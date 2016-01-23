using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public static class PackageReaderExtensions
    {
        public static IEnumerable<string> GetPackageFiles(this IPackageCoreReader packageReader, PackageSaveMode packageSaveMode)
        {
            return packageReader.GetFiles().Where(file => PackageHelper.IsPackageFile(file, packageSaveMode));
        }

        public static IEnumerable<string> GetSatelliteFiles(this IPackageContentReader packageReader, string packageLanguage)
        {
            var satelliteFiles = new List<string>();

            // Existence of the package file is the validation that the package exists
            var libItemGroups = packageReader.GetLibItems();
            foreach (var libItemGroup in libItemGroups)
            {
                var satelliteFilesInGroup = libItemGroup.Items.Where(item => Path.GetDirectoryName(item).Split(Path.DirectorySeparatorChar)
                    .Contains(packageLanguage, StringComparer.OrdinalIgnoreCase));

                satelliteFiles.AddRange(satelliteFilesInGroup);
            }

            return satelliteFiles;
        }

        public static string GetNuspecFile(this IPackageCoreReader packageReader)
        {
            // Find all nuspec files in the root folder of the zip.
            var nuspecEntries = packageReader.GetFiles()
                .Select(f => f.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))
                .Where(f => f.EndsWith(PackagingCoreConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase))
                .Where(f => string.Equals(f, Path.GetFileName(f), StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (nuspecEntries.Length == 0)
            {
                throw new PackagingException(Strings.MissingNuspec);
            }
            else if (nuspecEntries.Length > 1)
            {
                throw new PackagingException(Strings.MultipleNuspecFiles);
            }

            return nuspecEntries[0];
        }
    }
}
