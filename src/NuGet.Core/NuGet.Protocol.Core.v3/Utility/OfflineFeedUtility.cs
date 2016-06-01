// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Protocol.Core.v3;
using Strings = NuGet.Protocol.Core.v3.Strings;

namespace NuGet.Protocol.Core.Types
{
    public static class OfflineFeedUtility
    {
        public static bool PackageExists(
            PackageIdentity packageIdentity,
            string offlineFeed,
            out bool isValidPackage)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (string.IsNullOrEmpty(offlineFeed))
            {
                throw new ArgumentNullException(nameof(offlineFeed));
            }

            var versionFolderPathResolver = new VersionFolderPathResolver(offlineFeed, normalizePackageId: true);
            string nupkgFilePath = versionFolderPathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);
            string hashFilePath = versionFolderPathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);
            string nuspecFilePath = versionFolderPathResolver.GetManifestFilePath(packageIdentity.Id, packageIdentity.Version);

            var nupkgFileExists = File.Exists(nupkgFilePath);

            var hashFileExists = File.Exists(hashFilePath);

            var nuspecFileExists = File.Exists(nuspecFilePath);

            if (nupkgFileExists || hashFileExists || nuspecFileExists)
            {
                if (!nupkgFileExists || !hashFileExists || !nuspecFileExists)
                {
                    // One of the necessary files to represent the package in the feed does not exist
                    isValidPackage = false;
                }
                else
                {
                    // All the necessary files to represent the package in the feed are present.
                    // Check if the existing nupkg matches the hash. Otherwise, it is considered invalid.
                    var packageHash = GetHash(nupkgFilePath);
                    var existingHash = File.ReadAllText(hashFilePath);

                    isValidPackage = packageHash.Equals(existingHash, StringComparison.Ordinal);
                }

                return true;
            }

            isValidPackage = false;
            return false;
        }
        public static string GetPackageDirectory(PackageIdentity packageIdentity, string offlineFeed)
        {
            var versionFolderPathResolver = new VersionFolderPathResolver(offlineFeed, normalizePackageId: true);
            return Path.GetDirectoryName(
                versionFolderPathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version));
        }

        public static void ThrowIfInvalid(string path)
        {
            Uri pathUri = UriUtility.TryCreateSourceUri(path, UriKind.RelativeOrAbsolute);
            if (pathUri == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Path_Invalid,
                    path));
            }

            var invalidPathChars = Path.GetInvalidPathChars();
            if (invalidPathChars.Any(p => path.Contains(p)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Path_Invalid,
                    path));
            }

            if (!pathUri.IsAbsoluteUri)
            {
                path = Path.GetFullPath(path);
                pathUri = new Uri(path);
            }

            if (!pathUri.IsFile && !pathUri.IsUnc)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.Path_Invalid_NotFileNotUnc,
                    path));
            }
        }

        public static void ThrowIfInvalidOrNotFound(
            string path,
            bool isDirectory,
            string resourceString)
        {
            if (resourceString == null)
            {
                throw new ArgumentNullException(nameof(resourceString));
            }

            ThrowIfInvalid(path);

            if ((isDirectory && !Directory.Exists(path)) ||
                (!isDirectory && !File.Exists(path)))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    resourceString,
                    path));
            }
        }

        public static async Task AddPackageToSource(
            OfflineFeedAddContext offlineFeedAddContext,
            CancellationToken token)
        {
            var packagePath = offlineFeedAddContext.PackagePath;
            var source = offlineFeedAddContext.Source;
            var logger = offlineFeedAddContext.Logger;

            using (var packageStream = File.OpenRead(packagePath))
            {
                try
                {
                    var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
                    var packageIdentity = packageReader.GetIdentity();

                    bool isValidPackage;
                    if (PackageExists(packageIdentity, source, out isValidPackage))
                    {
                        // Package already exists. Verify if it is valid
                        if (isValidPackage)
                        {
                            var message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.AddPackage_PackageAlreadyExists,
                                packageIdentity, 
                                source);

                            if (offlineFeedAddContext.ThrowIfPackageExists)
                            {
                                throw new ArgumentException(message);
                            }
                            else
                            {
                                logger.LogMinimal(message);
                            }
                        }
                        else
                        {
                            var message = string.Format(CultureInfo.CurrentCulture,
                                Strings.AddPackage_ExistingPackageInvalid, 
                                packageIdentity, 
                                source);

                            if (offlineFeedAddContext.ThrowIfPackageExistsAndInvalid)
                            {
                                throw new ArgumentException(message);
                            }
                            else
                            {
                                logger.LogWarning(message);
                            }
                        }
                    }
                    else
                    {
                        packageStream.Seek(0, SeekOrigin.Begin);
                        var packageSaveMode = offlineFeedAddContext.Expand
                            ? PackageSaveMode.Defaultv3
                            : PackageSaveMode.Nuspec | PackageSaveMode.Nupkg;

                        var versionFolderPathContext = new VersionFolderPathContext(
                            packageIdentity,
                            source,
                            logger,
                            fixNuspecIdCasing: false,
                            packageSaveMode: packageSaveMode,
                            normalizeFileNames: true,
                            xmlDocFileSaveMode: PackageExtractionBehavior.XmlDocFileSaveMode);

                        await PackageExtractor.InstallFromSourceAsync(
                            stream => packageStream.CopyToAsync(stream),
                            versionFolderPathContext,
                            token);

                        var message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.AddPackage_SuccessfullyAdded,
                            packagePath,
                            source);

                        logger.LogMinimal(message);
                    }
                }
                catch (InvalidDataException)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.NupkgPath_Invalid,
                        packagePath);

                    if (offlineFeedAddContext.ThrowIfSourcePackageIsInvalid)
                    {
                        throw new ArgumentException(message);
                    }
                    else
                    {
                        logger.LogWarning(message);
                    }
                }
            }
        }

        private static string GetHash(string nupkgFilePath)
        {
            string packageHash;
            using (var nupkgStream
                = File.Open(nupkgFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sha512 = SHA512.Create())
                {
                    packageHash = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
                }
            }

            return packageHash;
        }
    }
}
