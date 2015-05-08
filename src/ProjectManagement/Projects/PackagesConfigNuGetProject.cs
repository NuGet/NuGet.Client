using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Represents a NuGet project as represented by packages.config
    /// </summary>
    public class PackagesConfigNuGetProject : NuGetProject
    {
        public string FullPath
        {
            get
            {
                if (UsingPackagesProjectNameConfigPath)
                {
                    return PackagesProjectNameConfigPath;
                }
                else
                {
                    return PackagesConfigPath;
                }
            }
        }

        private bool UsingPackagesProjectNameConfigPath { get; set; }

        /// <summary>
        /// Represents the full path to "packages.config"
        /// </summary>
        private string PackagesConfigPath { get; }

        /// <summary>
        /// Represents the full path to "packages.'projectName'.config"
        /// </summary>
        private string PackagesProjectNameConfigPath { get; }
        private NuGetFramework TargetFramework { get; }

        public PackagesConfigNuGetProject(string folderPath, IDictionary<string, object> metadata) : base(metadata)
        {
            if (folderPath == null)
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

            TargetFramework = GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);

            PackagesConfigPath = Path.Combine(folderPath, "packages.config");

            var projectName = GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            PackagesProjectNameConfigPath = Path.Combine(folderPath, "packages." + projectName + ".config");
        }

        public override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, Stream packageStream,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            var newPackageReference = new PackageReference(packageIdentity, TargetFramework);
            List<PackageReference> installedPackagesList = GetInstalledPackagesList();
            var packageReferenceWithSameId = installedPackagesList.Where(p => p.PackageIdentity.Id.Equals(packageIdentity.Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (packageReferenceWithSameId != null)
            {
                if (packageReferenceWithSameId.PackageIdentity.Equals(packageIdentity))
                {
                    nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageAlreadyExistsInPackagesConfig, packageIdentity, Path.GetFileName(FullPath));
                    return Task.FromResult(false);
                }
                else
                {
                    // Higher version of an installed package is being installed. Remove old and add new
                    installedPackagesList.Remove(packageReferenceWithSameId);
                    installedPackagesList.Add(newPackageReference);
                }
            }
            else
            {
                installedPackagesList.Add(newPackageReference);
            }

            // Create new file or overwrite existing file
            using (var stream = FileSystemUtility.CreateFile(FullPath, nuGetProjectContext))
            {
                var writer = new PackagesConfigWriter(stream);
                foreach (var pr in installedPackagesList)
                {
                    writer.WritePackageEntry(pr);
                }
                writer.Close();
            }
            nuGetProjectContext.Log(MessageLevel.Info, Strings.AddedPackageToPackagesConfig, packageIdentity, Path.GetFileName(FullPath));
            return Task.FromResult(true);
        }

        public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
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
            if (packageReference == null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageDoesNotExisttInPackagesConfig, packageIdentity, Path.GetFileName(FullPath));
                return Task.FromResult(false);
            }

            installedPackagesList.Remove(packageReference);
            if (installedPackagesList.Count > 0)
            {
                // Create new file or overwrite existing file
                using (var stream = FileSystemUtility.CreateFile(FullPath, nuGetProjectContext))
                {
                    var writer = new PackagesConfigWriter(stream);
                    foreach (var pr in installedPackagesList)
                    {
                        writer.WritePackageEntry(pr);
                    }
                    writer.Close();
                }
            }
            else
            {
                FileSystemUtility.DeleteFile(FullPath, nuGetProjectContext);
            }
            nuGetProjectContext.Log(MessageLevel.Info, Strings.RemovedPackageFromPackagesConfig, packageIdentity, Path.GetFileName(FullPath));
            return Task.FromResult(true);
        }

        public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return Task.FromResult<IEnumerable<PackageReference>>(GetInstalledPackagesList());
        }

        private void UpdateFullPath()
        {
            if (UsingPackagesProjectNameConfigPath && !File.Exists(PackagesProjectNameConfigPath) && File.Exists(PackagesConfigPath))
            {
                UsingPackagesProjectNameConfigPath = false;
                return;
            }
            else if (!File.Exists(PackagesConfigPath) && File.Exists(PackagesProjectNameConfigPath))
            {
                UsingPackagesProjectNameConfigPath = true;
                return;
            }
        }

        private List<PackageReference> GetInstalledPackagesList()
        {
            UpdateFullPath();
            if (File.Exists(FullPath))
            {
                var reader = new PackagesConfigReader(XDocument.Load(FullPath));
                return reader.GetPackages().ToList();
            }

            return new List<PackageReference>();
        }
    }
}
