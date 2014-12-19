using NuGet.Frameworks;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public class ResolverApplication
    {
        const string RegistrationTemplate = "https://az320820.vo.msecnd.net/registrations-1/{0}/index.json";

        NuGetFramework _targetFramework;
        bool _prerelease;
        DependencyGroupInfo _appDependencyGroupInfo;
        IDictionary<string, NuGetVersion> _installedVersions;
        RegistrationInfo _appRegistrationInfo;
        IDictionary<string, VersionRange> _allowedVersions;

        public ResolverApplication(string targetFramework, bool prerelease = false)
        {
            _targetFramework = NuGetFramework.Parse(targetFramework);
            _prerelease = prerelease;
            _appDependencyGroupInfo = new DependencyGroupInfo { TargetFramework = "any" };
            _installedVersions = new Dictionary<string, NuGetVersion>();
            _allowedVersions = new Dictionary<string, VersionRange>();

            //PackageInfo appPackageInfo = new PackageInfo { Version = NuGetVersion.Parse("0.0.0"), PackageContent = new Uri("http://tempuri.org/root/package") };
            PackageInfo appPackageInfo = new PackageInfo();
            appPackageInfo.DependencyGroups.Add(_appDependencyGroupInfo);

            _appRegistrationInfo = new RegistrationInfo();
            _appRegistrationInfo.IncludePrerelease = false;
            _appRegistrationInfo.Packages.Add(appPackageInfo);
        }

        public IList<PackageEntry> Inventory { get; set; }

        public void Install(PackageEntry packageEntry)
        {
            VersionRange range;
            if (packageEntry.Version != null)
            {
                range = new VersionRange(packageEntry.Version, true, packageEntry.Version, true);
            }
            else if (packageEntry.Allowed != null)
            {
                range = packageEntry.Allowed;
            }
            else
            {
                range = Utils.CreateVersionRange("[0.0.0-alpha,)", _prerelease);
            }

            _appDependencyGroupInfo.Dependencies.Add(new DependencyInfo
            {
                Id = packageEntry.Id,
                Range = range,
                RegistrationUri = new Uri(string.Format(RegistrationTemplate, packageEntry.Id.ToLowerInvariant()))
            });
        }

        public async Task<RegistrationInfo> Load(HttpClient httpClient)
        {
            if (Inventory != null)
            {
                foreach (PackageEntry packageEntry in Inventory)
                {
                    AddApplicationDependency(packageEntry);
                }
            }

            RegistrationInfo fullRegistrationInfo = await ResolverMetadataClient.GetTree(httpClient, _appRegistrationInfo, TargetFrameworkFilter);

            Trim.TrimByAllowedVersions(fullRegistrationInfo, _allowedVersions);

            return fullRegistrationInfo;
        }

        void AddApplicationDependency(PackageEntry packageEntry)
        {
            _appDependencyGroupInfo.Dependencies.Add(new DependencyInfo
            {
                Id = packageEntry.Id,
                Range = packageEntry.Allowed ?? Utils.CreateVersionRange("[0.0.0-alpha,)", _prerelease),
                RegistrationUri = new Uri(string.Format(RegistrationTemplate, packageEntry.Id.ToLowerInvariant()))
            });

            _installedVersions.Add(packageEntry.Id, packageEntry.Version);

            if (packageEntry.Allowed != null)
            {
                _allowedVersions.Add(packageEntry.Id, packageEntry.Allowed);
            }
        }

        IDictionary<NuGetVersion, HashSet<string>> TargetFrameworkFilter(IDictionary<NuGetVersion, HashSet<string>> before)
        {
            IDictionary<NuGetVersion, HashSet<string>> after = new Dictionary<NuGetVersion, HashSet<string>>();
            IFrameworkCompatibilityProvider compatibilityProvider = DefaultCompatibilityProvider.Instance;

            foreach (KeyValuePair<NuGetVersion, HashSet<string>> entry in before)
            {
                if (entry.Value.Count == 0)
                {
                    after[entry.Key] = new HashSet<string>();
                }
                else
                {
                    HashSet<string> compatible = new HashSet<string>();
                    foreach (string specified in entry.Value)
                    {
                        NuGetFramework specifiedInThePackage = NuGetFramework.Parse(specified);

                        if (compatibilityProvider.IsCompatible(_targetFramework, specifiedInThePackage))
                        {
                            compatible.Add(specified);
                        }
                    }
                    after[entry.Key] = compatible;
                }
            }

            return after;
        }
    }
}
