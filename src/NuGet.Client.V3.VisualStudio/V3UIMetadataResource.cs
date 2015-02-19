using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Client.VisualStudio;
using NuGet.Data;
using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.Versioning;

namespace NuGet.Client.V3.VisualStudio
{
    public class V3UIMetadataResource : UIMetadataResource
    {
        private readonly V3RegistrationResource _regResource;
        private readonly V3ReportAbuseResource _reportAbuseResource;
        private readonly DataClient _client;

        public V3UIMetadataResource(DataClient client, V3RegistrationResource regResource, V3ReportAbuseResource reportAbuseResource)
            : base()
        {
            _regResource = regResource;
            _client = client;
            _reportAbuseResource = reportAbuseResource;
        }

        public override async Task<IEnumerable<UIPackageMetadata>> GetMetadata(IEnumerable<PackageIdentity> packages, CancellationToken token)
        {
            List<UIPackageMetadata> results = new List<UIPackageMetadata>();

            // group by id to optimize
            foreach (var group in packages.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                var versions = group.OrderBy(e => e.Version, VersionComparer.VersionRelease);

                // find the range of versions we need
                VersionRange range = new VersionRange(versions.First().Version, true, versions.Last().Version, true, true);

                IEnumerable<JObject> metadataList = await _regResource.GetPackageMetadata(group.Key, range, true, true, token);

                results.AddRange(metadataList.Select(item => ParseMetadata(item)));
            }

            return results;
        }

        public override async Task<IEnumerable<UIPackageMetadata>> GetMetadata(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            IEnumerable<JObject> metadataList = await _regResource.GetPackageMetadata(packageId, includePrerelease, includeUnlisted, token);
            return metadataList.Select(item => ParseMetadata(item));
        }

        public UIPackageMetadata ParseMetadata(JObject metadata)
        {
            NuGetVersion Version = NuGetVersion.Parse(metadata.Value<string>(Properties.Version));
            string publishedStr = metadata.Value<string>(Properties.Published);
            DateTimeOffset? Published = null;
            if (!String.IsNullOrEmpty(publishedStr))
            {
                Published = DateTime.Parse(publishedStr);
            }

            string id = metadata.Value<string>(Properties.PackageId);
            string title = metadata.Value<string>(Properties.Title);
            string summary = metadata.Value<string>(Properties.Summary);
            string description = metadata.Value<string>(Properties.Description);
            string authors = GetField(metadata, Properties.Authors);
            string owners = GetField(metadata, Properties.Owners);
            Uri iconUrl = GetUri(metadata, Properties.IconUrl);
            Uri licenseUrl = GetUri(metadata, Properties.LicenseUrl);
            Uri projectUrl = GetUri(metadata, Properties.ProjectUrl);
            string tags = GetField(metadata, Properties.Tags);
            IEnumerable<UIPackageDependencySet> dependencySets = (metadata.Value<JArray>(Properties.DependencyGroups) ?? Enumerable.Empty<JToken>()).Select(obj => LoadDependencySet((JObject)obj));
            bool requireLicenseAcceptance = metadata[Properties.RequireLicenseAcceptance] == null ? false : metadata[Properties.RequireLicenseAcceptance].ToObject<bool>();

            Uri reportAbuseUrl =
                _reportAbuseResource != null ?
                _reportAbuseResource.GetReportAbuseUrl(id, Version) :
                null;

            if (String.IsNullOrEmpty(title))
            {
                // If no title exists, use the Id
                title = id;
            }

            return new UIPackageMetadata(
                new PackageIdentity(id, Version),
                title, summary, description, authors, owners,
                iconUrl, licenseUrl, projectUrl, reportAbuseUrl,
                tags, Published, dependencySets, requireLicenseAcceptance);
        }

        /// <summary>
        /// Returns a field value or the empty string. Arrays will become comma delimited strings.
        /// </summary>
        private static string GetField(JObject json, string property)
        {
            JToken value = json[property];

            if (value == null)
            {
                return string.Empty;
            }

            JArray array = value as JArray;

            if (array != null)
            {
                return String.Join(", ", array.Select(e => e.ToString()));
            }

            return value.ToString();
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