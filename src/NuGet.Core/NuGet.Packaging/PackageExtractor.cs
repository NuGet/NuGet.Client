// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using ZipFilePair = System.Tuple<string, System.IO.Compression.ZipArchiveEntry>;

namespace NuGet.Packaging
{
    public static class PackageExtractor
    {
        public static async Task<IEnumerable<string>> ExtractPackageAsync(
            Stream packageStream,
            PackagePathResolver packagePathResolver,
            PackageExtractionContext packageExtractionContext,
            PackageSaveModes packageSaveMode,
            CancellationToken token)
        {
            var filesAdded = new List<string>();
            if (packageStream == null)
            {
                throw new ArgumentNullException("packageStream");
            }

            if (!packageStream.CanSeek)
            {
                throw new ArgumentException(Strings.PackageStreamShouldBeSeekable);
            }

            if (packagePathResolver == null)
            {
                throw new ArgumentNullException("packagePathResolver");
            }

            // TODO: Need to handle PackageSaveMode
            // TODO: Support overwriting files also?
            var nupkgStartPosition = packageStream.Position;
            var zipArchive = new ZipArchive(packageStream);

            var packageReader = new PackageReader(zipArchive);

            var packageIdentityFromNuspec = packageReader.GetIdentity();

            var packageDirectoryInfo = Directory.CreateDirectory(packagePathResolver.GetInstallPath(packageIdentityFromNuspec));
            var packageDirectory = packageDirectoryInfo.FullName;

            filesAdded.AddRange(await PackageHelper.CreatePackageFiles(zipArchive.Entries, packageDirectory, packageSaveMode, token));

            var nupkgFilePath = Path.Combine(packageDirectory, packagePathResolver.GetPackageFileName(packageIdentityFromNuspec));
            if (packageSaveMode.HasFlag(PackageSaveModes.Nupkg))
            {
                // During package extraction, nupkg is the last file to be created
                // Since all the packages are already created, the package stream is likely positioned at its end
                // Reset it to the nupkgStartPosition
                packageStream.Seek(nupkgStartPosition, SeekOrigin.Begin);
                filesAdded.Add(await PackageHelper.CreatePackageFile(nupkgFilePath, packageStream, token));
            }

            // Now, copy satellite files unless requested to not copy them
            if (packageExtractionContext == null
                || packageExtractionContext.CopySatelliteFiles)
            {
                filesAdded.AddRange(await CopySatelliteFilesAsync(packageIdentityFromNuspec, packagePathResolver, packageSaveMode, token));
            }

            return filesAdded;
        }

        public static async Task<IEnumerable<string>> ExtractPackageAsync(
            PackageReaderBase packageReader,
            Stream packageStream,
            PackagePathResolver packagePathResolver,
            PackageExtractionContext packageExtractionContext,
            PackageSaveModes packageSaveMode,
            CancellationToken token)
        {
            if (packageStream == null)
            {
                throw new ArgumentNullException(nameof(packageStream));
            }

            if (packagePathResolver == null)
            {
                throw new ArgumentNullException(nameof(packagePathResolver));
            }

            // TODO: Need to handle PackageSaveMode
            // TODO: Support overwriting files also?
            var nupkgStartPosition = packageStream.Position;
            var filesAdded = new List<string>();

            var packageIdentityFromNuspec = packageReader.GetIdentity();

            var packageDirectoryInfo = Directory.CreateDirectory(packagePathResolver.GetInstallPath(packageIdentityFromNuspec));
            var packageDirectory = packageDirectoryInfo.FullName;

            var zipPackageReader = packageReader as PackageReader;

            if (zipPackageReader != null)
            {
                // For zip files, use the ZipArchive directly
                var files = await PackageHelper.CreatePackageFiles(
                    zipPackageReader.ZipArchive.Entries, 
                    packageDirectory, 
                    packageSaveMode, 
                    token);

                filesAdded.AddRange(files);
            }
            else
            {
                // Folder readers
                foreach (var file in packageReader.GetFiles().Where(file => PackageHelper.IsPackageFile(file, packageSaveMode)))
                {
                    token.ThrowIfCancellationRequested();

                    var targetPath = Path.Combine(packageDirectory, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

                    using (var sourceStream = packageReader.GetStream(file))
                    {
                        using (var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024, useAsync: true))
                        {
                            await sourceStream.CopyToAsync(targetStream);
                        }
                    }

                    filesAdded.Add(targetPath);
                }
            }

            var nupkgFilePath = Path.Combine(packageDirectory, packagePathResolver.GetPackageFileName(packageIdentityFromNuspec));
            if (packageSaveMode.HasFlag(PackageSaveModes.Nupkg))
            {
                // During package extraction, nupkg is the last file to be created
                // Since all the packages are already created, the package stream is likely positioned at its end
                // Reset it to the nupkgStartPosition
                if (packageStream.Position != 0)
                {
                    if (!packageStream.CanSeek)
                    {
                        throw new ArgumentException(Strings.PackageStreamShouldBeSeekable);
                    }

                    packageStream.Position = 0;
                }

                filesAdded.Add(await PackageHelper.CreatePackageFile(nupkgFilePath, packageStream, token));
            }

            // Now, copy satellite files unless requested to not copy them
            if (packageExtractionContext == null || packageExtractionContext.CopySatelliteFiles)
            {
                PackageIdentity runtimeIdentity;
                string packageLanguage;

                var nuspecReader = new NuspecReader(packageReader.GetNuspec());
                var isSatellitePackage = PackageHelper.IsSatellitePackage(nuspecReader, out runtimeIdentity, out packageLanguage);

                // Short-circuit this if the package is not a satellite package.
                if (isSatellitePackage)
                {
                    filesAdded.AddRange(await CopySatelliteFilesAsync(packageIdentityFromNuspec, packagePathResolver, packageSaveMode, token));
                }
            }

            return filesAdded;
        }
        public static async Task<IEnumerable<string>> CopySatelliteFilesAsync(PackageIdentity packageIdentity, PackagePathResolver packagePathResolver,
            PackageSaveModes packageSaveMode, CancellationToken token)
        {
            var satelliteFilesCopied = Enumerable.Empty<string>();
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if (packagePathResolver == null)
            {
                throw new ArgumentNullException("packagePathResolver");
            }

            var nupkgFilePath = packagePathResolver.GetInstalledPackageFilePath(packageIdentity);
            if (File.Exists(nupkgFilePath))
            {
                using (var packageStream = File.OpenRead(nupkgFilePath))
                {
                    string language;
                    string runtimePackageDirectory;
                    IEnumerable<ZipArchiveEntry> satelliteFiles;
                    if (PackageHelper.GetSatelliteFiles(packageStream, packageIdentity, packagePathResolver, out language, out runtimePackageDirectory, out satelliteFiles))
                    {
                        // Now, add all the satellite files collected from the package to the runtime package folder(s)
                        satelliteFilesCopied = await PackageHelper.CreatePackageFiles(satelliteFiles, runtimePackageDirectory, packageSaveMode, token);
                    }
                }
            }

            return satelliteFilesCopied;
        }
    }
}
