using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// This class represents a NuGetProject based on a .NET project. This also contains an instance of a FolderNuGetProject
    /// </summary>
    public class MSBuildNuGetProject : NuGetProject
    {
        private IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; set; }
        public FolderNuGetProject FolderNuGetProject { get; private set; }
        public MSBuildNuGetProject(IMSBuildNuGetProjectSystem msbuildNuGetProjectSystem, FolderNuGetProject folderNuGetProject)
        {
            if (msbuildNuGetProjectSystem == null)
            {
                throw new ArgumentNullException("nugetDotNetProjectSystem");
            }

            if (folderNuGetProject == null)
            {
                throw new ArgumentNullException("folderSystemNuGetProject");
            }

            MSBuildNuGetProjectSystem = msbuildNuGetProjectSystem;
            FolderNuGetProject = folderNuGetProject;
            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, MSBuildNuGetProjectSystem.TargetFramework);
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            throw new NotImplementedException();
        }

        public override bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream, INuGetProjectContext nuGetProjectContext)
        {
            // 1. FolderNuGetProject.InstallPackage(packageIdentity, packageStream);
            // 2. Update packages.config
            // 3. Call into DotNetNuGetProjectSystem
            throw new NotImplementedException();
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext)
        {
            throw new NotImplementedException();
        }
    }
}
