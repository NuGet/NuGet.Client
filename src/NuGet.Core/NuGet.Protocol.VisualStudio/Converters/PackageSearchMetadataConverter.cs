using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio.Converters
{
    public class PackageSearchMetadataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PackageSearchMetadata);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            // Load JObject from stream
            JObject jObject = JObject.Load(reader);

            // Populate properties
            var id = jObject.GetString(Properties.PackageId);
            var version = NuGetVersion.Parse(jObject.GetString(Properties.Version));
            var packageMetadata = ParseMetadata(jObject);

            var result = new PackageSearchMetadata
            {
                Identity = new PackageIdentity(id, version),
                Description = jObject.GetString(Properties.Description),
                Summary = jObject.GetString(Properties.Summary),
                Title = jObject.GetString(Properties.Title),
                IconUrl = jObject.GetUri(Properties.IconUrl),
                Versions = GetVersionList(jObject, version),
                Tags = packageMetadata.Tags.Split(','),
                DownloadCount = packageMetadata.DownloadCount
            };

            return result;
        }

        public static UIPackageMetadata ParseMetadata(JObject metadata)
        {
            var version = NuGetVersion.Parse(metadata.Value<string>(Properties.Version));
            DateTimeOffset? published = metadata.GetDateTime(Properties.Published);
            long? downloadCountValue = metadata.GetLong(Properties.DownloadCount);
            var id = metadata.Value<string>(Properties.PackageId);
            var title = metadata.Value<string>(Properties.Title);
            var summary = metadata.Value<string>(Properties.Summary);
            var description = metadata.Value<string>(Properties.Description);
            var authors = GetField(metadata, Properties.Authors);
            var owners = GetField(metadata, Properties.Owners);
            var iconUrl = metadata.GetUri(Properties.IconUrl);
            var licenseUrl = metadata.GetUri(Properties.LicenseUrl);
            var projectUrl = metadata.GetUri(Properties.ProjectUrl);
            var tags = GetField(metadata, Properties.Tags);
            var requireLicenseAcceptance =
                metadata.GetBoolean(Properties.RequireLicenseAcceptance) ?? false;

            if (string.IsNullOrEmpty(title))
            {
                // If no title exists, use the Id
                title = id;
            }

            return new UIPackageMetadata(
                new PackageIdentity(id, version),
                title,
                summary,
                description,
                authors,
                owners,
                iconUrl,
                licenseUrl,
                projectUrl,
                null,
                tags,
                published,
                null,
                requireLicenseAcceptance,
                downloadCountValue);
        }

        /// <summary>
        /// Returns a field value or the empty string. Arrays will become comma delimited strings.
        /// </summary>
        private static string GetField(JObject json, string property)
        {
            var value = json[property];

            if (value == null)
            {
                return string.Empty;
            }

            var array = value as JArray;

            if (array != null)
            {
                return String.Join(", ", array.Select(e => e.ToString()));
            }

            return value.ToString();
        }

        private static IEnumerable<VersionInfo> GetVersionList(JObject package, NuGetVersion version)
        {
            var versionList = new List<VersionInfo>();
            var versions = package.GetJArray(Properties.Versions);

            if (versions != null)
            {
                foreach (var v in versions)
                {
                    var nugetVersion = NuGetVersion.Parse(v.Value<string>("version"));
                    var count = v.Value<int?>("downloads");
                    versionList.Add(new VersionInfo(nugetVersion, count));
                }
            }

            // TODO: in v2, we only have download count for all versions, not per version.
            // To be consistent, in v3, we also use total download count for now.
            var totalDownloadCount = versionList.Select(v => v.DownloadCount).Sum();
            versionList = versionList.Select(v => new VersionInfo(v.Version, totalDownloadCount))
                .ToList();

            if (!versionList.Select(v => v.Version).Contains(version))
            {
                versionList.Add(new VersionInfo(version, 0));
            }

            return versionList;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
