using Newtonsoft.Json.Linq;
using NuGet.Client.VisualStudio;
using NuGet.Data;
using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    public class V3UIMetadataResource : UIMetadataResource
    {
        private readonly V3RegistrationResource _regResource;
        private readonly DataClient _client;

        public V3UIMetadataResource(DataClient client, V3RegistrationResource regResource)
            : base()
        {
            _regResource = regResource;
            _client = client;
        }

        public override async Task<IEnumerable<UIPackageMetadata>> GetMetadata(IEnumerable<PackageIdentity> packages, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            List<UIPackageMetadata> results = new List<UIPackageMetadata>();

            foreach (var package in packages)
            {
                JObject metatdata = await _regResource.GetPackage(package, token);
                results.Add(GetVisualstudioPackageMetadata(metatdata));
            }

            return results;
        }

        public override async Task<IEnumerable<UIPackageMetadata>> GetMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            IEnumerable<JObject> metadataList = await _regResource.Get(packageId, includePrerelease, includeUnlisted, token);
            return metadataList.Select(item => GetVisualstudioPackageMetadata(item));
        }

        private UIPackageMetadata GetVisualstudioPackageMetadata(JObject metadata)
        {

            NuGetVersion Version = NuGetVersion.Parse(metadata.Value<string>(Properties.Version));
            string publishedStr = metadata.Value<string>(Properties.Published);
            DateTimeOffset? Published = null;
            if (!String.IsNullOrEmpty(publishedStr))
            {
                Published = DateTime.Parse(publishedStr);
            }

            string id = metadata.Value<string>(Properties.PackageId);
            string Summary = metadata.Value<string>(Properties.Summary);
            string Description = metadata.Value<string>(Properties.Description);
            string Authors = metadata.Value<string>(Properties.Authors);
            string Owners = metadata.Value<string>(Properties.Owners);
            Uri IconUrl = GetUri(metadata, Properties.IconUrl);
            Uri LicenseUrl = GetUri(metadata, Properties.LicenseUrl);
            Uri ProjectUrl = GetUri(metadata, Properties.ProjectUrl);
            string Tags = String.Join(" ", (metadata.Value<JArray>(Properties.Tags) ?? Enumerable.Empty<JToken>()).Select(t => t.ToString()));
            int DownloadCount = metadata.Value<int>(Properties.DownloadCount);
            IEnumerable<UIPackageDependencySet> DependencySets = (metadata.Value<JArray>(Properties.DependencyGroups) ?? Enumerable.Empty<JToken>()).Select(obj => LoadDependencySet((JObject)obj));
            bool requireLicenseAcceptance = metadata[Properties.RequireLicenseAcceptance] == null ? false : metadata[Properties.RequireLicenseAcceptance].ToObject<bool>();

            bool HasDependencies = DependencySets.Any(
                set => set.Dependencies != null && set.Dependencies.Count > 0);

            return new UIPackageMetadata(new PackageIdentity(id, Version), Summary, Description, Authors, Owners, IconUrl, LicenseUrl, ProjectUrl, Tags, DownloadCount, Published, DependencySets, HasDependencies, requireLicenseAcceptance);
        }
        private Uri GetUri(JObject json, string property)
        {
            if (json[property] == null)
            {
                return null;
            }
            string str = json[property].ToString();
            if (String.IsNullOrEmpty(str))
            {
                return null;
            }
            return new Uri(str);
        }

        private static UIPackageDependencySet LoadDependencySet(JObject set)
        {
            var fxName = set.Value<string>(Properties.TargetFramework);

            NuGetFramework framework = NuGetFramework.AnyFramework;

            if (!String.IsNullOrEmpty(fxName))
            {
                framework = NuGetFramework.Parse(fxName);
                fxName = framework.GetShortFolderName();
            }

            return new UIPackageDependencySet(framework,
                (set.Value<JArray>(Properties.Dependencies) ?? Enumerable.Empty<JToken>()).Select(obj => LoadDependency((JObject)obj)));
        }

        private static PackageDependency LoadDependency(JObject dep)
        {
            var ver = dep.Value<string>(Properties.Range);
            return new PackageDependency(
                dep.Value<string>(Properties.PackageId),
                String.IsNullOrEmpty(ver) ? null : VersionRange.Parse(ver));
        }
    }
}
