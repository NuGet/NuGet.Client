using NuGet.PackagingCore;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NuGet.Packaging
{
    public static class PackageExtractor
    {
        public static void ExtractPackage(Stream packageStream, PackageIdentity packageIdentity, PackagePathResolver packagePathResolver, PackageSaveModes packageSaveMode = PackageSaveModes.Nupkg)
        {
            // TODO: Need to handle PackageSaveMode
            // TODO: Need to handle satellite package files differently
            // TODO: Support overwriting files also?
            long nupkgStartPosition = packageStream.Position;
            var zipArchive = new ZipArchive(packageStream);
            var packageDirectoryInfo = Directory.CreateDirectory(packagePathResolver.GetInstallPath(packageIdentity));
            foreach (var entry in zipArchive.Entries)
            {
                if (PackageHelper.IsPackageFile(entry.FullName, packageSaveMode))
                {
                    var packageFileFullPath = Path.Combine(packageDirectoryInfo.FullName, entry.FullName);
                    using (var inputStream = entry.Open())
                    {
                        CreatePackageFile(packageFileFullPath, inputStream);
                    }
                }
            }

            if(packageSaveMode.HasFlag(PackageSaveModes.Nupkg))
            {
                var nupkgFilePath = Path.Combine(packageDirectoryInfo.FullName, packagePathResolver.GetPackageFileName(packageIdentity));
                // During package extraction, nupkg is the last file to be created
                // Since all the packages are already created, the package stream is likely positioned at its end
                // Reset it to the nupkgStartPosition
                packageStream.Seek(nupkgStartPosition, SeekOrigin.Begin);
                CreatePackageFile(nupkgFilePath, packageStream);
            }
        }

        private static void CreatePackageFile(string packageFileFullPath, Stream inputStream)
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
                inputStream.CopyTo(outputStream);
            }
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
