using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// This class represents a NuGetProject based on a folder such as packages folder on a VisualStudio solution
    /// </summary>
    public class FolderNuGetProject : NuGetProject
    {
        public string Root { get; set; }
        public PackagePathResolver PackagePathResolver { get; private set; }
        /// <summary>
        /// PackageSaveMode may be set externally for change in behavior
        /// </summary>
        public PackageSaveModes PackageSaveMode { get; set; }

        // TODO: Once PackageExtractor supports handling of satellite files, there will another enum here
        //       which can be set to control what happens during package extraction

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
            await PackageExtractor.ExtractPackageAsync(packageStream, packageIdentity, PackagePathResolver, nuGetProjectContext.PackageExtractionContext,
                PackageSaveMode, token);

            // Source control stuff
            var sourceControlManager = SourceControlUtility.GetSourceControlManager(nuGetProjectContext);
            if(sourceControlManager != null && sourceControlManager.IsPackagesFolderBoundToSourceControl())
            {
                var packageInstallPath = PackagePathResolver.GetInstallPath(packageIdentity);
                sourceControlManager.AddFilesUnderDirectory(packageInstallPath);
            }

            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddedPackageToFolder, packageIdentity, Root);
            return true;
        }

        public async override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            await PackageExtractor.RemoveSatelliteFilesAsync(packageIdentity, PackagePathResolver, PackageSaveMode, token);
            return true;
        }

        /// <summary>
        /// A package is considered to exist in FileSystemNuGetProject, if the 'nupkg' file is present where expected
        /// </summary>
        public bool PackageExists(PackageIdentity packageIdentity)
        {
            string packageFileFullPath = Path.Combine(PackagePathResolver.GetInstallPath(packageIdentity),
                PackagePathResolver.GetPackageFileName(packageIdentity));
            return File.Exists(packageFileFullPath);
        }

        public async Task<bool> CopySatelliteFilesAsync(PackageIdentity packageIdentity,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return await PackageExtractor.CopySatelliteFilesAsync(packageIdentity, PackagePathResolver, PackageSaveMode, token);
        }

        public string GetPackagePath(PackageIdentity packageIdentity)
        {
            return Path.Combine(PackagePathResolver.GetInstallPath(packageIdentity),
                PackagePathResolver.GetPackageFileName(packageIdentity));
        }
    }
}
