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
        private string FullPath { get; set; }
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
            using(var stream = File.OpenWrite(FullPath))
            {
                var writer = new PackagesConfigWriter(stream);
                writer.WritePackageEntry(packageIdentity, TargetFramework);
                writer.Close();
            }
            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddedPackageToPackagesConfig, packageIdentity);
            return true;
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            if (File.Exists(FullPath))
            {
                var reader = new PackagesConfigReader(XDocument.Load(FullPath));
                return reader.GetPackages();
            }

            return Enumerable.Empty<PackageReference>();
        }
    }
}
