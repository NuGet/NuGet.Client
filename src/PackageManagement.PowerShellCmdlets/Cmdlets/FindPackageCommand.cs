extern alias Legacy;
using LegacyNuGet = Legacy.NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// FindPackage is identical to GetPackage except that FindPackage filters packages only by Id and does not consider description or tags.
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "Package")]
    [OutputType(typeof(IPowerShellPackage))]
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
        public SwitchParameter AllVersions { get; set; }

        /// <summary>
        /// Determines if an exact Id match would be performed with the Filter parameter. By default, FindPackage returns all packages that starts with the
        /// Filter value.
        /// </summary>
        [Parameter]
        public SwitchParameter ExactMatch { get; set; }

        /// <summary>
        /// Find packages by AutoComplete endpoint, starting with Id.
        /// Used for tab expansion.
        /// </summary>
        [Parameter]
        public SwitchParameter StartWith { get; set; }

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
            if (First == 0)
            {
                First = MaxReturnedPackages;
            }
            if (Id == null)
            {
                Id = string.Empty;
            }
            if (Version == null)
            {
                Version = string.Empty;
            }

            UpdateActiveSourceRepository(Source);
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            if (StartWith.IsPresent)
            {
                FindPackageStartWithId();
            }
            else
            {
                FindPackagesByPSSearchService();
            }
        }

        private void FindPackagesByPSSearchService()
        {
            VersionType versionType;
            IEnumerable<PSSearchMetadata> remotePackages = GetPackagesFromRemoteSource(Id, Enumerable.Empty<string>(), IncludePrerelease.IsPresent, Skip, First);
            if (ExactMatch.IsPresent)
            {
                remotePackages = remotePackages.Where(p => string.Equals(p.Identity.Id, Id, StringComparison.OrdinalIgnoreCase));
            }

            if (AllVersions.IsPresent)
            {
                versionType = VersionType.all;
            }
            else
            {
                versionType = VersionType.latest;
            }
            var view = PowerShellRemotePackage.GetPowerShellPackageView(remotePackages, versionType);
            if (view.Any())
            {
                WriteObject(view, enumerateCollection: true);
            }
        }

        private void FindPackageStartWithId()
        {
            PSAutoCompleteResource autoCompleteResource = ActiveSourceRepository.GetResource<PSAutoCompleteResource>();
            IEnumerable<string> packageIds = Enumerable.Empty<string>();
            try
            {
                Task<IEnumerable<string>> task = autoCompleteResource.IdStartsWith(Id, IncludePrerelease.IsPresent, CancellationToken.None);
                packageIds = task.Result;
                if (packageIds != null && packageIds.Any())
                {
                    packageIds = packageIds.Skip(Skip).Take(First);
                }
            }
            catch (Exception) { }

            if (!ExactMatch.IsPresent)
            {
                List<IPowerShellPackage> packages = new List<IPowerShellPackage>();
                foreach (string id in packageIds)
                {
                    IPowerShellPackage package = GetIPowerShellPackageFromRemoteSource(autoCompleteResource, id);
                    if (package.Versions != null && package.Versions.Any())
                    {
                        packages.Add(package);
                    }
                }
                WriteObject(packages, enumerateCollection: true);
            }
            else
            {
                if (packageIds.Any())
                {
                    string packageId = packageIds.Where(p => string.Equals(p, Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (!string.IsNullOrEmpty(packageId))
                    {
                        IPowerShellPackage package = GetIPowerShellPackageFromRemoteSource(autoCompleteResource, packageId);
                        if (package.Versions != null && package.Versions.Any())
                        {
                            WriteObject(package);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get IPowerShellPackage from the remote package source
        /// </summary>
        /// <param name="autoCompleteResource"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private IPowerShellPackage GetIPowerShellPackageFromRemoteSource(PSAutoCompleteResource autoCompleteResource, string id)
        {
            IEnumerable<NuGetVersion> versions = Enumerable.Empty<NuGetVersion>();
            try
            {
                Task<IEnumerable<NuGetVersion>> versionTask = autoCompleteResource.VersionStartsWith(id, Version, IncludePrerelease.IsPresent, CancellationToken.None);
                versions = versionTask.Result;
            }
            catch (Exception) { }

            IPowerShellPackage package = new PowerShellPackage();
            package.Id = id;
            if (AllVersions.IsPresent)
            {
                if (versions != null && versions.Any())
                {
                    package.Versions = versions.OrderByDescending(v => v);
                    LegacyNuGet.SemanticVersion sVersion;
                    LegacyNuGet.SemanticVersion.TryParse(package.Versions.FirstOrDefault().ToNormalizedString(), out sVersion);
                    package.Version = sVersion;
                }
            }
            else
            {
                NuGetVersion nVersion = null;
                if (versions != null && versions.Any())
                {
                    nVersion = versions.OrderByDescending(v => v).FirstOrDefault();
                }

                if (nVersion != null)
                {
                    package.Versions = new List<NuGetVersion>() { nVersion };
                    LegacyNuGet.SemanticVersion sVersion;
                    LegacyNuGet.SemanticVersion.TryParse(nVersion.ToNormalizedString(), out sVersion);
                    package.Version = sVersion;
                }
            }
            return package;
        }
    }
}
