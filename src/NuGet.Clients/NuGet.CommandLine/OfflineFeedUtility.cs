using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine
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

            var nupkgFilePath
                = versionFolderPathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);

            var hashFilePath
                = versionFolderPathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);

            var nuspecFilePath
                = versionFolderPathResolver.GetManifestFilePath(packageIdentity.Id, packageIdentity.Version);

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

        public static void ThrowIfInvalid(string path)
        {
            Uri pathUri;
            if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out pathUri))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.Path_Invalid)),
                    path);
            }

            var invalidPathChars = Path.GetInvalidPathChars();
            if (invalidPathChars.Any(p => path.Contains(p)))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.Path_Invalid)),
                    path);
            }

            if (!pathUri.IsAbsoluteUri)
            {
                path = Path.GetFullPath(path);
                pathUri = new Uri(path);
            }

            if (!pathUri.IsFile && !pathUri.IsUnc)
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.Path_Invalid_NotFileNotUnc)),
                    path);
            }
        }

        public static void ThrowIfInvalidOrNotFound(
            string path,
            bool isDirectory,
            string nameOfNotFoundErrorResource)
        {
            if (nameOfNotFoundErrorResource == null)
            {
                throw new ArgumentNullException(nameof(nameOfNotFoundErrorResource));
            }

            ThrowIfInvalid(path);

            if ((isDirectory && !Directory.Exists(path)) ||
                (!isDirectory && !File.Exists(path)))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameOfNotFoundErrorResource),
                    path);
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
                    var packageReader = new PackageReader(packageStream);
                    var packageIdentity = packageReader.GetIdentity();

                    bool isValidPackage;
                    if (PackageExists(packageIdentity, source, out isValidPackage))
                    {
                        // Package already exists. Verify if it is valid
                        if (isValidPackage)
                        {
                            var message = string.Format(
                                CultureInfo.CurrentCulture,
                                LocalizedResourceManager.GetString(
                                nameof(NuGetResources.AddCommand_PackageAlreadyExists)), packageIdentity, source);

                            if (offlineFeedAddContext.ThrowIfPackageExists)
                            {
                                throw new CommandLineException(message);
                            }
                            else
                            {
                                logger.LogInformation(message);
                            }
                        }
                        else
                        {
                            var message = string.Format(
                                CultureInfo.CurrentCulture,
                                LocalizedResourceManager.GetString(
                                nameof(NuGetResources.AddCommand_ExistingPackageInvalid)), packageIdentity, source);

                            if (offlineFeedAddContext.ThrowIfPackageExistsAndInvalid)
                            {
                                throw new CommandLineException(message);
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
                        var versionFolderPathContext = new VersionFolderPathContext(
                            packageIdentity,
                            source,
                            logger,
                            fixNuspecIdCasing: false,
                            extractNuspecOnly: !offlineFeedAddContext.Expand,
                            normalizeFileNames: true);

                        await NuGetPackageUtils.InstallFromSourceAsync(
                            stream => packageStream.CopyToAsync(stream),
                            versionFolderPathContext,
                            token);

                        var message = string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizedResourceManager.GetString(nameof(NuGetResources.AddCommand_SuccessfullyAdded)),
                            packagePath,
                            source);

                        logger.LogInformation(message);
                    }
                }
                catch (InvalidDataException)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        LocalizedResourceManager.GetString(nameof(NuGetResources.NupkgPath_InvalidNupkg)),
                        packagePath);

                    if (offlineFeedAddContext.ThrowIfSourcePackageIsInvalid)
                    {
                        throw new CommandLineException(message);
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
