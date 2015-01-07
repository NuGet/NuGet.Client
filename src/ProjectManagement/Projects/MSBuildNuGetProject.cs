using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// This class represents a NuGetProject based on a .NET project. This also contains an instance of a FolderNuGetProject
    /// </summary>
    public class MSBuildNuGetProject : NuGetProject
    {
        private IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; set; }
        public FolderNuGetProject FolderNuGetProject { get; private set; }
        public PackagesConfigNuGetProject PackagesConfigNuGetProject { get; private set; }
        public MSBuildNuGetProject(IMSBuildNuGetProjectSystem msbuildNuGetProjectSystem, string folderNuGetProjectPath, string packagesConfigFullPath)
        {
            if (msbuildNuGetProjectSystem == null)
            {
                throw new ArgumentNullException("nugetDotNetProjectSystem");
            }

            if (folderNuGetProjectPath == null)
            {
                throw new ArgumentNullException("folderNuGetProjectPath");
            }

            if (packagesConfigFullPath == null)
            {
                throw new ArgumentNullException("packagesConfigFullPath");
            }

            MSBuildNuGetProjectSystem = msbuildNuGetProjectSystem;
            FolderNuGetProject = new FolderNuGetProject(folderNuGetProjectPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, MSBuildNuGetProjectSystem.TargetFramework);
            PackagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, InternalMetadata);
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            return PackagesConfigNuGetProject.GetInstalledPackages();
        }

        public override bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream, INuGetProjectContext nuGetProjectContext)
        {
            // Step-1: Create PackageReader using the PackageStream and obtain the various item groups
            PackageReader packageReader = new PackageReader(new ZipArchive(packageStream));
            IEnumerable<FrameworkSpecificGroup> libItemGroups = packageReader.GetLibItems();

            // Step-2: Get the most compatible items groups for all items groups
            bool hasCompatibleItems = false;
            FrameworkSpecificGroup mostCompatibleLibItemGroup = GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, libItemGroups);
            hasCompatibleItems = (mostCompatibleLibItemGroup != null);

            // Step-3: Check if there are any compatible items in the package. If not, throw
            if(!hasCompatibleItems)
            {
                throw new InvalidOperationException(
                           String.Format(CultureInfo.CurrentCulture,
                           Strings.UnableToFindCompatibleItems, packageIdentity, MSBuildNuGetProjectSystem.TargetFramework));
            }

            // Step-4: FolderNuGetProject.InstallPackage(packageIdentity, packageStream);
            FolderNuGetProject.InstallPackage(packageIdentity, packageStream, nuGetProjectContext);

            // Step-5: Update packages.config
            PackagesConfigNuGetProject.InstallPackage(packageIdentity, packageStream, nuGetProjectContext);

            // Step-6: MSBuildNuGetProjectSystem operations
            // Step-6.1: Add references to project
            foreach(var libItem in mostCompatibleLibItemGroup.Items)
            {
                var libItemFullPath = Path.Combine(FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity), libItem);
                MSBuildNuGetProjectSystem.AddReference(libItemFullPath);
            }

            return true;
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext)
        {
            throw new NotImplementedException();
        }

        protected static FrameworkSpecificGroup GetMostCompatibleGroup(NuGetFramework projectTargetFramework, IEnumerable<FrameworkSpecificGroup> itemGroups)
        {
            FrameworkReducer reducer = new FrameworkReducer();
            NuGetFramework mostCompatibleFramework = reducer.GetNearest(projectTargetFramework, itemGroups.Select(i => NuGetFramework.Parse(i.TargetFramework)));
            if(mostCompatibleFramework != null)
            {
                IEnumerable<FrameworkSpecificGroup> mostCompatibleGroups = itemGroups.Where(i => NuGetFramework.Parse(i.TargetFramework).Equals(mostCompatibleFramework));
                return mostCompatibleGroups.FirstOrDefault();
            }
            return null;
        }
    }
}
