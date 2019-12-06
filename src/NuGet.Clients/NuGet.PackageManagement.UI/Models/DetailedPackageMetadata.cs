// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class DetailedPackageMetadata
    {
        public DetailedPackageMetadata()
        {
        }

        public DetailedPackageMetadata(IPackageSearchMetadata serverData, PackageDeprecationMetadata deprecationMetadata, long? downloadCount)
        {
            Id = serverData.Identity.Id;
            Version = serverData.Identity.Version;
            Summary = serverData.Summary;
            Description = serverData.Description;
            Authors = serverData.Authors;
            Owners = serverData.Owners;
            IconUrl = serverData.IconUrl;
            LicenseUrl = serverData.LicenseUrl;
            ProjectUrl = serverData.ProjectUrl;
            ReportAbuseUrl = serverData.ReportAbuseUrl;
            Tags = serverData.Tags;
            DownloadCount = downloadCount;
            Published = serverData.Published;
            DependencySets = serverData.DependencySets?
                .Select(e => new PackageDependencySetMetadata(e))
                ?? new PackageDependencySetMetadata[] { };
            HasDependencies = DependencySets.Any(
                dependencySet => dependencySet.Dependencies != null && dependencySet.Dependencies.Count > 0);
            PrefixReserved = serverData.PrefixReserved;
            LicenseMetadata = serverData.LicenseMetadata;
            DeprecationMetadata = deprecationMetadata;
            _localMetadata = serverData as LocalPackageSearchMetadata;

            // Determine the package details URL and text.
            PackageDetailsUrl = null;
            PackageDetailsText = null;
            if (serverData.PackageDetailsUrl != null
                && serverData.PackageDetailsUrl.IsAbsoluteUri
                && serverData.PackageDetailsUrl.Host != null)
            {
                PackageDetailsUrl = serverData.PackageDetailsUrl;
                PackageDetailsText = serverData.PackageDetailsUrl.Host;

                // Special case the subdomain "www." - we hide it. Other subdomains are not hidden.
                const string wwwDot = "www.";
                if (PackageDetailsText.StartsWith(wwwDot, StringComparison.OrdinalIgnoreCase)
                    && PackageDetailsText.Length > wwwDot.Length)
                {
                    PackageDetailsText = PackageDetailsText.Substring(wwwDot.Length);
                }
            }
        }

        private readonly LocalPackageSearchMetadata _localMetadata;

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public string Summary { get; set; }

        public string Description { get; set; }

        public string Authors { get; set; }

        public string Owners { get; set; }

        public Uri IconUrl { get; set; }

        public Uri LicenseUrl { get; set; }

        public Uri ProjectUrl { get; set; }

        public Uri ReportAbuseUrl { get; set; }

        public Uri PackageDetailsUrl { get; set; }

        public string PackageDetailsText { get; set; }

        public string Tags { get; set; }

        public long? DownloadCount { get; set; }

        public DateTimeOffset? Published { get; set; }

        public IEnumerable<PackageDependencySetMetadata> DependencySets { get; set; }

        public bool PrefixReserved { get; set; }

        // This property is used by data binding to display text "No dependencies"
        public bool HasDependencies { get; set; }

        public LicenseMetadata LicenseMetadata { get; set; }

        public PackageDeprecationMetadata DeprecationMetadata { get; set; }

        public IReadOnlyList<IText> LicenseLinks => PackageLicenseUtilities.GenerateLicenseLinks(this);

        public string LoadFileAsText(string path)
        {
            if (_localMetadata != null)
            {
                return _localMetadata.LoadFileAsText(path);
            }
            return null;
        }

        public Stream GetEmbeddedIconStream(string iconPath)
        {
            Stream stream = null;

            if (_localMetadata != null)
            {
                return _localMetadata.GetEmbeddedIconStream(iconPath);
            }

            return stream;
        }

        public Stream EmbeddedIconStream
        {
            get
            {
                if (_localMetadata != null && IconUrl != null && IconUrl.IsAbsoluteUri)
                {
                    var poundMark = IconUrl.OriginalString.LastIndexOf('#');
                    if (poundMark >= 0)
                    {
                        var iconEntry = Uri.UnescapeDataString(IconUrl.Fragment).Substring(1);
                        return GetEmbeddedIconStream(iconEntry);
                    }
                }

                return null;
            }
        }
    }
}
