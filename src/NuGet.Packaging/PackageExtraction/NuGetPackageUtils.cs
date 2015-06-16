// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Logging;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    public static class NuGetPackageUtils
    {
        private const string ManifestExtension = ".nuspec";

        public static async Task InstallFromSourceAsync(
            Func<Stream, Task> copyToAsync,
            PackageIdentity packageIdentity,
            string packagesDirectory,
            ILogger log,
            bool fixNuspecIdCasing,
            CancellationToken token)
        {
            var packagePathResolver = new VersionFolderPathResolver(packagesDirectory);

            var targetPath = packagePathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(packageIdentity.Id, packageIdentity.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(packageIdentity.Id, packageIdentity.Version);
            var hashPath = packagePathResolver.GetHashPath(packageIdentity.Id, packageIdentity.Version);

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkg,
                action: async cancellationToken =>
                {
                    // If this is the first process trying to install the target nupkg, go ahead
                    // After this process successfully installs the package, all other processes
                    // waiting on this lock don't need to install it again.
                    if (!File.Exists(targetNupkg))
                    {
                        log.LogInformation($"Installing {packageIdentity.Id} {packageIdentity.Version}");

                        Directory.CreateDirectory(targetPath);
                        using (var nupkgStream = new FileStream(
                            targetNupkg,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete,
                            bufferSize: 4096,
                            useAsync: true))
                        {
                            await copyToAsync(nupkgStream);
                            nupkgStream.Seek(0, SeekOrigin.Begin);

                            ExtractPackage(targetPath, nupkgStream);
                        }

                        if (fixNuspecIdCasing)
                        {
                            // DNU REFACTORING TODO: delete the hacky FixNuSpecIdCasing() and uncomment logic below after we
                            // have implementation of NuSpecFormatter.Read()
                            // Fixup the casing of the nuspec on disk to match what we expect
                            var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + ManifestExtension).Single();
                            FixNuSpecIdCasing(nuspecFile, targetNuspec, packageIdentity.Id);
                        }

                        using (var nupkgStream = File.Open(targetNupkg, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            string packageHash;
                            using (var sha512 = SHA512.Create())
                            {
                                packageHash = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
                            }

                            // Note: PackageRepository relies on the hash file being written out as the final operation as part of a package install
                            // to assume a package was fully installed.
                            File.WriteAllText(hashPath, packageHash);
                        }

                        log.LogVerbose($"Completed installation of {packageIdentity.Id} {packageIdentity.Version}");
                    }

                    return 0;
                },
                token: token);
        }

        // DNU REFACTORING TODO: delete this temporary workaround after we have NuSpecFormatter.Read()
        private static void FixNuSpecIdCasing(string nuspecFile, string targetNuspec, string correctedId)
        {
            var actualNuSpecName = Path.GetFileName(nuspecFile);
            var expectedNuSpecName = Path.GetFileName(targetNuspec);

            if (!string.Equals(actualNuSpecName, expectedNuSpecName, StringComparison.Ordinal))
            {
                var xDoc = XDocument.Parse(File.ReadAllText(nuspecFile),
                    LoadOptions.PreserveWhitespace);
                var metadataNode = xDoc.Root.Elements().Where(e => StringComparer.Ordinal.Equals(e.Name.LocalName, "metadata")).First();
                var node = metadataNode.Elements(XName.Get("id", metadataNode.GetDefaultNamespace().NamespaceName)).First();
                node.Value = correctedId;

                var tmpNuspecFile = nuspecFile + ".tmp";
                File.Move(nuspecFile, tmpNuspecFile);

                using (var stream = File.OpenWrite(targetNuspec))
                {
                    xDoc.Save(stream);
                }

                File.Delete(tmpNuspecFile);
            }
        }

        private static void ExtractPackage(string targetPath, FileStream stream)
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ExtractNupkg(archive, targetPath);
            }
        }

        private static void ExtractNupkg(ZipArchive archive, string targetPath)
        {
            ExtractFiles(
                archive,
                targetPath,
                shouldInclude: NupkgFilter);
        }

        private static bool NupkgFilter(string fullName)
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

            return true;
        }

        private static void ExtractFiles(ZipArchive archive, string targetPath, Func<string, bool> shouldInclude)
        {
            foreach (var entry in archive.Entries)
            {
                var entryFullName = entry.FullName;
                // An entry in a ZipArchive could start with a '/' based on how it is zipped
                // Remove it if present
                if (entryFullName.StartsWith("/", StringComparison.Ordinal))
                {
                    entryFullName = entryFullName.Substring(1);
                }

                // ZipArchive always has forward slashes in them. By replacing them with DirectorySeparatorChar;
                // in windows, we get the windows-style path
                entryFullName = Uri.UnescapeDataString(entryFullName.Replace('/', Path.DirectorySeparatorChar));

                var targetFile = Path.Combine(targetPath, entryFullName);
                if (!targetFile.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!shouldInclude(entryFullName))
                {
                    continue;
                }

                if (Path.GetFileName(targetFile).Length == 0)
                {
                    Directory.CreateDirectory(targetFile);
                }
                else
                {
                    var targetEntryPath = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetEntryPath))
                    {
                        Directory.CreateDirectory(targetEntryPath);
                    }

                    using (var entryStream = entry.Open())
                    {
                        using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            entryStream.CopyTo(targetStream);
                        }
                    }
                }
            }
        }
    }
}
