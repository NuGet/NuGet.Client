// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;

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
                var pathContext = new VersionFolderPathContext(
                    package: reader.GetIdentity(),
                    packagesDirectory: globalFolder,
                    logger: Common.NullLogger.Instance,
                    fixNuspecIdCasing: true,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    normalizeFileNames: true,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                using (var stream = File.OpenRead(packagePath))
                {
                    await PackageExtractor.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                                   pathContext,
                                                                   CancellationToken.None);
                }
            }
        }
    }
}
