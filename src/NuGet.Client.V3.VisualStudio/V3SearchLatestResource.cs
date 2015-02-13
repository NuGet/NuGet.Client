using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.Client.V3;
using NuGet.Client.VisualStudio;
using NuGet.Data;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Client.V3.VisualStudio
{
    public class V3SearchLatestResource : SearchLatestResource
    {
        private readonly V3RawSearchResource _searchResource;
        private readonly V3UIMetadataResource _metadataResource;

        public V3SearchLatestResource(V3RawSearchResource searchResource, V3UIMetadataResource metadataResource)
            : base()
        {
            _searchResource = searchResource;
            _metadataResource = metadataResource;
        }

        public override async Task<IEnumerable<UIPackageMetadata>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            List<UIPackageMetadata> results = new List<UIPackageMetadata>();

            var searchResultJsonObjects = await _searchResource.Search(searchTerm, filters, skip, take, cancellationToken);

            foreach (JObject package in searchResultJsonObjects)
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

                // retrieve metadata for the top package
                results.Add(_metadataResource.ParseMetadata(package));
            }

            return results;
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
