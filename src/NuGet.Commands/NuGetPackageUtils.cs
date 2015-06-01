// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.Packaging;

namespace NuGet.Commands
{
    internal static class NuGetPackageUtils
    {
        private const string ManifestExtension = ".nuspec";

        internal static async Task InstallFromStream(
            Stream stream,
            LibraryIdentity library,
            string packagesDirectory,
            ILogger log)
        {
            var packagePathResolver = new DefaultPackagePathResolver(packagesDirectory);

            var targetPath = packagePathResolver.GetInstallPath(library.Name, library.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(library.Name, library.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(library.Name, library.Version);
            var hashPath = packagePathResolver.GetHashPath(library.Name, library.Version);

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkg, async createdNewLock =>
                {
                    // If this is the first process trying to install the target nupkg, go ahead
                    // After this process successfully installs the package, all other processes
                    // waiting on this lock don't need to install it again.
                    if (createdNewLock && !File.Exists(targetNupkg))
                    {
                        log.LogInformation(Strings.FormatLog_InstallingPackage(library.Name, library.Version));

                        Directory.CreateDirectory(targetPath);
                        using (var nupkgStream = new FileStream(
                            targetNupkg,
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.ReadWrite | FileShare.Delete,
                            bufferSize: 4096,
                            useAsync: true))
                        {
                            await stream.CopyToAsync(nupkgStream);
                            nupkgStream.Seek(0, SeekOrigin.Begin);

                            ExtractPackage(targetPath, nupkgStream);
                        }

                        // DNU REFACTORING TODO: delete the hacky FixNuSpecIdCasing() and uncomment logic below after we
                        // have implementation of NuSpecFormatter.Read()
                        // Fixup the casing of the nuspec on disk to match what we expect
                        var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + ManifestExtension).Single();
                        FixNuSpecIdCasing(nuspecFile, targetNuspec, library.Name);

                        stream.Seek(0, SeekOrigin.Begin);
                        string packageHash;
                        using (var sha512 = SHA512.Create())
                        {
                            packageHash = Convert.ToBase64String(sha512.ComputeHash(stream));
                        }

                        // Note: PackageRepository relies on the hash file being written out as the final operation as part of a package install
                        // to assume a package was fully installed.
                        File.WriteAllText(hashPath, packageHash);
                    }

                    return 0;
                });
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

                File.Delete(nuspecFile);

                using (var stream = File.OpenWrite(targetNuspec))
                {
                    xDoc.Save(stream);
                }
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

        public static void ExtractFiles(ZipArchive archive, string targetPath, Func<string, bool> shouldInclude)
        {
            foreach (var entry in archive.Entries)
            {
                var entryFullName = entry.FullName;
                if (entryFullName.StartsWith("/", StringComparison.Ordinal))
                {
                    entryFullName = entryFullName.Substring(1);
                }
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
