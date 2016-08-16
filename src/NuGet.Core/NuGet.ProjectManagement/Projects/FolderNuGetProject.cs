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
using NuGet.Protocol;
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

            var packageDirectory = PackagePathResolver.GetInstallPath(packageIdentity);

            return ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                packageDirectory,
                action: cancellationToken =>
                {
                    // 1. Set a default package extraction context, if necessary.
                    PackageExtractionContext packageExtractionContext = nuGetProjectContext.PackageExtractionContext;
                    if (packageExtractionContext == null)
                    {
                        packageExtractionContext = new PackageExtractionContext(new LoggerAdapter(nuGetProjectContext));
                    }

                    // 2. Check if the Package already exists at root, if so, return false
                    if (PackageExists(packageIdentity, packageExtractionContext.PackageSaveMode))
                    {
                        nuGetProjectContext.Log(MessageLevel.Info, Strings.PackageAlreadyExistsInFolder, packageIdentity, Root);
                        return Task.FromResult(false);
                    }

                    nuGetProjectContext.Log(MessageLevel.Info, Strings.AddingPackageToFolder, packageIdentity, Path.GetFullPath(Root));

                    // 3. Call PackageExtractor to extract the package into the root directory of this FileSystemNuGetProject
                    downloadResourceResult.PackageStream.Seek(0, SeekOrigin.Begin);
                    var addedPackageFilesList = new List<string>();
                    
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

                    // Extra logging with source for verbosity detailed
                    // Used by external tool CoreXT to track package provenance
                    if (!string.IsNullOrEmpty(downloadResourceResult.PackageSource))
                    {
                        nuGetProjectContext.Log(MessageLevel.Debug, Strings.AddedPackageToFolderFromSource, packageIdentity, Path.GetFullPath(Root), downloadResourceResult.PackageSource);
                    }

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
        /// A package is considered to exist in FileSystemNuGetProject, if the .nupkg file is present where expected
        /// </summary>
        public bool PackageExists(PackageIdentity packageIdentity)
        {
            return PackageExists(packageIdentity, PackageSaveMode.Nupkg);
        }

        public bool PackageExists(PackageIdentity packageIdentity, PackageSaveMode packageSaveMode)
        {
            var packageExists = !string.IsNullOrEmpty(GetInstalledPackageFilePath(packageIdentity));
            var manifestExists = !string.IsNullOrEmpty(GetInstalledManifestFilePath(packageIdentity));

            // A package must have either a nupkg or a nuspec to be valid
            var result = packageExists || manifestExists;
            
            // Verify nupkg present if specified
            if ((packageSaveMode & PackageSaveMode.Nupkg) == PackageSaveMode.Nupkg)
            {
                result &= packageExists;
            }
            
            // Verify nuspec present if specified
            if ((packageSaveMode & PackageSaveMode.Nuspec) == PackageSaveMode.Nuspec)
            {
                result &= manifestExists;
            }

            return result;
        }

        public bool ManifestExists(PackageIdentity packageIdentity)
        {
            return !string.IsNullOrEmpty(GetInstalledManifestFilePath(packageIdentity));
        }

        public bool PackageAndManifestExists(PackageIdentity packageIdentity)
        {
            return !string.IsNullOrEmpty(GetInstalledPackageFilePath(packageIdentity)) && !string.IsNullOrEmpty(GetInstalledManifestFilePath(packageIdentity));
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

            // Keep the previous optimization of just going by the existance of the file if we find it.
            if (File.Exists(installPath))
            {
                return installPath;
            }

            // If the file was not found check for non-normalized paths and verify the id/version
            LocalPackageInfo package = null;

            if (PackagePathResolver.UseSideBySidePaths)
            {
                // Search for a folder with the id and version
                package = LocalFolderUtility.GetPackagesConfigFolderPackage(
                    Root,
                    packageIdentity,
                    NullLogger.Instance);
            }
            else
            {
                // Search for just the id
                package = LocalFolderUtility.GetPackageV2(
                    Root,
                    packageIdentity,
                    NullLogger.Instance);
            }

            if (package != null && packageIdentity.Equals(package.Identity))
            {
                return package.Path;
            }

            // Default to empty
            return string.Empty;
        }

        /// <summary>
        /// Get the path to the package nuspec.
        /// </summary>
        public string GetInstalledManifestFilePath(PackageIdentity packageIdentity)
        {
            // Check the expected location before searching all directories
            var packageDirectory = PackagePathResolver.GetInstallPath(packageIdentity);
            var manifestName = PackagePathResolver.GetManifestFileName(packageIdentity);

            var installPath = Path.GetFullPath(Path.Combine(packageDirectory, manifestName));

            // Keep the previous optimization of just going by the existance of the file if we find it.
            if (File.Exists(installPath))
            {
                return installPath;
            }

            // Don't look in non-normalized paths for nuspec
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
