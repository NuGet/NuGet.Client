// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
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

            var file = GetAndVerifyFileInfo(path);

            return GetPackageFromNupkg(file);
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

            // Verify the root path is a valid path.
            GetAndVerifyRootDirectory(root);

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
        /// Retrieve a package with an id and version from a packages.config packages folder.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        public static IEnumerable<LocalPackageInfo> GetPackagesConfigFolderPackages(
            string root,
            ILogger log)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var rootDir = new DirectoryInfo(root);

            // Find the matching nupkg for each sub directory.
            if (rootDir.Exists)
            {
                foreach (var dir in rootDir.GetDirectories())
                {
                    var package = GetPackagesConfigFolderPackage(dir);

                    // Ensure that the nupkg file exists
                    if (package != null)
                    {
                        yield return package;
                    }
                }
            }

            yield break;
        }

        /// <summary>
        /// Retrieve a package with an id and version from a packages.config packages folder.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        public static IEnumerable<LocalPackageInfo> GetPackagesConfigFolderPackages(
            string root,
            string id,
            ILogger log)
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

            var rootDir = new DirectoryInfo(root);

            if (rootDir.Exists)
            {
                var searchPattern = GetPackagesConfigFolderSearchPattern(id);

                foreach (var dir in rootDir.GetDirectories(searchPattern, SearchOption.TopDirectoryOnly))
                {
                    // Check the id and version of the path, if the id matches and the version
                    // is valid this will be non-null;
                    var dirVersion = GetVersionFromIdVersionString(dir.Name, id);

                    if (dirVersion != null)
                    {
                        var package = GetPackagesConfigFolderPackage(dir);

                        if (package != null)
                        {
                            yield return package;
                        }
                    }
                }
            }

            yield break;
        }

        /// <summary>
        /// Retrieve a package with an id and version from a packages.config packages folder.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        /// <param name="identity">Package id and version.</param>
        public static LocalPackageInfo GetPackagesConfigFolderPackage(string root, PackageIdentity identity, ILogger log)
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

            // Try matching the exact version format
            var idVersion = $"{identity.Id}.{identity.Version.ToString()}";
            var expectedPath = Path.Combine(root, idVersion, $"{idVersion}{PackagingCoreConstants.NupkgExtension}");
            var expectedFile = new FileInfo(expectedPath);

            if (expectedFile.Exists)
            {
                var localPackage = GetPackageFromNupkg(expectedFile);

                // Verify that the nuspec matches the expected id/version.
                if (localPackage != null && identity.Equals(localPackage.Identity))
                {
                    return localPackage;
                }
            }

            // Search all sub folders
            var rootDir = new DirectoryInfo(root);

            if (rootDir.Exists)
            {
                var searchPattern = GetPackagesConfigFolderSearchPattern(identity.Id);

                foreach (var dir in rootDir.GetDirectories(searchPattern, SearchOption.TopDirectoryOnly))
                {
                    // Check the id and version of the path, if the id matches and the version
                    // is valid this will be non-null;
                    var dirVersion = GetVersionFromIdVersionString(dir.Name, identity.Id);

                    if (identity.Version == dirVersion)
                    {
                        var localPackage = GetPackagesConfigFolderPackage(dir);

                        // Verify that the nuspec matches the expected id/version.
                        if (localPackage != null && identity.Equals(localPackage.Identity))
                        {
                            return localPackage;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns either id.* or * depending on the OS.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static string GetPackagesConfigFolderSearchPattern(string id)
        {
            // Case insensitive searches on windows may use the id prefix.
            // The majority of packages.config scenarios will be on windows.
            if (!string.IsNullOrEmpty(id) && RuntimeEnvironmentHelper.IsWindows)
            {
                return $"{id}.*";
            }

            // For non-windows systems which may be case-sensitive search all directories
            return $"*";
        }

        /// <summary>
        /// Retrieve a package with an id and version from a packages.config packages folder.
        /// </summary>
        /// <param name="root">Nupkg folder directory path.</param>
        public static LocalPackageInfo GetPackagesConfigFolderPackage(
            string root,
            string id,
            NuGetVersion version,
            ILogger log)
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

            var identity = new PackageIdentity(id, version);
            return GetPackagesConfigFolderPackage(root, identity, log);
        }

        /// <summary>
        /// Return the package nupkg from a packages.config folder sub directory.
        /// </summary>
        /// <param name="dir">Package directory in the format id.version</param>
        private static LocalPackageInfo GetPackagesConfigFolderPackage(DirectoryInfo dir)
        {
            LocalPackageInfo result = null;

            var nupkgPath = Path.Combine(
                dir.FullName,
                $"{dir.Name}{PackagingCoreConstants.NupkgExtension}");

            var nupkgFile = new FileInfo(nupkgPath);

            if (nupkgFile.Exists)
            {
                result = GetPackageFromNupkg(nupkgFile);
            }

            return result;
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

            return identity.Equals(GetIdentityFromNupkgPath(file, identity.Id));
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

            return GetIdentityFromNupkgPath(file, id) != null;
        }

        /// <summary>
        /// An imperfect attempt at finding the identity of a package from the file name.
        /// This can fail if the package name ends with something such as .1
        /// </summary>
        public static PackageIdentity GetIdentityFromNupkgPath(FileInfo file, string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var version = GetVersionFromFileName(file.Name, id, PackagingCoreConstants.NupkgExtension);

            if (version != null)
            {
                return new PackageIdentity(id, version);
            }

            return null;
        }

        /// <summary>
        /// An imperfect attempt at finding the version of a package from the file name.
        /// This can fail if the package name ends with something such as .1
        /// </summary>
        public static NuGetVersion GetVersionFromFileName(string fileName, string id, string extension)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            NuGetVersion result = null;

            if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);

                // Skip symbol packages
                if (!fileName.EndsWith(".symbols", StringComparison.OrdinalIgnoreCase))
                {
                    result = GetVersionFromIdVersionString(fileName, id);
                }
            }

            return result;
        }

        /// <summary>
        /// Parse a possible version from a string in the format Id.Version
        /// Returns null if the version is invalid or the id did not match.
        /// </summary>
        /// <param name="idVersionString">Id.Version</param>
        /// <param name="id">Expected id</param>
        private static NuGetVersion GetVersionFromIdVersionString(string idVersionString, string id)
        {
            var prefix = $"{id}.";

            if (idVersionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var versionString = idVersionString.Substring(prefix.Length);

                NuGetVersion version;
                if (NuGetVersion.TryParse(versionString, out version))
                {
                    return version;
                }
            }

            return null;
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

            // Verify the root path is a valid path.
            GetAndVerifyRootDirectory(root);

            var pathResolver = new VersionFolderPathResolver(root);

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
            DirectoryInfo rootDirectoryInfo = GetAndVerifyRootDirectory(root);

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
        /// Verify that a path could be a valid directory. Throw a FatalProtocolException otherwise.
        /// </summary>
        public static DirectoryInfo GetAndVerifyRootDirectory(string root)
        {
            // Check for package files one level deep.
            DirectoryInfo rootDirectoryInfo = null;

            try
            {
                // Verify that the directory is a valid path.
                rootDirectoryInfo = new DirectoryInfo(root);

                // The root must also be parsable as a URI (relative or absolute). This rejects
                // sources that have the weird "C:Source" format. For more information about this 
                // format, see:
                // https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx#paths
                new Uri(root, UriKind.RelativeOrAbsolute);
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is IOException ||
                                       ex is SecurityException ||
                                       ex is UriFormatException ||
                                       ex is NotSupportedException)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToRetrievePackage, root);

                throw new FatalProtocolException(message, ex);
            }

            return rootDirectoryInfo;
        }

        /// <summary>
        /// Verify that a path could be a valid file. Throw a FatalProtocolException otherwise.
        /// </summary>
        private static FileInfo GetAndVerifyFileInfo(Uri fileUri)
        {
            FileInfo fileInfo = null;

            try
            {
                // Verify that the file is a valid path
                fileInfo = new FileInfo(fileUri.LocalPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is SecurityException)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToRetrievePackage, fileUri.AbsoluteUri);

                throw new FatalProtocolException(message, ex);
            }

            return fileInfo;
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

            // Validate teh root path
            DirectoryInfo rootDirectoryInfo = GetAndVerifyRootDirectory(root);

            if (!rootDirectoryInfo.Exists)
            {
                // Directory is missing
                yield break;
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
            DirectoryInfo rootDirectoryInfo = GetAndVerifyRootDirectory(root);

            if (!rootDirectoryInfo.Exists)
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

        /// <summary>
        /// Find all nupkgs in the top level of a directory.
        /// </summary>
        private static FileInfo[] GetNupkgsFromDirectory(string root, ILogger log)
        {
            return GetFilesSafe(root, NupkgFilter, log);
        }
    }
}
