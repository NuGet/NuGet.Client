// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Protocol.Core.Types;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// This class represents a NuGetProject based on a folder such as packages folder on a VisualStudio solution
    /// </summary>
    public class FolderNuGetProject : NuGetProject
    {
        public string Root { get; set; }
        private PackagePathResolver PackagePathResolver { get; }

        public FolderNuGetProject(string root)
            : this(root, new PackagePathResolver(root))
        {
        }

        public FolderNuGetProject(string root, bool excludeVersion)
            : this(root, new PackagePathResolver(root, !excludeVersion))
        {
        }

        public FolderNuGetProject(string root, PackagePathResolver packagePathResolver)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }
            Root = root;
            PackagePathResolver = packagePathResolver;
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, root);
            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, NuGetFramework.AnyFramework);
        }

        public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<PackageReference>());
        }

        public override Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (downloadResourceResult == null)
            {
                throw new ArgumentNullException(nameof(downloadResourceResult));
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException(nameof(nuGetProjectContext));
            }

            if (!downloadResourceResult.PackageStream.CanSeek)
            {
                throw new ArgumentException(Strings.PackageStreamShouldBeSeekable);
            }
            var packageFile = PackagePathResolver.GetInstallPath(packageIdentity);

            return ConcurrencyUtilities.ExecuteWithFileLockedAsync(packageFile,
                action: cancellationToken =>
                {
                    // 1. Check if the Package already exists at root, if so, return false
                    if (PackageExists(packageIdentity))
                    {
                        nuGetProjectContext.Log(MessageLevel.Info, Strings.PackageAlreadyExistsInFolder, packageIdentity, Root);
                        return Task.FromResult(false);
                    }

                    nuGetProjectContext.Log(MessageLevel.Info, Strings.AddingPackageToFolder, packageIdentity, Path.GetFullPath(Root));
                    // 2. Call PackageExtractor to extract the package into the root directory of this FileSystemNuGetProject
                    downloadResourceResult.PackageStream.Seek(0, SeekOrigin.Begin);
                    var addedPackageFilesList = new List<string>();

                    PackageExtractionContext packageExtractionContext = nuGetProjectContext.PackageExtractionContext;
                    if (packageExtractionContext == null)
                    {
                        packageExtractionContext = new PackageExtractionContext(new LoggerAdapter(nuGetProjectContext));
                    }

                    if (downloadResourceResult.PackageReader != null)
                    {
                        addedPackageFilesList.AddRange(
                            PackageExtractor.ExtractPackage(
                                downloadResourceResult.PackageReader,
                                downloadResourceResult.PackageStream,
                                PackagePathResolver,
                                packageExtractionContext,
                                cancellationToken));
                    }
                    else
                    {
                        addedPackageFilesList.AddRange(
                            PackageExtractor.ExtractPackage(
                                downloadResourceResult.PackageStream,
                                PackagePathResolver,
                                packageExtractionContext,
                                cancellationToken));
                    }


                    var packageSaveMode = GetPackageSaveMode(nuGetProjectContext);
                    if (packageSaveMode.HasFlag(PackageSaveMode.Nupkg))
                    {
                        var packageFilePath = GetInstalledPackageFilePath(packageIdentity);
                        if (File.Exists(packageFilePath))
                        {
                            addedPackageFilesList.Add(packageFilePath);
                        }
                    }

                    // Pend all the package files including the nupkg file
                    FileSystemUtility.PendAddFiles(addedPackageFilesList, Root, nuGetProjectContext);

                    nuGetProjectContext.Log(MessageLevel.Info, Strings.AddedPackageToFolder, packageIdentity, Path.GetFullPath(Root));
                    return Task.FromResult(true);
                },
                token: token);
        }

        public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            // Do nothing. Return true
            return Task.FromResult(true);
        }

        /// <summary>
        /// A package is considered to exist in FileSystemNuGetProject, if the 'nupkg' file is present where expected
        /// </summary>
        public bool PackageExists(PackageIdentity packageIdentity)
        {
            return !string.IsNullOrEmpty(GetInstalledPackageFilePath(packageIdentity));
        }

        public Task<bool> CopySatelliteFilesAsync(PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            PackageExtractionContext packageExtractionContext = nuGetProjectContext.PackageExtractionContext;
            if (packageExtractionContext == null)
            {
                packageExtractionContext = new PackageExtractionContext(new LoggerAdapter(nuGetProjectContext));
            }

            var copiedSatelliteFiles = PackageExtractor.CopySatelliteFiles(
                packageIdentity,
                PackagePathResolver,
                GetPackageSaveMode(nuGetProjectContext),
                packageExtractionContext,
                token);

            FileSystemUtility.PendAddFiles(copiedSatelliteFiles, Root, nuGetProjectContext);

            return Task.FromResult(copiedSatelliteFiles.Any());
        }

        /// <summary>
        /// Get the path to the package nupkg.
        /// </summary>
        public string GetInstalledPackageFilePath(PackageIdentity packageIdentity)
        {
            // Check the expected location before searching all directories
            var packageDirectory = PackagePathResolver.GetInstallPath(packageIdentity);
            var packageName = PackagePathResolver.GetPackageFileName(packageIdentity);

            var installPath = Path.GetFullPath(Path.Combine(packageDirectory, packageName));

            if (File.Exists(installPath))
            {
                return installPath;
            }

            // Fallback to the v2 directory search
            installPath = PackagePathResolver.GetInstalledPackageFilePath(packageIdentity);

            if (!string.IsNullOrEmpty(installPath))
            {
                return installPath;
            }

            // Default to empty
            return string.Empty;
        }

        /// <summary>
        /// Get the root directory of an installed package.
        /// </summary>
        public string GetInstalledPath(PackageIdentity packageIdentity)
        {
            var installFilePath = GetInstalledPackageFilePath(packageIdentity);

            if (!string.IsNullOrEmpty(installFilePath))
            {
                return Path.GetDirectoryName(installFilePath);
            }

            // Default to empty
            return string.Empty;
        }

        public Task<bool> DeletePackage(PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var packageFilePath = GetInstalledPackageFilePath(packageIdentity);
            if (File.Exists(packageFilePath))
            {
                var packageDirectoryPath = Path.GetDirectoryName(packageFilePath);
                using (var packageReader = new PackageArchiveReader(packageFilePath))
                {
                    var installedSatelliteFilesPair = PackageHelper.GetInstalledSatelliteFiles(
                        packageReader,
                        PackagePathResolver,
                        GetPackageSaveMode(nuGetProjectContext));
                    var runtimePackageDirectory = installedSatelliteFilesPair.Item1;
                    var installedSatelliteFiles = installedSatelliteFilesPair.Item2;
                    if (!string.IsNullOrEmpty(runtimePackageDirectory))
                    {
                        try
                        {
                            // Delete all the package files now
                            FileSystemUtility.DeleteFiles(installedSatelliteFiles, runtimePackageDirectory, nuGetProjectContext);
                        }
                        catch (Exception ex)
                        {
                            nuGetProjectContext.Log(MessageLevel.Warning, ex.Message);
                            // Catch all exception with delete so that the package file is always deleted
                        }
                    }

                    // Get all the package files before deleting the package file
                    var installedPackageFiles = PackageHelper.GetInstalledPackageFiles(
                        packageReader,
                        packageIdentity,
                        PackagePathResolver,
                        GetPackageSaveMode(nuGetProjectContext));

                    try
                    {
                        // Delete all the package files now
                        FileSystemUtility.DeleteFiles(installedPackageFiles, packageDirectoryPath, nuGetProjectContext);
                    }
                    catch (Exception ex)
                    {
                        nuGetProjectContext.Log(MessageLevel.Warning, ex.Message);
                        // Catch all exception with delete so that the package file is always deleted
                    }
                }

                // Delete the package file
                FileSystemUtility.DeleteFile(packageFilePath, nuGetProjectContext);

                // Delete the package directory if any
                FileSystemUtility.DeleteDirectorySafe(packageDirectoryPath, recursive: true, nuGetProjectContext: nuGetProjectContext);

                // If this is the last package delete the package directory
                // If this is the last package delete the package directory
                if (!FileSystemUtility.GetFiles(Root, string.Empty, "*.*").Any()
                    && !FileSystemUtility.GetDirectories(Root, string.Empty).Any())
                {
                    FileSystemUtility.DeleteDirectorySafe(Root, recursive: false, nuGetProjectContext: nuGetProjectContext);
                }
            }

            return Task.FromResult(true);
        }

        private PackageSaveMode GetPackageSaveMode(INuGetProjectContext nuGetProjectContext)
        {
            return nuGetProjectContext.PackageExtractionContext?.PackageSaveMode ?? PackageSaveMode.Defaultv2;
        }
    }
}
