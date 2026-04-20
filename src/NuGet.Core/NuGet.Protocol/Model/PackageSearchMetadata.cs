// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Licenses;
using NuGet.Protocol.Converters;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class PackageSearchMetadata : IPackageSearchMetadata
    {
        [JsonPropertyName(JsonProperties.Authors)]
        [JsonConverter(typeof(MetadataFieldStjConverter))]
        [JsonInclude]
        public string Authors { get; internal set; }

        [JsonPropertyName(JsonProperties.DependencyGroups)]
        [JsonInclude]
        public IEnumerable<PackageDependencyGroup> DependencySetsInternal { get; internal set; }

        [JsonIgnore]
        public IEnumerable<PackageDependencyGroup> DependencySets
        {
            get
            {
                return DependencySetsInternal ?? Enumerable.Empty<PackageDependencyGroup>();
            }
        }

        [JsonPropertyName(JsonProperties.Description)]
        [JsonInclude]
        public string Description { get; internal set; }

        [JsonPropertyName(JsonProperties.DownloadCount)]
        [JsonInclude]
        public long? DownloadCount { get; internal set; }

        [JsonPropertyName(JsonProperties.IconUrl)]
        [JsonInclude]
        public Uri IconUrl { get; internal set; }

        private PackageIdentity _packageIdentity = null;

        [JsonIgnore]
        public PackageIdentity Identity
        {
            get
            {
                if (_packageIdentity == null)
                {
                    _packageIdentity = new PackageIdentity(PackageId, Version);
                }
                return _packageIdentity;
            }
        }

        [JsonPropertyName(JsonProperties.LicenseUrl)]
        [JsonConverter(typeof(SafeUriStjConverter))]
        [JsonInclude]
        public Uri LicenseUrl { get; internal set; }

        private IReadOnlyList<string> _ownersList;

        [JsonPropertyName(JsonProperties.Owners)]
        [JsonConverter(typeof(MetadataStringOrArrayStjConverter))]
        [JsonInclude]
        public IReadOnlyList<string> OwnersList
        {
            get { return _ownersList; }
            internal set
            {
                if (_ownersList != value)
                {
                    _ownersList = value;
                    _owners = null;
                }
            }
        }

        private string _owners;
        public string Owners
        {
            get
            {
                if (_owners == null)
                {
                    _owners = OwnersList != null ? string.Join(", ", OwnersList.Where(s => !string.IsNullOrWhiteSpace(s))) : null;
                }
                return _owners;
            }
        }

        [JsonPropertyName(JsonProperties.PackageId)]
        [JsonInclude]
        public string PackageId { get; internal set; }

        [JsonPropertyName(JsonProperties.ProjectUrl)]
        [JsonConverter(typeof(SafeUriStjConverter))]
        [JsonInclude]
        public Uri ProjectUrl { get; internal set; }

        [JsonPropertyName(JsonProperties.Published)]
        [JsonInclude]
        public DateTimeOffset? Published { get; internal set; }

        [JsonPropertyName(JsonProperties.ReadmeUrl)]
        [JsonConverter(typeof(SafeUriStjConverter))]
        [JsonInclude]
        public Uri ReadmeUrl { get; internal set; }

        [JsonIgnore]
        public string ReadmeFileUrl { get; internal set; }

        [JsonIgnore]
        public Uri ReportAbuseUrl { get; set; }

        [JsonIgnore]
        public Uri PackageDetailsUrl { get; set; }

        [JsonPropertyName(JsonProperties.RequireLicenseAcceptance)]
        [JsonConverter(typeof(SafeBoolStjConverter))]
        [JsonInclude]
        public bool RequireLicenseAcceptance { get; internal set; }

        private string _summaryValue;

        [JsonPropertyName(JsonProperties.Summary)]
        [JsonInclude]
        public string Summary
        {
            get { return !string.IsNullOrEmpty(_summaryValue) ? _summaryValue : Description; }
            internal set { _summaryValue = value; }
        }

        [JsonPropertyName(JsonProperties.Tags)]
        [JsonConverter(typeof(MetadataFieldStjConverter))]
        [JsonInclude]
        public string Tags { get; internal set; }

        private string _titleValue;

        [JsonPropertyName(JsonProperties.Title)]
        [JsonInclude]
        public string Title
        {
            get { return !string.IsNullOrEmpty(_titleValue) ? _titleValue : PackageId; }
            internal set { _titleValue = value; }
        }

        [JsonPropertyName(JsonProperties.Version)]
        [JsonInclude]
        public NuGetVersion Version { get; internal set; }

        [JsonPropertyName(JsonProperties.Versions)]
        [JsonInclude]
        public VersionInfo[] ParsedVersions { get; internal set; }

        [JsonPropertyName(JsonProperties.PrefixReserved)]
        [JsonInclude]
        public bool PrefixReserved { get; internal set; }

        [JsonPropertyName(JsonProperties.LicenseExpression)]
        [JsonInclude]
        public string LicenseExpression { get; internal set; }

        [JsonPropertyName(JsonProperties.LicenseExpressionVersion)]
        [JsonInclude]
        public string LicenseExpressionVersion { get; internal set; }

        [JsonIgnore]
        public LicenseMetadata LicenseMetadata
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LicenseExpression))
                {
                    return null;
                }

                var trimmedLicenseExpression = LicenseExpression.Trim();

                _ = System.Version.TryParse(LicenseExpressionVersion, out var effectiveVersion);
                effectiveVersion = effectiveVersion ?? LicenseMetadata.EmptyVersion;

                List<string> errors = null;
                NuGetLicenseExpression parsedExpression = null;

                if (effectiveVersion.CompareTo(LicenseMetadata.CurrentVersion) <= 0)
                {
                    try
                    {
                        parsedExpression = NuGetLicenseExpression.Parse(trimmedLicenseExpression);

                        var invalidLicenseIdentifiers = GetNonStandardLicenseIdentifiers(parsedExpression);
                        if (invalidLicenseIdentifiers != null)
                        {
                            if (errors == null)
                            {
                                errors = new List<string>();
                            }
                            errors.Add(string.Format(CultureInfo.CurrentCulture, Strings.NuGetLicenseExpression_NonStandardIdentifier, string.Join(", ", invalidLicenseIdentifiers)));
                        }
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

                return new LicenseMetadata(LicenseType.Expression, license: trimmedLicenseExpression, expression: parsedExpression, warningsAndErrors: errors, version: effectiveVersion);
            }
        }

        private static IList<string> GetNonStandardLicenseIdentifiers(NuGetLicenseExpression expression)
        {
            IList<string> invalidLicenseIdentifiers = null;
            Action<NuGetLicense> licenseProcessor = delegate (NuGetLicense nugetLicense)
            {
                if (!nugetLicense.IsStandardLicense)
                {
                    if (invalidLicenseIdentifiers == null)
                    {
                        invalidLicenseIdentifiers = new List<string>();
                    }
                    invalidLicenseIdentifiers.Add(nugetLicense.Identifier);
                }
            };
            expression.OnEachLeafNode(licenseProcessor, null);

            return invalidLicenseIdentifiers;
        }

        /// <inheritdoc cref="IPackageSearchMetadata.GetVersionsAsync" />
        public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => Task.FromResult<IEnumerable<VersionInfo>>(ParsedVersions);

        [JsonPropertyName(JsonProperties.Listed)]
        [JsonInclude]
        public bool IsListed { get; internal set; } = true;

        [JsonPropertyName(JsonProperties.Deprecation)]
        [JsonInclude]
        public PackageDeprecationMetadata DeprecationMetadata { get; internal set; }

        /// <inheritdoc cref="IPackageSearchMetadata.GetDeprecationMetadataAsync" />
        public Task<PackageDeprecationMetadata> GetDeprecationMetadataAsync() => Task.FromResult(DeprecationMetadata);

        /// <inheritdoc cref="IPackageSearchMetadata.Vulnerabilities" />
        [JsonPropertyName(JsonProperties.Vulnerabilities)]
        [JsonInclude]
        public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; internal set; }

        internal void CacheStrings(MetadataReferenceCache cache)
        {
            Authors = cache.GetString(Authors);
            Description = cache.GetString(Description);
            PackageId = cache.GetString(PackageId);
            ReadmeFileUrl = cache.GetString(ReadmeFileUrl);
            Tags = cache.GetString(Tags);
            Summary = cache.GetString(Summary);
            Title = cache.GetString(Title);
            LicenseExpression = cache.GetString(LicenseExpression);
            LicenseExpressionVersion = cache.GetString(LicenseExpressionVersion);
        }
    }
}
