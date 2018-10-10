// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Licenses;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class PackageSearchMetadata : IPackageSearchMetadata
    {
        [JsonProperty(PropertyName = JsonProperties.Authors)]
        [JsonConverter(typeof(MetadataFieldConverter))]
        public string Authors { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.DependencyGroups, ItemConverterType = typeof(PackageDependencyGroupConverter))]
        public IEnumerable<PackageDependencyGroup> DependencySetsInternal { get; private set; }

        [JsonIgnore]
        public IEnumerable<PackageDependencyGroup> DependencySets
        {
            get
            {
                return DependencySetsInternal ?? Enumerable.Empty<PackageDependencyGroup>();
            }
        }

        [JsonProperty(PropertyName = JsonProperties.Description)]
        public string Description { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.DownloadCount)]
        public long? DownloadCount { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.IconUrl)]
        public Uri IconUrl { get; private set; }

        [JsonIgnore]
        public PackageIdentity Identity => new PackageIdentity(PackageId, Version);

        [JsonProperty(PropertyName = JsonProperties.LicenseUrl)]
        [JsonConverter(typeof(SafeUriConverter))]
        public Uri LicenseUrl { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Owners)]
        [JsonConverter(typeof(MetadataFieldConverter))]
        public string Owners { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.PackageId)]
        public string PackageId { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.ProjectUrl)]
        [JsonConverter(typeof(SafeUriConverter))]
        public Uri ProjectUrl { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Published)]
        public DateTimeOffset? Published { get; private set; }

        [JsonIgnore]
        public Uri ReportAbuseUrl { get; set; }

        [JsonProperty(PropertyName = JsonProperties.RequireLicenseAcceptance, DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(false)]
        [JsonConverter(typeof(SafeBoolConverter))]
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
        public VersionInfo[] ParsedVersions { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.PrefixReserved)]
        public bool PrefixReserved { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.LicenseExpression)]
        public string LicenseExpression { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.LicenseExpressionVersion)]
        public string LicenseExpressionVersion { get; private set; }

        [JsonIgnore]
        public LicenseMetadata LicenseMetadata
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LicenseExpression))
                {
                    return null;
                }

                System.Version.TryParse(LicenseExpressionVersion, out var effectiveVersion);
                effectiveVersion = effectiveVersion ?? LicenseMetadata.EmptyVersion;

                IList<string> errors = null;
                NuGetLicenseExpression parsedExpression = null;

                if (effectiveVersion.CompareTo(LicenseMetadata.CurrentVersion) <= 0)
                {
                    try
                    {
                        parsedExpression = NuGetLicenseExpression.Parse(LicenseExpression);
                    }
                    catch (NuGetLicenseExpressionParsingException e)
                    {
                        if (errors == null)
                        {
                            errors = new List<string>();
                        }
                        errors.Add(e.Message);
                    }
                }
                else
                {
                    // We can't parse it, add an error 
                    if (errors == null)
                    {
                        errors = new List<string>();
                    }

                    errors.Add(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.NuGetLicense_LicenseExpressionVersionTooHigh,
                            effectiveVersion,
                            LicenseMetadata.CurrentVersion));
                }

                return new LicenseMetadata(LicenseType.Expression, license: LicenseExpression, expression: parsedExpression, warningsAndErrors: errors, version: effectiveVersion);
            }
        }


        public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => Task.FromResult<IEnumerable<VersionInfo>>(ParsedVersions);

        [JsonProperty(PropertyName = JsonProperties.Listed)]
        public bool IsListed { get; private set; } = true;
    }
}
