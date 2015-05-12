// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

extern alias Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;
using SemanticVersion = Legacy::NuGet.SemanticVersion;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// FindPackage is identical to GetPackage except that FindPackage filters packages only by Id and does not
    /// consider description or tags.
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "Package")]
    [OutputType(typeof(IPowerShellPackage))]
    public class FindPackageCommand : NuGetPowerShellBaseCommand
    {
        // NOTE: Number of packages returned by api.nuget.org is static and is 20
        // Display the same number of results with other endpoints, such as nuget.org/api/v2, as well
        private const int MaxReturnedPackages = 20;

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
        /// Determines if an exact Id match would be performed with the Filter parameter. By default, FindPackage
        /// returns all packages that starts with the
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

        protected void Preprocess()
        {
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
                FindPackageStartWithId(excludeVersionInfo: false);
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

        protected void FindPackageStartWithId(bool excludeVersionInfo)
        {
            PSAutoCompleteResource autoCompleteResource = ActiveSourceRepository.GetResource<PSAutoCompleteResource>(Token);
            IEnumerable<string> packageIds;

            Task<IEnumerable<string>> task = autoCompleteResource.IdStartsWith(Id, IncludePrerelease.IsPresent, Token);

            packageIds = task.Result ?? Enumerable.Empty<string>();

            Token.ThrowIfCancellationRequested();

            packageIds = packageIds.Skip(Skip).Take(First);

            if (excludeVersionInfo)
            {
                List<PowerShellPackage> packages = new List<PowerShellPackage>();

                foreach (var id in packageIds)
                {
                    packages.Add(new PowerShellPackage
                        {
                            Id = id
                        });
                }

                WriteObject(packages, enumerateCollection: true);
                return;
            }

            if (!ExactMatch.IsPresent)
            {
                List<IPowerShellPackage> packages = new List<IPowerShellPackage>();
                foreach (string id in packageIds)
                {
                    IPowerShellPackage package = GetIPowerShellPackageFromRemoteSource(autoCompleteResource, id);
                    if (package.Versions != null
                        && package.Versions.Any())
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
                        if (package.Versions != null
                            && package.Versions.Any())
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
                Task<IEnumerable<NuGetVersion>> versionTask = autoCompleteResource.VersionStartsWith(id, Version, IncludePrerelease.IsPresent, Token);
                versions = versionTask.Result;
            }
            catch (Exception)
            {
            }

            IPowerShellPackage package = new PowerShellPackage();
            package.Id = id;
            if (AllVersions.IsPresent)
            {
                if (versions != null
                    && versions.Any())
                {
                    package.Versions = versions.OrderByDescending(v => v);
                    SemanticVersion sVersion;
                    SemanticVersion.TryParse(package.Versions.FirstOrDefault().ToNormalizedString(), out sVersion);
                    package.Version = sVersion;
                }
            }
            else
            {
                NuGetVersion nVersion = null;
                if (versions != null
                    && versions.Any())
                {
                    nVersion = versions.OrderByDescending(v => v).FirstOrDefault();
                }

                if (nVersion != null)
                {
                    package.Versions = new List<NuGetVersion> { nVersion };
                    SemanticVersion sVersion;
                    SemanticVersion.TryParse(nVersion.ToNormalizedString(), out sVersion);
                    package.Version = sVersion;
                }
            }
            return package;
        }
    }
}
