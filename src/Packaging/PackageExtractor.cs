using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NuGet.Packaging
{
    public static class PackageExtractor
    {
        public static void ExtractPackage(Stream packageStream, string rootDirectory, PackageSaveModes packageSaveMode = PackageSaveModes.Nupkg)
        {
            // TODO: Need to handle PackageSaveMode
            // TODO: Need to handle satellite package files differently
            // TODO: Support overwriting files also?
            var zipArchive = new ZipArchive(packageStream);
            var directory = Directory.CreateDirectory(rootDirectory);
            foreach (var entry in zipArchive.Entries)
            {
                if (PackageHelper.IsPackageFile(entry.FullName, packageSaveMode))
                {
                    var packageFileFullPath = Path.Combine(directory.FullName, entry.FullName);

                    if(File.Exists(packageFileFullPath))
                    {
                        // Log and skip to next package file
                        continue;
                    }
                    using (Stream outputStream = CreateFileWithDirectory(packageFileFullPath))
                    {
                        entry.Open().CopyTo(outputStream);
                    }
                }
            }

            if(packageSaveMode.HasFlag(PackageSaveModes.Nupkg))
            {
                // TODO
            }
        }

        private static Stream CreateFileWithDirectory(string packageFileFullPath)
        {
            string directory = Path.GetDirectoryName(packageFileFullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return File.Create(packageFileFullPath);
        }
    }

    internal static class PackageHelper
    {
        private static readonly string[] ExcludePaths = new[] { "_rels", "package" };
        public static bool IsManifest(string path)
        {
            return Path.GetExtension(path).Equals(PackagingConstants.NuspecExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPackageFile(string packageFileName, PackageSaveModes packageSaveMode)
        {
            if(packageSaveMode.HasFlag(PackageSaveModes.Nuspec))
            {
                return !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                return !IsManifest(packageFileName) && !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            } 
        }
    }

    [Flags]
    public enum PackageSaveModes
    {
        None = 0,
        Nuspec = 1,

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Naming",
            "CA1704:IdentifiersShouldBeSpelledCorrectly",
            MessageId = "Nupkg",
            Justification = "nupkg is the file extension of the package file")]
        Nupkg = 2
    }
}
