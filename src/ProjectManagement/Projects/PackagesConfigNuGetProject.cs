using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Represents a NuGet project as represented by packages.config
    /// </summary>
    public class PackagesConfigNuGetProject : NuGetProject
    {
        public string FullPath { get; set; }
        private NuGetFramework TargetFramework { get; set; }

        internal PackagesConfigNuGetProject(string fullPath, NuGetFramework targetFramework)
            : this(fullPath, new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework },
            }) { }

        public PackagesConfigNuGetProject(string fullPath, IDictionary<string, object> metadata) : base(metadata)
        {
            if(fullPath == null)
            {
                throw new ArgumentNullException("fullPath");
            }
            FullPath = fullPath;
            TargetFramework = GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);
        }

        public override bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream, INuGetProjectContext nuGetProjectContext)
        {            
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            List<PackageReference> installedPackagesList = GetInstalledPackagesList();
            var packageReference = installedPackagesList.Where(p => p.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if(packageReference != null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageAlreadyExistsInPackagesConfig, packageIdentity);
                return false;
            }

            installedPackagesList.Add(new PackageReference(packageIdentity, TargetFramework));
            using (var stream = File.OpenWrite(FullPath))
            {
                var writer = new PackagesConfigWriter(stream);
                foreach (var pr in installedPackagesList)
                {
                    writer.WritePackageEntry(pr);
                }
                writer.Close();
            }
            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddedPackageToPackagesConfig, packageIdentity);
            return true;
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            List<PackageReference> installedPackagesList = GetInstalledPackagesList();
            var packageReference = installedPackagesList.Where(p => p.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if(packageReference == null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageDoesNotExisttInPackagesConfig, packageIdentity);
                return false;
            }

            installedPackagesList.Remove(packageReference);
            using (var stream = File.OpenWrite(FullPath))
            {
                var writer = new PackagesConfigWriter(stream);
                foreach (var pr in installedPackagesList)
                {
                    writer.WritePackageEntry(pr);
                }
                writer.Close();
            }
            nuGetProjectContext.Log(MessageLevel.Info, Strings.RemovedPackageFromPackagesConfig, packageIdentity);
            return true;
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            return GetInstalledPackagesList();
        }

        private List<PackageReference> GetInstalledPackagesList()
        {
            if (File.Exists(FullPath))
            {
                var reader = new PackagesConfigReader(XDocument.Load(FullPath));
                return reader.GetPackages().ToList();
            }

            return new List<PackageReference>();
        }
    }
}
