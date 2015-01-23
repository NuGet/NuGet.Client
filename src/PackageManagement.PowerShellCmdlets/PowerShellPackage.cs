using NuGet.Client.VisualStudio;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    // TODO List
    // 1. Should we add Title to the display? If so, need to embed title field in the search result.
    public class PowerShellPackage
    {
        public string Id { get; set; }

        public List<NuGetVersion> Version { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// Get the view of PowerShell Package. Use for Get-Package command. 
        /// </summary>
        /// <param name="metadata"></param>
        /// <param name="versionType"></param>
        /// <returns></returns>
        public static List<PowerShellPackage> GetPowerShellPackageView(IEnumerable<PSSearchMetadata> metadata, VersionType versionType)
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

        public static PowerShellPackage GetPowerShellPackageView(PSSearchMetadata data, NuGetVersion version, VersionType versionType)
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

    public enum VersionType
    {
        all,
        latest,
        updates
    }
}
