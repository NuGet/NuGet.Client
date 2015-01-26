using NuGet.Client.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    internal class PowerShellPackage
    {
        public string Id { get; set; }

        public List<NuGetVersion> Version { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Get the view of PowerShellPackage. Used for Get-Package -ListAvailable command. 
        /// </summary>
        /// <param name="metadata">list of PSSearchMetadata</param>
        /// <param name="versionType"></param>
        /// <returns></returns>
        internal static List<PowerShellPackage> GetPowerShellPackageView(IEnumerable<PSSearchMetadata> metadata, VersionType versionType)
        {
            List<PowerShellPackage> view = new List<PowerShellPackage>();
            foreach (PSSearchMetadata data in metadata)
            {
                PowerShellPackage package = new PowerShellPackage();
                package.Id = data.Identity.Id;
                package.Description = data.Summary;

                switch (versionType)
                {
                    case VersionType.all:
                        {
                            package.Version = data.Versions.ToList();
                        }
                        break;
                    case VersionType.latest:
                        {
                            NuGetVersion nVersion = data.Version;
                            if (nVersion == null)
                            {
                                nVersion = data.Versions.OrderByDescending(v => v).FirstOrDefault();
                            }
                            package.Version = new List<NuGetVersion>() { nVersion };
                        }
                        break;
                    case VersionType.updates:
                        {
                            NuGetVersion nVersion = data.Version;
                            package.Version = new List<NuGetVersion>() { nVersion };
                        }
                        break;
                }

                view.Add(package);
            }
            return view;
        }

        /// <summary>
        /// Get the view of PowerShellPackage. Used for Get-Package -Updates command. 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="version"></param>
        /// <param name="versionType"></param>
        /// <returns></returns>
        internal static PowerShellPackage GetPowerShellPackageView(PSSearchMetadata data, NuGetVersion version, VersionType versionType)
        {
            PowerShellPackage package = new PowerShellPackage();
            package.Id = data.Identity.Id;
            package.Description = data.Summary;
            switch (versionType)
            {
                case VersionType.updates:
                    {
                        package.Version = data.Versions.Where(p => p > version).ToList();
                    }
                    break;
                case VersionType.latest:
                    {
                        NuGetVersion nVersion = data.Version;
                        package.Version = new List<NuGetVersion>() { nVersion };
                    }
                    break;
            }

            return package;
        }
    }

    internal class PowerShellPackageWithProject
    {
        public string Id { get; set; }

        public List<NuGetVersion> Version { get; set; }

        public string ProjectName { get; set; }

        /// <summary>
        /// Get the view of installed packages. Use for Get-Package command. 
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="versionType"></param>
        /// <returns></returns>
        internal static List<PowerShellPackageWithProject> GetPowerShellPackageView(Dictionary<NuGetProject, IEnumerable<PackageReference>> dictionary)
        {
            List<PowerShellPackageWithProject> views = new List<PowerShellPackageWithProject>();
            foreach (KeyValuePair<NuGetProject, IEnumerable<PackageReference>> entry in dictionary)
            {
                foreach (PackageReference package in entry.Value)
                {
                    PowerShellPackageWithProject view = new PowerShellPackageWithProject();
                    view.Id = package.PackageIdentity.Id;
                    view.Version = new List<NuGetVersion>() { package.PackageIdentity.Version };
                    view.ProjectName = entry.Key.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                    views.Add(view);
                }
            }
            return views;
        }
    }

    /// <summary>
    /// Enum for types of version to output, which can be all versions, latest version or update versions.
    /// </summary>
    public enum VersionType
    {
        all,
        latest,
        updates
    }
}
