// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace NuGet.Test.Utility
{
    public static class GlobalFolderUtility
    {
        /// <summary>
        /// Add a nupkg to the global package folder.
        /// </summary>
        public static Task AddPackageToGlobalFolderAsync(FileInfo packagePath, DirectoryInfo globalFolder)
        {
            return AddPackageToGlobalFolderAsync(packagePath.FullName, globalFolder.FullName);
        }

        /// <summary>
        /// Add a nupkg to the global package folder.
        /// </summary>
        public static async Task AddPackageToGlobalFolderAsync(string packagePath, string globalFolder)
        {
            using (var reader = new PackageArchiveReader(packagePath))
            {
                var pathContext = new PackageExtractionContext(
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                    logger: Common.NullLogger.Instance,
                    signedPackageVerifier: null);

                var versionFolderPathResolver = new VersionFolderPathResolver(globalFolder);

                using (var stream = File.OpenRead(packagePath))
                {
                    await PackageExtractor.InstallFromSourceAsync(reader.GetIdentity(),
                        async (d) => await stream.CopyToAsync(d),
                        versionFolderPathResolver,
                        pathContext,
                        packageSignatureVerified: true,
                        requiredRepoSign: false,
                        RepositoryCertificateInfos: null,
                        token: CancellationToken.None);
                }
            }
        }
    }
}
