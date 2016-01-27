// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Standard Nuspec metadata
    /// </summary>
    public class PackageMetadata : PackageIdentity
    {
        private readonly string _title;
        private readonly string _summary;
        private readonly string _description;
        private readonly string[] _authors;
        private readonly Uri _iconUrl;
        private readonly Uri _licenseUrl;
        private readonly Uri _projectUrl;
        private readonly string[] _tags;
        private readonly DateTimeOffset? _published;
        private readonly PackageDependencyGroup[] _dependencySets;
        private readonly bool _requireLicenseAcceptance;
        private readonly NuGetVersion _minClientVersion;

        public PackageMetadata(PackageIdentity identity, string title, string summary, string description, IEnumerable<string> authors, Uri iconUrl,
            Uri licenseUrl, Uri projectUrl, IEnumerable<string> tags, DateTimeOffset? published, IEnumerable<PackageDependencyGroup> dependencySets,
            bool requireLicenseAcceptance, NuGetVersion minClientVersion)
            : base(identity.Id, identity.Version)
        {
            _title = title;
            _summary = summary;
            _description = description;
            _authors = authors == null ? new string[0] : authors.ToArray();
            _iconUrl = iconUrl;
            _licenseUrl = licenseUrl;
            _projectUrl = projectUrl;
            _tags = tags == null ? new string[0] : tags.ToArray();
            _published = published;
            _dependencySets = dependencySets == null ? new PackageDependencyGroup[0] : dependencySets.ToArray();
            _requireLicenseAcceptance = requireLicenseAcceptance;
            _minClientVersion = minClientVersion;
        }

        /// <summary>
        /// The Title of the package or the Id if no title was provided.
        /// </summary>
        public string Title
        {
            get { return _title; }
        }

        public string Summary
        {
            get { return _summary; }
        }

        public string Description
        {
            get { return _description; }
        }

        public IEnumerable<string> Authors
        {
            get { return _authors; }
        }

        public Uri IconUrl
        {
            get { return _iconUrl; }
        }

        public Uri LicenseUrl
        {
            get { return _licenseUrl; }
        }

        public Uri ProjectUrl
        {
            get { return _projectUrl; }
        }

        public IEnumerable<string> Tags
        {
            get { return _tags; }
        }

        public DateTimeOffset? Published
        {
            get { return _published; }
        }

        public IEnumerable<PackageDependencyGroup> DependencySets
        {
            get { return _dependencySets; }
        }

        public bool RequireLicenseAcceptance
        {
            get { return _requireLicenseAcceptance; }
        }

        public NuGetVersion MinClientVersion
        {
            get { return _minClientVersion; }
        }
    }
}
