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
    public class DotNetNuGetProject : NuGetProject
    {
        private IDotNetNuGetProjectSystem DotNetNuGetProjectSystem { get; set; }
        public SimpleNuGetProject SimpleNuGetProject { get; private set; }
        public DotNetNuGetProject(IDotNetNuGetProjectSystem nugetDotNetProjectSystem, SimpleNuGetProject fileSystemNuGetProject)
        {
            if (nugetDotNetProjectSystem == null)
            {
                throw new ArgumentNullException("nugetDotNetProjectSystem");
            }

            if (fileSystemNuGetProject == null)
            {
                throw new ArgumentNullException("fileSystemNuGetProject");
            }

            DotNetNuGetProjectSystem = nugetDotNetProjectSystem;
            SimpleNuGetProject = fileSystemNuGetProject;
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            throw new NotImplementedException();
        }

        public override NuGetFramework TargetFramework
        {
            get
            {
                return DotNetNuGetProjectSystem.TargetFramework;
            }
        }        

        public override bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream)
        {
            // 1. FileSystemNuGetProject.InstallPackage(packageIdentity, packageStream);
            // 2. Update packages.config
            // 3. Call into DotNetNuGetProjectSystem
            throw new NotImplementedException();
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity)
        {
            throw new NotImplementedException();
        }
    }
}
