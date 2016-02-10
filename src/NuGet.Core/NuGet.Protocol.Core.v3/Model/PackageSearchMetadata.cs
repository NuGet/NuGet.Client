using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    public class PackageSearchMetadata : IPackageSearchMetadata
    {
        [JsonProperty(PropertyName = JsonProperties.Authors)]
        [JsonConverter(typeof(MetadataFieldConverter))]
        public string Authors { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.DependencyGroups, ItemConverterType = typeof(PackageDependencyGroupConverter))]
        public IEnumerable<PackageDependencyGroup> DependencySets { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Description)]
        public string Description { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.DownloadCount)]
        public long? DownloadCount { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.IconUrl)]
        public Uri IconUrl { get; private set; }

        [JsonIgnore]
        public PackageIdentity Identity => new PackageIdentity(PackageId, Version);

        [JsonProperty(PropertyName = JsonProperties.LicenseUrl)]
        public Uri LicenseUrl { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Owners)]
        [JsonConverter(typeof(MetadataFieldConverter))]
        public string Owners { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.PackageId)]
        public string PackageId { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.ProjectUrl)]
        public Uri ProjectUrl { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Published)]
        public DateTimeOffset? Published { get; private set; }

        [JsonIgnore]
        public Uri ReportAbuseUrl { get; set; }

        [JsonProperty(PropertyName = JsonProperties.RequireLicenseAcceptance, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        public bool RequireLicenseAcceptance { get; private set; }

        private string _summaryValue;

        [JsonProperty(PropertyName = JsonProperties.Summary)]
        public string Summary
        {
            get { return !string.IsNullOrEmpty(_summaryValue) ? _summaryValue : Description; }
            private set { _summaryValue = value; }
        }

        [JsonProperty(PropertyName = JsonProperties.Tags)]
        [JsonConverter(typeof(MetadataFieldConverter))]
        public string Tags { get; private set; }

        private string _titleValue;

        [JsonProperty(PropertyName = JsonProperties.Title)]
        public string Title
        {
            get { return !string.IsNullOrEmpty(_titleValue) ? _titleValue : PackageId; }
            private set { _titleValue = value; }
        }

        [JsonProperty(PropertyName = JsonProperties.Version)]
        public NuGetVersion Version { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Versions)]
        [JsonConverter(typeof(OnDemandParsedVersionsConverter))]
        public Lazy<VersionInfo[]> OnDemandParsedVersions { get; private set; }

        public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => Task.FromResult<IEnumerable<VersionInfo>>(OnDemandParsedVersions.Value);
    }
}
