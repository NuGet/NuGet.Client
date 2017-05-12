﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public static class PackageExtractor
    {
        public static async Task<IEnumerable<string>> ExtractPackageAsync(
            Stream packageStream,
            PackagePathResolver packagePathResolver,
            PackageExtractionContext packageExtractionContext,
            CancellationToken token)
        {
            if (packageStream == null)
            {
                throw new ArgumentNullException(nameof(packageStream));
            }

            if (!packageStream.CanSeek)
            {
                throw new ArgumentException(Strings.PackageStreamShouldBeSeekable);
            }

            if (packagePathResolver == null)
            {
                throw new ArgumentNullException(nameof(packagePathResolver));
            }

            if (packageExtractionContext == null)
            {
                throw new ArgumentNullException(nameof(packageExtractionContext));
            }

            var packageSaveMode = packageExtractionContext.PackageSaveMode;

            var filesAdded = new List<string>();

            var nupkgStartPosition = packageStream.Position;
            using (var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                var packageIdentityFromNuspec = await packageReader.GetIdentityAsync(CancellationToken.None);

                var installPath = packagePathResolver.GetInstallPath(packageIdentityFromNuspec);
                var packageDirectoryInfo = Directory.CreateDirectory(installPath);
                var packageDirectory = packageDirectoryInfo.FullName;

                var packageFiles = await packageReader.GetPackageFilesAsync(packageSaveMode, token);

                if ((packageSaveMode & PackageSaveMode.Nuspec) == PackageSaveMode.Nuspec)
                {
                    var sourceNuspecFile = packageFiles.Single(p => PackageHelper.IsManifest(p));

                    var targetNuspecPath = Path.Combine(
                        packageDirectory,
                        packagePathResolver.GetManifestFileName(packageIdentityFromNuspec));

                    // Extract the .nuspec file with a well known file name.
                    filesAdded.Add(packageReader.ExtractFile(
                        sourceNuspecFile,
                        targetNuspecPath,
                        packageExtractionContext.Logger));

                    packageFiles = packageFiles.Except(new[] { sourceNuspecFile });
                }

                var packageFileExtractor = new PackageFileExtractor(packageFiles, packageExtractionContext.XmlDocFileSaveMode);

                filesAdded.AddRange(await packageReader.CopyFilesAsync(
                    packageDirectory,
                    packageFiles,
                    packageFileExtractor.ExtractPackageFile,
                    packageExtractionContext.Logger,
                    token));

                if ((packageSaveMode & PackageSaveMode.Nupkg) == PackageSaveMode.Nupkg)
                {
                    // During package extraction, nupkg is the last file to be created
                    // Since all the packages are already created, the package stream is likely positioned at its end
                    // Reset it to the nupkgStartPosition
                    packageStream.Seek(nupkgStartPosition, SeekOrigin.Begin);

                    var nupkgFilePath = Path.Combine(
                        packageDirectory,
                        packagePathResolver.GetPackageFileName(packageIdentityFromNuspec));

                    filesAdded.Add(packageStream.CopyToFile(nupkgFilePath));
                }

                // Now, copy satellite files unless requested to not copy them
                if (packageExtractionContext.CopySatelliteFiles)
                {
                    filesAdded.AddRange(await CopySatelliteFilesAsync(
                        packageReader,
                        packagePathResolver,
                        packageSaveMode,
                        packageExtractionContext,
                        token));
                }
            }

            return filesAdded;
        }

        public static async Task<IEnumerable<string>> ExtractPackageAsync(
            PackageReaderBase packageReader,
            Stream packageStream,
            PackagePathResolver packagePathResolver,
            PackageExtractionContext packageExtractionContext,
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

            if (packageExtractionContext == null)
            {
                throw new ArgumentNullException(nameof(packageExtractionContext));
            }

            var packageSaveMode = packageExtractionContext.PackageSaveMode;

            var nupkgStartPosition = packageStream.Position;
            var filesAdded = new List<string>();

            var packageIdentityFromNuspec = await packageReader.GetIdentityAsync(token);

            var packageDirectoryInfo = Directory.CreateDirectory(packagePathResolver.GetInstallPath(packageIdentityFromNuspec));
            var packageDirectory = packageDirectoryInfo.FullName;

            var packageFiles = await packageReader.GetPackageFilesAsync(packageSaveMode, token);
            var packageFileExtractor = new PackageFileExtractor(packageFiles, packageExtractionContext.XmlDocFileSaveMode);
            filesAdded.AddRange(await packageReader.CopyFilesAsync(
                packageDirectory,
                packageFiles,
                packageFileExtractor.ExtractPackageFile,
                packageExtractionContext.Logger,
                token));

            var nupkgFilePath = Path.Combine(packageDirectory, packagePathResolver.GetPackageFileName(packageIdentityFromNuspec));
            if (packageSaveMode.HasFlag(PackageSaveMode.Nupkg))
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

                filesAdded.Add(packageStream.CopyToFile(nupkgFilePath));
            }

            // Now, copy satellite files unless requested to not copy them
            if (packageExtractionContext.CopySatelliteFiles)
            {
                filesAdded.AddRange(await CopySatelliteFilesAsync(
                    packageReader,
                    packagePathResolver,
                    packageSaveMode,
                    packageExtractionContext,
                    token));
            }

            return filesAdded;
        }

        /// <summary>
        /// Uses a copy function to install a package to a global packages directory.
        /// </summary>
        /// <param name="copyToAsync">
        /// A function which should copy the package to the provided destination stream.
        /// </param>
        /// <param name="versionFolderPathContext">
        /// The version folder path context, which encapsulates all of the parameters to observe
        /// while installing the package.
        /// </param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>
        /// True if the package was installed. False if the package already exists and therefore
        /// resulted in no copy operation.
        /// </returns>
        public static async Task<bool> InstallFromSourceAsync(
            Func<Stream, Task> copyToAsync,
            VersionFolderPathContext versionFolderPathContext,
            CancellationToken token)
        {
            if (copyToAsync == null)
            {
                throw new ArgumentNullException(nameof(copyToAsync));
            }

            if (versionFolderPathContext == null)
            {
                throw new ArgumentNullException(nameof(versionFolderPathContext));
            }

            var packagePathResolver = new VersionFolderPathResolver(
                versionFolderPathContext.PackagesDirectory,
                versionFolderPathContext.IsLowercasePackagesDirectory);

            var packageIdentity = versionFolderPathContext.Package;
            var logger = versionFolderPathContext.Logger;

            var targetPath = packagePathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(packageIdentity.Id, packageIdentity.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);
            var hashPath = packagePathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);

            logger.LogVerbose(
                $"Acquiring lock for the installation of {packageIdentity.Id} {packageIdentity.Version}");

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(targetNupkg,
                action: async cancellationToken =>
                {
                    // If this is the first process trying to install the target nupkg, go ahead
                    // After this process successfully installs the package, all other processes
                    // waiting on this lock don't need to install it again.
                    if (!File.Exists(hashPath))
                    {
                        logger.LogVerbose(
                            $"Acquired lock for the installation of {packageIdentity.Id} {packageIdentity.Version}");

                        logger.LogMinimal(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Log_InstallingPackage,
                            packageIdentity.Id,
                            packageIdentity.Version));

                        cancellationToken.ThrowIfCancellationRequested();

                        // We do not stop the package extraction after this point
                        // based on CancellationToken, but things can still be stopped if the process is killed.
                        if (Directory.Exists(targetPath))
                        {
                            // If we had a broken restore, clean out the files first
                            var info = new DirectoryInfo(targetPath);

                            foreach (var file in info.GetFiles())
                            {
                                file.Delete();
                            }

                            foreach (var dir in info.GetDirectories())
                            {
                                dir.Delete(true);
                            }
                        }
                        else
                        {
                            Directory.CreateDirectory(targetPath);
                        }

                        var targetTempNupkg = Path.Combine(targetPath, Path.GetRandomFileName());
                        var tempHashPath = Path.Combine(targetPath, Path.GetRandomFileName());
                        var packageSaveMode = versionFolderPathContext.PackageSaveMode;

                        // Extract the nupkg
                        using (var nupkgStream = new FileStream(
                            targetTempNupkg,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete,
                            bufferSize: 4096,
                            useAsync: true))
                        {
                            await copyToAsync(nupkgStream);
                            nupkgStream.Seek(0, SeekOrigin.Begin);

                            using (var packageReader = new PackageArchiveReader(nupkgStream))
                            {
                                var nuspecFile = packageReader.GetNuspecFile();
                                if ((packageSaveMode & PackageSaveMode.Nuspec) == PackageSaveMode.Nuspec)
                                {
                                    packageReader.ExtractFile(nuspecFile, targetNuspec, logger);
                                }

                                if ((packageSaveMode & PackageSaveMode.Files) == PackageSaveMode.Files)
                                {
                                    var nupkgFileName = Path.GetFileName(targetNupkg);
                                    var nuspecFileName = Path.GetFileName(targetNuspec);
                                    var hashFileName = Path.GetFileName(hashPath);
                                    var packageFiles = packageReader.GetFiles()
                                        .Where(file => ShouldInclude(file, hashFileName));
                                    var packageFileExtractor = new PackageFileExtractor(
                                        packageFiles,
                                        versionFolderPathContext.XmlDocFileSaveMode);
                                    packageReader.CopyFiles(
                                        targetPath,
                                        packageFiles,
                                        packageFileExtractor.ExtractPackageFile,
                                        logger,
                                        token);
                                }

                                string packageHash;
                                nupkgStream.Position = 0;
                                packageHash = Convert.ToBase64String(new CryptoHashProvider("SHA512").CalculateHash(nupkgStream));

                                File.WriteAllText(tempHashPath, packageHash);
                            }
                        }

                        // Now rename the tmp file
                        if ((versionFolderPathContext.PackageSaveMode & PackageSaveMode.Nupkg) ==
                            PackageSaveMode.Nupkg)
                        {
                            File.Move(targetTempNupkg, targetNupkg);
                        }
                        else
                        {
                            try
                            {
                                File.Delete(targetTempNupkg);
                            }
                            catch (IOException ex)
                            {
                                logger.LogWarning(string.Format(
                                    CultureInfo.CurrentCulture,
                                    Strings.ErrorUnableToDeleteFile,
                                    targetTempNupkg,
                                    ex.Message));
                            }
                        }

                        // Note: PackageRepository relies on the hash file being written out as the
                        // final operation as part of a package install to assume a package was fully installed.
                        // Rename the tmp hash file
                        File.Move(tempHashPath, hashPath);

                        logger.LogVerbose($"Completed installation of {packageIdentity.Id} {packageIdentity.Version}");
                        return true;
                    }
                    else
                    {
                        logger.LogVerbose("Lock not required - Package already installed "
                                            + $"{packageIdentity.Id} {packageIdentity.Version}");
                        return false;
                    }
                },
                token: token);
        }

        private static bool ShouldInclude(
            string fullName,
            string hashFileName)
        {
            // Not all the files from a zip file are needed
            // So, files such as '.rels' and '[Content_Types].xml' are not extracted
            var fileName = Path.GetFileName(fullName);
            if (fileName != null)
            {
                if (fileName == ".rels")
                {
                    return false;
                }
                if (fileName == "[Content_Types].xml")
                {
                    return false;
                }
            }

            var extension = Path.GetExtension(fullName);
            if (extension == ".psmdcp")
            {
                return false;
            }

            if (string.Equals(fullName, hashFileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Skip nupkgs and nuspec files found in the root, the valid ones are already extracted
            if (PackageHelper.IsRoot(fullName)
                && (PackageHelper.IsNuspec(fullName)
                    || fullName.EndsWith(PackagingCoreConstants.NupkgExtension, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        public static async Task<IEnumerable<string>> CopySatelliteFilesAsync(
            PackageIdentity packageIdentity,
            PackagePathResolver packagePathResolver,
            PackageSaveMode packageSaveMode,
            PackageExtractionContext packageExtractionContext,
            CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (packagePathResolver == null)
            {
                throw new ArgumentNullException(nameof(packagePathResolver));
            }

            var satelliteFilesCopied = Enumerable.Empty<string>();

            var nupkgFilePath = packagePathResolver.GetInstalledPackageFilePath(packageIdentity);
            if (File.Exists(nupkgFilePath))
            {
                using (var packageReader = new PackageArchiveReader(nupkgFilePath))
                {
                    return await CopySatelliteFilesAsync(
                        packageReader,
                        packagePathResolver,
                        packageSaveMode,
                        packageExtractionContext,
                        token);
                }
            }

            return satelliteFilesCopied;
        }

        private static async Task<IEnumerable<string>> CopySatelliteFilesAsync(
            PackageReaderBase packageReader,
            PackagePathResolver packagePathResolver,
            PackageSaveMode packageSaveMode,
            PackageExtractionContext packageExtractionContext,
            CancellationToken token)
        {
            if (packageReader == null)
            {
                throw new ArgumentNullException(nameof(packageReader));
            }

            if (packagePathResolver == null)
            {
                throw new ArgumentNullException(nameof(packagePathResolver));
            }

            if (packageExtractionContext == null)
            {
                throw new ArgumentNullException(nameof(packageExtractionContext));
            }

            var satelliteFilesCopied = Enumerable.Empty<string>();

            var result = await PackageHelper.GetSatelliteFilesAsync(packageReader, packagePathResolver, token);

            var runtimePackageDirectory = result.Item1;
            var satelliteFiles = result.Item2
                .Where(file => PackageHelper.IsPackageFile(file, packageSaveMode))
                .ToList();

            if (satelliteFiles.Count > 0)
            {
                var packageFileExtractor = new PackageFileExtractor(satelliteFiles, packageExtractionContext.XmlDocFileSaveMode);

                // Now, add all the satellite files collected from the package to the runtime package folder(s)
                satelliteFilesCopied = await packageReader.CopyFilesAsync(
                    runtimePackageDirectory,
                    satelliteFiles,
                    packageFileExtractor.ExtractPackageFile,
                    packageExtractionContext.Logger,
                    token);
            }

            return satelliteFilesCopied;
        }
    }
}