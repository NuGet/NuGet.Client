using NuGet.Client.VisualStudio;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// FindPackage is identical to GetPackage except that FindPackage filters packages only by Id and does not consider description or tags.
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "Package")]
    [OutputType(typeof(PowerShellPackage))]
    public class FindPackageCommand : NuGetPowerShellBaseCommand
    {
        private const int MaxReturnedPackages = 30;

        public FindPackageCommand()
            : base()
        {
        }

        [Parameter(ValueFromPipelineByPropertyName = true, Position = 0)]
        public string Id { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public string Version { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string Source { get; set; }

        [Parameter]
        [Alias("Prerelease")]
        public SwitchParameter IncludePrerelease { get; set; }

        [Parameter]
        public SwitchParameter ListAll { get; set; }

        /// <summary>
        /// Determines if an exact Id match would be performed with the Filter parameter. By default, FindPackage returns all packages that starts with the
        /// Filter value.
        /// </summary>
        [Parameter]
        public SwitchParameter ExactMatch { get; set; }

        [Parameter]
        [ValidateRange(0, Int32.MaxValue)]
        public virtual int First { get; set; }

        [Parameter]
        [ValidateRange(0, Int32.MaxValue)]
        public int Skip { get; set; }

        protected override void Preprocess()
        {
            base.Preprocess();
            // Since this is used for intellisense, we need to limit the number of packages that we return. Otherwise,
            // typing InstallPackage TAB would download the entire feed.
            First = MaxReturnedPackages;
            if (Id == null)
            {
                Id = string.Empty;
            }
            if (Version == null)
            {
                Version = string.Empty;
            }

            GetActiveSourceRepository(Source);
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            PSAutoCompleteResource autoCompleteResource = ActiveSourceRepository.GetResource<PSAutoCompleteResource>();
            Task<IEnumerable<string>> task = autoCompleteResource.IdStartsWith(Id, IncludePrerelease.IsPresent, CancellationToken.None);
            IEnumerable<string> packageIds = task.Result;
            PowerShellPackage package = new PowerShellPackage();

            if (!ExactMatch.IsPresent)
            {
                List<PowerShellPackage> packages = new List<PowerShellPackage>();
                foreach (string id in packageIds)
                {
                    Task<IEnumerable<NuGetVersion>> versionTask = autoCompleteResource.VersionStartsWith(id, Version, IncludePrerelease.IsPresent, CancellationToken.None);
                    List<NuGetVersion> versions = versionTask.Result.ToList();
                    package.Id = id;
                    if (ListAll.IsPresent)
                    {
                        package.Version = versions;
                    }
                    else
                    {
                        NuGetVersion latestVersion = versions.OrderByDescending(v => v).FirstOrDefault();
                        package.Version = new List<NuGetVersion>() { latestVersion };
                    }
                    packages.Add(package);
                }
                WriteObject(packages, enumerateCollection: true);
            }
            else
            {
                string packageId = task.Result.Where(p => string.Equals(p, Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                package.Id = packageId;
                Task<IEnumerable<NuGetVersion>> versionTask = autoCompleteResource.VersionStartsWith(packageId, Version, IncludePrerelease.IsPresent, CancellationToken.None);
                List<NuGetVersion> versions = versionTask.Result.ToList();
                if (string.IsNullOrEmpty(Version))
                {
                    if (ListAll.IsPresent)
                    {
                        package.Version = versions;
                    }
                    else
                    {
                        NuGetVersion latestVersion = versions.OrderByDescending(v => v).FirstOrDefault();
                        package.Version = new List<NuGetVersion>() { latestVersion };
                    }
                    WriteObject(package);
                }
                else
                {
                    NuGetVersion nVersion;
                    bool success = NuGetVersion.TryParse(Version, out nVersion);
                    if (success)
                    {
                        NuGetVersion version = versions.Where(v => v == nVersion).FirstOrDefault();
                        package.Version = new List<NuGetVersion>() { version };
                        WriteObject(package);
                    }
                }
            }
        }
    }
}
