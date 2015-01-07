using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// This class represents a NuGetProject based on a folder such as packages folder on a VisualStudio solution
    /// </summary>
    public class FolderNuGetProject : NuGetProject
    {
        private string Root { get; set; }
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
            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, NuGetFramework.AnyFramework);
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            return Enumerable.Empty<PackageReference>();
        }

        public override bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream, INuGetProjectContext nuGetProjectContext)
        {
            // 1. Check if the Package already exists at root, if so, return false
            if (PackageExistsInProject(packageIdentity))
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageAlreadyExistsInFolder, packageIdentity);
                return false;
            }

            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddingPackageToFolder, packageIdentity);
            // 2. Call PackageExtractor to extract the package into the root directory of this FileSystemNuGetProject
            PackageExtractor.ExtractPackage(packageStream, packageIdentity, PackagePathResolver, PackageSaveMode);
            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddedPackageToFolder, packageIdentity);
            return true;
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext)
        {
            // TODO: Handle removing of satellite files from the runtime package also

            // 1. Check if the Package exists at root, if not, return false
            if (!PackageExistsInProject(packageIdentity))
            {
                return false;
            }

            // 2. Delete the package folder and files from the root directory of this FileSystemNuGetProject
            // Remember that the following code may throw System.UnauthorizedAccessException
            Directory.Delete(PackagePathResolver.GetInstallPath(packageIdentity), recursive: true);
            return true;
        }

        /// <summary>
        /// A package is considered to exist in FileSystemNuGetProject, if the 'nupkg' file is present where expected
        /// </summary>
        private bool PackageExistsInProject(PackageIdentity packageIdentity)
        {
            string packageFileFullPath = Path.Combine(PackagePathResolver.GetInstallPath(packageIdentity), PackagePathResolver.GetPackageFileName(packageIdentity));
            return File.Exists(packageFileFullPath);
        }
    }
}
