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
    /// This class represents a NuGetProject based on a .NET project. This also contains an instance of a FileSystemNuGetProject
    /// </summary>
    public class MSBuildNuGetProject : NuGetProject
    {
        private IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; set; }
        public FolderNuGetProject FolderNuGetProject { get; private set; }
        public MSBuildNuGetProject(IMSBuildNuGetProjectSystem msbuildNuGetProjectSystem, FolderNuGetProject fileSystemNuGetProject)
        {
            if (msbuildNuGetProjectSystem == null)
            {
                throw new ArgumentNullException("nugetDotNetProjectSystem");
            }

            if (fileSystemNuGetProject == null)
            {
                throw new ArgumentNullException("fileSystemNuGetProject");
            }

            MSBuildNuGetProjectSystem = msbuildNuGetProjectSystem;
            FolderNuGetProject = fileSystemNuGetProject;
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            throw new NotImplementedException();
        }

        public override NuGetFramework TargetFramework
        {
            get
            {
                return MSBuildNuGetProjectSystem.TargetFramework;
            }
        }

        public override bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream, IExecutionContext executionContext)
        {
            // 1. FileSystemNuGetProject.InstallPackage(packageIdentity, packageStream);
            // 2. Update packages.config
            // 3. Call into DotNetNuGetProjectSystem
            throw new NotImplementedException();
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity, IExecutionContext executionContext)
        {
            throw new NotImplementedException();
        }
    }
}
