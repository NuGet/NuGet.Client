// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public static class LocalFolderUtility
    {
        // *.nupkg
        private static readonly string NupkgFilter = $"*{NuGetConstants.PackageExtension}";

        /// <summary>
        /// Retrieve a nupkg using the path.
        /// </summary>
        /// <param name="path">Nupkg path in uri form.</param>
        public static LocalPackageInfo GetPackage(Uri path, ILogger log)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            return GetPackageFromNupkg(new FileInfo(path.LocalPath));
        }

        /// <summary>
        /// Retrieve all packages from a folder and one level deep.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        public static IEnumerable<LocalPackageInfo> GetPackagesV2(string root, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            return GetPackagesFromNupkgs(GetNupkgsFromFlatFolder(root, log));
        }

        /// <summary>
        /// Retrieve all packages of an id from a v2 folder.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        /// <param name="id">Package id.</param>
        public static IEnumerable<LocalPackageInfo> GetPackagesV2(string root, string id, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            foreach (var package in GetPackagesFromNupkgs(GetNupkgsFromFlatFolder(root, id, log)))
            {
                // Filter out any packages that were incorrectly identified
                // Ex: id: packageA.1 version: 1.0 -> packageA.1.1.0 -> packageA 1.1.0
                if (StringComparer.OrdinalIgnoreCase.Equals(id, package.Identity.Id))
                {
                    yield return package;
                }
            }

            yield break;
        }

        /// <summary>
        /// Retrieve all packages of an id from a v2 folder.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        /// <param name="id">Package id.</param>
        /// <param name="version">Package version.</param>
        public static LocalPackageInfo GetPackageV2(string root, string id, NuGetVersion version, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            return GetPackageV2(root, new PackageIdentity(id, version), log);
        }

        /// <summary>
        /// Retrieve all packages of an id from a v2 folder.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        /// <param name="identity">Package id and version.</param>
        public static LocalPackageInfo GetPackageV2(string root, PackageIdentity identity, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            // Search directories starting with the top directory for any package matching the identity
            // If multiple packages are found in the same directory that match (ex: 1.0, 1.0.0.0)
            // then favor the exact non-normalized match. If no exact match is found take the first
            // using the file system sort order. This is to match the legacy nuget 2.8.x behavior.
            foreach (var directoryList in GetNupkgsFromFlatFolderChunked(root, log))
            {
                LocalPackageInfo fallbackMatch = null;

                // Check for any files that are in the form packageId.version.nupkg
                foreach (var file in directoryList.Where(file => IsPossiblePackageMatch(file, identity)))
                {
                    var package = GetPackageFromNupkg(file);

                    if (identity.Equals(package.Identity))
                    {
                        if (StringComparer.OrdinalIgnoreCase.Equals(
                            identity.Version.ToString(),
                            package.Identity.Version.ToString()))
                        {
                            // Take an exact match immediately
                            return package;
                        }
                        else if (fallbackMatch == null)
                        {
                            // This matches the identity, but there may be an exact match still
                            fallbackMatch = package;
                        }
                    }
                }

                if (fallbackMatch != null)
                {
                    // Use the fallback match if an exact match was not found
                    return fallbackMatch;
                }
            }

            // Not found
            return null;
        }

        /// <summary>
        /// True if the file name matches the identity. This is could be incorrect if
        /// the package name ends with numbers. The result should be checked against the nuspec.
        /// </summary>
        public static bool IsPossiblePackageMatch(FileInfo file, PackageIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            return identity.Equals(GetIdentityFromFile(file, identity.Id));
        }

        /// <summary>
        /// True if the file name matches the id and is followed by a version. This is could be incorrect if
        /// the package name ends with numbers. The result should be checked against the nuspec.
        /// </summary>
        public static bool IsPossiblePackageMatch(FileInfo file, string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            return GetIdentityFromFile(file, id) != null;
        }

        /// <summary>
        /// An imperfect attempt at finding the identity of a package from the file name.
        /// This can fail if the package name ends with something such as .1
        /// </summary>
        public static PackageIdentity GetIdentityFromFile(FileInfo file, string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            PackageIdentity result = null;
            var prefix = $"{id}.";
            var fileName = file.Name;

            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && file.Name.EndsWith(PackagingCoreConstants.NupkgExtension, StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);

                // Skip symbol packages
                if (fileName.Length > prefix.Length && !fileName.EndsWith(".symbols", StringComparison.OrdinalIgnoreCase))
                {
                    var versionString = fileName.Substring(prefix.Length, fileName.Length - prefix.Length);

                    NuGetVersion version;
                    if (NuGetVersion.TryParse(versionString, out version))
                    {
                        result = new PackageIdentity(id, version);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Retrieve a single package from a v3 version folder.
        /// </summary>
        public static LocalPackageInfo GetPackageV3(string root, string id, NuGetVersion version, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            return GetPackageV3(root, new PackageIdentity(id, version), log);
        }

        /// <summary>
        /// Retrieve a package from a v3 feed.
        /// </summary>
        public static LocalPackageInfo GetPackageV3(string root, PackageIdentity identity, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var pathResolver = new VersionFolderPathResolver(root);

            var packageRoot = pathResolver.GetInstallPath(identity.Id, identity.Version);

            // Verify the neccessary files exist
            var nupkgPath = pathResolver.GetPackageFilePath(identity.Id, identity.Version);
            var nuspecPath = pathResolver.GetManifestFilePath(identity.Id, identity.Version);
            var hashPath = pathResolver.GetHashPath(identity.Id, identity.Version);

            if (!File.Exists(nupkgPath))
            {
                log.LogDebug($"Missing {nupkgPath}");
                return null;
            }

            if (!File.Exists(nuspecPath))
            {
                log.LogDebug($"Missing {nuspecPath}");
                return null;
            }

            if (!File.Exists(hashPath))
            {
                log.LogDebug($"Missing {hashPath}");
                return null;
            }

            var packageHelper = new Func<PackageReaderBase>(() => new PackageArchiveReader(nupkgPath));
            var nuspecHelper = new Lazy<NuspecReader>(() => new NuspecReader(nuspecPath));

            return new LocalPackageInfo(
                new PackageIdentity(identity.Id, identity.Version),
                nupkgPath,
                File.GetLastWriteTimeUtc(nupkgPath),
                nuspecHelper,
                packageHelper
            );
        }

        /// <summary>
        /// Discover all nupkgs from a v2 local folder.
        /// </summary>
        /// <param name="root">Folder root.</param>
        public static IEnumerable<FileInfo> GetNupkgsFromFlatFolder(string root, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            // Check for package files one level deep.
            DirectoryInfo rootDirectoryInfo = null;

            try
            {
                // Verify that the directory is a valid path
                rootDirectoryInfo = new DirectoryInfo(root);
            }
            catch (ArgumentException ex)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToRetrievePackage, root);

                throw new FatalProtocolException(message, ex);
            }

            // Return all directory file list chunks in a flat list
            foreach (var directoryList in GetNupkgsFromFlatFolderChunked(rootDirectoryInfo.FullName, log))
            {
                foreach (var file in directoryList)
                {
                    yield return file;
                }
            }

            yield break;
        }

        /// <summary>
        /// Retrieve files in chunks, this helps maintain the legacy behavior of searching for
        /// certain non-normalized file names.
        /// </summary>
        private static IEnumerable<FileInfo[]> GetNupkgsFromFlatFolderChunked(string root, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            // Ignore missing directories for v2
            if (!Directory.Exists(root))
            {
                yield break;
            }

            // Search the top level directory
            var topLevel = GetNupkgsFromDirectory(root, log);

            if (topLevel.Length > 0)
            {
                yield return topLevel;
            }

            // Search all sub directories
            foreach (var subDirectory in GetDirectoriesSafe(root, log))
            {
                var files = GetNupkgsFromDirectory(subDirectory.FullName, log);

                if (files.Length > 0)
                {
                    yield return files;
                }
            }

            yield break;
        }

        /// <summary>
        /// Discover nupkgs from a v2 local folder.
        /// </summary>
        /// <param name="root">Folder root.</param>
        /// <param name="id">Package id file name prefix.</param>
        public static IEnumerable<FileInfo> GetNupkgsFromFlatFolder(string root, string id, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            foreach (var path in GetNupkgsFromFlatFolder(root, log))
            {
                if (IsPossiblePackageMatch(path, id))
                {
                    yield return path;
                }
            }

            yield break;
        }

        /// <summary>
        /// Find all nupkgs in the top level of a directory.
        /// </summary>
        private static FileInfo[] GetNupkgsFromDirectory(string root, ILogger log)
        {
            return GetFilesSafe(root, NupkgFilter, log);
        }

        /// <summary>
        /// Discover all nupkgs from a v3 folder.
        /// </summary>
        /// <param name="root">Folder root.</param>
        public static IEnumerable<LocalPackageInfo> GetPackagesV3(string root, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            // Match all nupkgs in the folder
            foreach (var idPath in GetDirectoriesSafe(root, log))
            {
                foreach (var nupkg in GetPackagesV3(root, id: idPath.Name, log: log))
                {
                    yield return nupkg;
                }
            }

            yield break;
        }

        /// <summary>
        /// Discover nupkgs from a v3 local folder.
        /// </summary>
        /// <param name="root">Folder root.</param>
        /// <param name="id">Package id or package id prefix.</param>
        public static IEnumerable<LocalPackageInfo> GetPackagesV3(string root, string id, ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            // Check for package files one level deep.
            DirectoryInfo rootDirectoryInfo = null;

            try
            {
                rootDirectoryInfo = new DirectoryInfo(root);
            }
            catch (ArgumentException ex)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToRetrievePackage, root);

                throw new FatalProtocolException(message, ex);
            }

            if (!Directory.Exists(rootDirectoryInfo.FullName))
            {
                // Directory is missing
                yield break;
            }

            var pathResolver = new VersionFolderPathResolver(root);
            var idRoot = Path.Combine(root, id);

            foreach (var versionDir in GetDirectoriesSafe(idRoot, log))
            {
                NuGetVersion version;
                if (NuGetVersion.TryParse(versionDir.Name, out version))
                {
                    var identity = new PackageIdentity(id, version);

                    // Read the package, this may be null if files are missing
                    var package = GetPackageV3(root, identity, log);

                    if (package != null)
                    {
                        yield return package;
                    }
                }
                else
                {
                    log.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.UnableToParseFolderV3Version, versionDir));
                }
            }

            yield break;
        }

        /// <summary>
        /// Remove duplicate packages which can occur in directories.
        /// In V2 packages may exist under multiple sub folders. 
        /// Non-normalized versions also lead to duplicates: ex: 1.0, 1.0.0.0
        /// </summary>
        public static IEnumerable<LocalPackageInfo> GetDistinctPackages(IEnumerable<LocalPackageInfo> packages)
        {
            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            var seen = new HashSet<PackageIdentity>();

            foreach (var package in packages)
            {
                if (seen.Add(package.Identity))
                {
                    yield return package;
                }
            }

            yield break;
        }

        /// <summary>
        /// Retrieve directories and log exceptions that occur.
        /// </summary>
        private static DirectoryInfo[] GetDirectoriesSafe(string root, ILogger log)
        {
            try
            {
                var rootDir = new DirectoryInfo(root);
                return rootDir.GetDirectories();
            }
            catch (Exception e)
            {
                log.LogWarning(e.Message);
            }

            return new DirectoryInfo[0];
        }

        /// <summary>
        /// Retrieve files and log exceptions that occur.
        /// </summary>
        private static FileInfo[] GetFilesSafe(string root, string filter, ILogger log)
        {
            try
            {
                var rootDir = new DirectoryInfo(root);
                return rootDir.GetFiles(filter);
            }
            catch (Exception e)
            {
                log.LogWarning(e.Message);
            }

            return new FileInfo[0];
        }

        /// <summary>
        /// Path -> LocalPackageInfo
        /// </summary>
        private static IEnumerable<LocalPackageInfo> GetPackagesFromNupkgs(IEnumerable<FileInfo> files)
        {
            return files.Select(file => GetPackageFromNupkg(file));
        }

        /// <summary>
        /// Path -> LocalPackageInfo
        /// </summary>
        private static LocalPackageInfo GetPackageFromNupkg(FileInfo nupkgFile)
        {
            try
            {
                using (var package = new PackageArchiveReader(nupkgFile.FullName))
                {
                    var nuspec = package.NuspecReader;

                    var packageHelper = new Func<PackageReaderBase>(() => new PackageArchiveReader(nupkgFile.FullName));
                    var nuspecHelper = new Lazy<NuspecReader>(() => nuspec);

                    return new LocalPackageInfo(
                        nuspec.GetIdentity(),
                        nupkgFile.FullName,
                        nupkgFile.LastWriteTimeUtc,
                        nuspecHelper,
                        packageHelper
                    );
                }
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.NupkgPath_InvalidEx,
                    nupkgFile.FullName,
                    ex.Message);

                throw new FatalProtocolException(message, ex);
            }
        }
    }
}
