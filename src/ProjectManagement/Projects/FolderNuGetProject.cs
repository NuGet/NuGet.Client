using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZipFilePair = System.Tuple<string, System.IO.Compression.ZipArchiveEntry>;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// This class represents a NuGetProject based on a folder such as packages folder on a VisualStudio solution
    /// </summary>
    public class FolderNuGetProject : NuGetProject
    {
        public string Root { get; set; }
        private PackagePathResolver PackagePathResolver { get; set; }
        /// <summary>
        /// PackageSaveMode may be set externally for change in behavior
        /// </summary>
        public PackageSaveModes PackageSaveMode { get; set; }

        public FolderNuGetProject(string root)
        {
            if(root == null)
            {
                throw new ArgumentNullException("root");
            }
            Root = root;
            PackagePathResolver = new PackagePathResolver(root);
            PackageSaveMode = PackageSaveModes.Nupkg;
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, root);
            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, NuGetFramework.AnyFramework);
        }

        public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return Task.FromResult(Enumerable.Empty<PackageReference>());
        }

        public async override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, Stream packageStream,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if (packageStream == null)
            {
                throw new ArgumentNullException("packageStream");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            if (!packageStream.CanSeek)
            {
                throw new ArgumentException(Strings.PackageStreamShouldBeSeekable);
            }

            // 1. Check if the Package already exists at root, if so, return false
            if (PackageExists(packageIdentity))
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageAlreadyExistsInFolder, packageIdentity, Root);
                return false;
            }

            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddingPackageToFolder, packageIdentity, Root);
            // 2. Call PackageExtractor to extract the package into the root directory of this FileSystemNuGetProject
            packageStream.Seek(0, SeekOrigin.Begin);
            var addedPackageFilesList = new List<string>(await PackageExtractor.ExtractPackageAsync(packageStream, packageIdentity, PackagePathResolver, nuGetProjectContext.PackageExtractionContext,
                PackageSaveMode, token));

            if (PackageSaveMode.HasFlag(PackageSaveModes.Nupkg))
            {
                var packageFilePath = GetInstalledPackageFilePath(packageIdentity);
                if (File.Exists(packageFilePath))
                {
                    addedPackageFilesList.Add(packageFilePath);
                }
            }

            // Pend all the package files including the nupkg file
            FileSystemUtility.PendAddFiles(addedPackageFilesList, Root, nuGetProjectContext);

            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddedPackageToFolder, packageIdentity, Root);
            return true;
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
            return !String.IsNullOrEmpty(GetInstalledPackageFilePath(packageIdentity));
        }

        public async Task<bool> CopySatelliteFilesAsync(PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var copiedSatelliteFiles = await PackageExtractor.CopySatelliteFilesAsync(packageIdentity, PackagePathResolver, PackageSaveMode, token);
            FileSystemUtility.PendAddFiles(copiedSatelliteFiles, Root, nuGetProjectContext);

            return copiedSatelliteFiles.Any();
        }

        public string GetInstalledPackageFilePath(PackageIdentity packageIdentity)
        {
            return PackagePathResolver.GetInstalledPackageFilePath(packageIdentity) ?? String.Empty;
        }

        public string GetInstalledPath(PackageIdentity packageIdentity)
        {
            return PackagePathResolver.GetInstalledPath(packageIdentity) ?? String.Empty;
;        }

        public async Task<bool> DeletePackage(PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var packageFilePath = GetInstalledPackageFilePath(packageIdentity);
            if (File.Exists(packageFilePath))
            {
                string packageDirectoryPath = Path.GetDirectoryName(packageFilePath);
                using (var packageStream = File.OpenRead(packageFilePath))
                {
                    var installedSatelliteFilesPair = await PackageHelper.GetInstalledSatelliteFiles(packageStream, packageIdentity, PackagePathResolver, PackageSaveMode, token);
                    var runtimePackageDirectory = installedSatelliteFilesPair.Item1;
                    var installedSatelliteFiles = installedSatelliteFilesPair.Item2;
                    if (!String.IsNullOrEmpty(runtimePackageDirectory))
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
                    var installedPackageFiles = await PackageHelper.GetInstalledPackageFiles(packageStream, packageIdentity, PackagePathResolver, PackageSaveMode, token);

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
                if (!FileSystemUtility.GetFiles(Root, String.Empty, "*.*").Any() &&
                    !FileSystemUtility.GetDirectories(Root, String.Empty).Any())
                {
                    FileSystemUtility.DeleteDirectorySafe(Root, recursive: false, nuGetProjectContext: nuGetProjectContext);
                }
            }

            return true;
        }
    }
}
