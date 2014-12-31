using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace NuGet.ProjectManagement.DotNet
{
    /// <summary>
    /// This class represents a NuGetProject based on a .NET project. This also contains an instance of a FileSystemNuGetProject
    /// </summary>
    public class DotNetNuGetProject : NuGetProject
    {
        private DotNetNuGetProjectSystem DotNetNuGetProjectSystem { get; set; }
        public FileSystemNuGetProject FileSystemNuGetProject { get; private set; }
        public DotNetNuGetProject(DotNetNuGetProjectSystem nugetDotNetProjectSystem, FileSystemNuGetProject fileSystemNuGetProject)
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
            FileSystemNuGetProject = fileSystemNuGetProject;
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
            throw new NotImplementedException();
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity)
        {
            throw new NotImplementedException();
        }
    }
}
