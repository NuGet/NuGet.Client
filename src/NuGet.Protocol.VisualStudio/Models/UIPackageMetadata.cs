using NuGet.Packaging;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// Full package details as used by the UI
    /// </summary>
    public sealed class UIPackageMetadata
    {
        public UIPackageMetadata(PackageIdentity identity, string title, string summary, string description, string authors, string owners, Uri iconUrl, Uri licenseUrl, Uri projectUrl, Uri reportAbuseUrl,
            string tags, DateTimeOffset? published, IEnumerable<PackageDependencyGroup> dependencySet, bool requireLicenseAccept)
        {
            Identity = identity;
            Summary = summary;
            Description = description;
            Authors = authors;
            Owners = owners;
            IconUrl = iconUrl;
            LicenseUrl = licenseUrl;
            ProjectUrl = projectUrl;
            ReportAbuseUrl = reportAbuseUrl;
            Description = description;
            Summary = summary;
            Tags = tags;
            DependencySets = dependencySet;
            RequireLicenseAcceptance = requireLicenseAccept;
            Title = title;
        }

        /// <summary>
        /// Package Id and Version
        /// </summary>
        public PackageIdentity Identity { get; private set; }

        /// <summary>
        /// The Title of the package or the Id if no title was provided.
        /// </summary>
        public string Title { get; private set; }

        public string Summary { get; private set; }

        public string Description { get; private set; }

        public string Authors { get; private set; }

        public string Owners { get; private set; }

        public Uri IconUrl { get; private set; }

        public Uri LicenseUrl { get; private set; }

        public Uri ProjectUrl { get; private set; }

        public Uri ReportAbuseUrl { get; private set; }

        public string Tags { get; private set; }

        public int DownloadCount { get; private set; }

        public DateTimeOffset? Published { get; private set; }

        public IEnumerable<PackageDependencyGroup> DependencySets { get; private set; }

        public bool RequireLicenseAcceptance { get; private set; }
    }
}
