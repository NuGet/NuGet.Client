extern alias Legacy;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;
using LegacyNuGet = Legacy.NuGet;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// Represent the view of packages by Id and Versions
    /// </summary>
    internal class PowerShellPackage : IPowerShellPackage
    {
        public string Id { get; set; }

        public IEnumerable<NuGetVersion> Versions { get; set; }

        public LegacyNuGet.SemanticVersion Version { get; set; }
    }

    /// <summary>
    /// Represent the view of packages installed to project(s)
    /// </summary>
    internal class PowerShellInstalledPackage : IPowerShellPackage
    {
        public string Id { get; set; }

        public IEnumerable<NuGetVersion> Versions { get; set; }

        public LegacyNuGet.SemanticVersion Version { get; set; }

        public string ProjectName { get; set; }

        /// <summary>
        /// Get the view of installed packages. Use for Get-Package command. 
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="versionType"></param>
        /// <returns></returns>
        internal static List<PowerShellInstalledPackage> GetPowerShellPackageView(Dictionary<NuGetProject, IEnumerable<PackageReference>> dictionary)
        {
            List<PowerShellInstalledPackage> views = new List<PowerShellInstalledPackage>();
            foreach (KeyValuePair<NuGetProject, IEnumerable<PackageReference>> entry in dictionary)
            {
                // entry.Value is an empty list if no packages are installed
                foreach (PackageReference package in entry.Value)
                {
                    PowerShellInstalledPackage view = new PowerShellInstalledPackage();
                    view.Id = package.PackageIdentity.Id;
                    view.Versions = new List<NuGetVersion>() { package.PackageIdentity.Version };
                    LegacyNuGet.SemanticVersion sVersion;
                    LegacyNuGet.SemanticVersion.TryParse(package.PackageIdentity.Version.ToNormalizedString(), out sVersion);
                    view.Version = sVersion;
                    view.ProjectName = entry.Key.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
                    views.Add(view);
                }
            }
            return views;
        }
    }

    /// <summary>
    /// Represent packages found from the remote package source
    /// </summary>
    internal class PowerShellRemotePackage : IPowerShellPackage
    {
        public string Id { get; set; }

        public IEnumerable<NuGetVersion> Versions { get; set; }

        public LegacyNuGet.SemanticVersion Version { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Get the view of PowerShellPackage. Used for Get-Package -ListAvailable command. 
        /// </summary>
        /// <param name="metadata">list of PSSearchMetadata</param>
        /// <param name="versionType"></param>
        /// <returns></returns>
        internal static List<PowerShellRemotePackage> GetPowerShellPackageView(IEnumerable<PSSearchMetadata> metadata, VersionType versionType)
        {
            List<PowerShellRemotePackage> view = new List<PowerShellRemotePackage>();
            foreach (PSSearchMetadata data in metadata)
            {
                PowerShellRemotePackage package = new PowerShellRemotePackage();
                package.Id = data.Identity.Id;
                package.Description = data.Summary;

                switch (versionType)
                {
                    case VersionType.all:
                        {
                            package.Versions = data.Versions.OrderByDescending(v => v);
                            if (package.Versions != null && package.Versions.Any())
                            {
                                LegacyNuGet.SemanticVersion sVersion;
                                LegacyNuGet.SemanticVersion.TryParse(package.Versions.FirstOrDefault().ToNormalizedString(), out sVersion);
                                package.Version = sVersion;
                            }
                        }
                        break;
                    case VersionType.latest:
                        {
                            NuGetVersion nVersion = data.Version == null ? data.Versions.OrderByDescending(v => v).FirstOrDefault() : data.Version;
                            package.Versions = new List<NuGetVersion>() { nVersion };
                            if (nVersion != null)
                            {
                                LegacyNuGet.SemanticVersion sVersion;
                                LegacyNuGet.SemanticVersion.TryParse(nVersion.ToNormalizedString(), out sVersion);
                                package.Version = sVersion;
                            }
                        }
                        break;
                }

                view.Add(package);
            }
            return view;
        }
    }

    /// <summary>
    /// Represent package updates found from the remote package source
    /// </summary>
    internal class PowerShellUpdatePackage : IPowerShellPackage
    {
        public string Id { get; set; }

        public IEnumerable<NuGetVersion> Versions { get; set; }

        public LegacyNuGet.SemanticVersion Version { get; set; }

        public string Description { get; set; }

        public string ProjectName { get; set; }

        /// <summary>
        /// Get the view of PowerShellPackage. Used for Get-Package -Updates command. 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="version"></param>
        /// <param name="versionType"></param>
        /// <returns></returns>
        internal static PowerShellUpdatePackage GetPowerShellPackageUpdateView(PSSearchMetadata data, NuGetVersion version, VersionType versionType, NuGetProject project)
        {
            PowerShellUpdatePackage package = new PowerShellUpdatePackage();
            package.Id = data.Identity.Id;
            package.Description = data.Summary;
            package.ProjectName = project.GetMetadata<string>(NuGetProjectMetadataKeys.Name);
            switch (versionType)
            {
                case VersionType.updates:
                    {
                        package.Versions = data.Versions.Where(p => p > version).OrderByDescending(v => v);
                        if (package.Versions != null && package.Versions.Any())
                        {
                            LegacyNuGet.SemanticVersion sVersion;
                            LegacyNuGet.SemanticVersion.TryParse(package.Versions.FirstOrDefault().ToNormalizedString(), out sVersion);
                            package.Version = sVersion;
                        }
                    }
                    break;
                case VersionType.latest:
                    {
                        NuGetVersion nVersion = data.Versions.Where(p => p > version).OrderByDescending(v => v).FirstOrDefault();
                        if (nVersion != null)
                        {
                            package.Versions = new List<NuGetVersion>() { nVersion };
                            LegacyNuGet.SemanticVersion sVersion;
                            LegacyNuGet.SemanticVersion.TryParse(nVersion.ToNormalizedString(), out sVersion);
                            package.Version = sVersion;
                        }
                    }
                    break;
            }

            return package;
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
