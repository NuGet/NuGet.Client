using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Protocol.VisualStudio
{
    public class UISearchResourceV3 : UISearchResource
    {
        private readonly RawSearchResourceV3 _searchResource;
        private readonly UIMetadataResource _metadataResource;

        public UISearchResourceV3(RawSearchResourceV3 searchResource, UIMetadataResource metadataResource)
            : base()
        {
            _searchResource = searchResource;
            _metadataResource = metadataResource;
        }

        public override async Task<IEnumerable<UISearchMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            List<UISearchMetadata> visualStudioUISearchResults = new List<UISearchMetadata>();

            var searchResultJsonObjects = await _searchResource.Search(searchTerm, filters, skip, take, cancellationToken);

            foreach (JObject searchResultJson in searchResultJsonObjects)
            {
                 visualStudioUISearchResults.Add(await GetVisualStudioUISearchResult(searchResultJson, filters.IncludePrerelease, cancellationToken));
            }

            return visualStudioUISearchResults;
        }

        private async Task<UISearchMetadata> GetVisualStudioUISearchResult(JObject package, bool includePrerelease, CancellationToken token)
        {
            string id = package.Value<string>(Properties.PackageId);
            NuGetVersion version = NuGetVersion.Parse(package.Value<string>(Properties.Version));
            PackageIdentity topPackage = new PackageIdentity(id, version);
            Uri iconUrl = GetUri(package, Properties.IconUrl);
            string summary = package.Value<string>(Properties.Summary);
            if (string.IsNullOrWhiteSpace(summary))
            {
                // summary is empty. Use its description instead.
                summary = package.Value<string>(Properties.Description);
            }

            string title = package.Value<string>(Properties.Title);
            if (String.IsNullOrEmpty(title))
            {
                // Use the id instead of the title when no title exists.
                title = id;
            }

            // get other versions
            var versionList = new List<VersionInfo>();
            var versions = package.Value<JArray>(Properties.Versions);
            if (versions != null)
            {
                foreach (var v in versions)
                {
                    var nugetVersion = NuGetVersion.Parse(v.Value<string>("version"));
                    var count = v.Value<int>("downloads");
                    versionList.Add(new VersionInfo(nugetVersion, count));
                }
            }

            // TODO: in v2, we only have download count for all versions, not per version.
            // To be consistent, in v3, we also use total download count for now.
            int totalDownloadCount = versionList.Select(v => v.DownloadCount).Sum();
            versionList = versionList.Select(v => new VersionInfo(v.Version, totalDownloadCount))
                .ToList();

            if (!includePrerelease)
            {
                // remove prerelease version if includePrelease is false
                versionList.RemoveAll(v => v.Version.IsPrerelease);
            }

            if (!versionList.Select(v => v.Version).Contains(version))
            {
                versionList.Add(new VersionInfo(version, 0));
            }

            // retrieve metadata for the top package
            UIPackageMetadata metadata = null;
            UIMetadataResourceV3 v3metadataRes = _metadataResource as UIMetadataResourceV3;

            // for v3 just parse the data from the search results
            if (v3metadataRes != null)
            {
                metadata = v3metadataRes.ParseMetadata(package);
            }

            // if we do not have a v3 metadata resource, request it using whatever is available
            if (metadata == null)
            {
                metadata = await _metadataResource.GetMetadata(topPackage, token);
            }

            UISearchMetadata searchResult = new UISearchMetadata(topPackage, title, summary, iconUrl, versionList, metadata);
            return searchResult;
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

        private static int GetInt(JObject json, string property)
        {
            JToken value = json[property];

            if (value == null)
            {
                return 0;
            }

            return value.ToObject<int>();
        }

        private static DateTimeOffset? GetDateTime(JObject json, string property)
        {
            JToken value = json[property];

            if (value == null)
            {
                return null;
            }

            return value.ToObject<DateTimeOffset>();
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
    }
}
