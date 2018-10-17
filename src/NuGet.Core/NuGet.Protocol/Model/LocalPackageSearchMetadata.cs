// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalPackageSearchMetadata : IPackageSearchMetadata
    {
        private readonly NuspecReader _nuspec;
        private readonly LocalPackageInfo _package;

        public LocalPackageSearchMetadata(LocalPackageInfo package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _nuspec = package.Nuspec;
        }

        public string Authors => _nuspec.GetAuthors();

        public IEnumerable<PackageDependencyGroup> DependencySets => _nuspec.GetDependencyGroups().ToArray();

        public string Description => _nuspec.GetDescription();

        // Local packages always have 0 as the download count
        public long? DownloadCount => 0;

        public Uri IconUrl => Convert(_nuspec.GetIconUrl());

        public PackageIdentity Identity => _nuspec.GetIdentity();

        public Uri LicenseUrl => Convert(_nuspec.GetLicenseUrl());

        public string Owners => _nuspec.GetOwners();

        public Uri ProjectUrl => Convert(_nuspec.GetProjectUrl());

        public DateTimeOffset? Published => _package.LastWriteTimeUtc;

        // There is no report abuse url for local packages.
        public Uri ReportAbuseUrl => null;

        public bool RequireLicenseAcceptance => _nuspec.GetRequireLicenseAcceptance();

        public string Summary => !string.IsNullOrEmpty(_nuspec.GetSummary()) ? _nuspec.GetSummary() : Description;

        public string Tags
        {
            get
            {
                var tags = _nuspec.GetTags()?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[] { };
                return string.Join(" ", tags);
            }
        }

        public string Title => !string.IsNullOrEmpty(_nuspec.GetTitle()) ? _nuspec.GetTitle() : _nuspec.GetId();

        public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => Task.FromResult(Enumerable.Empty<VersionInfo>());

        /// <summary>
        /// Convert a string to a URI safely. This will return null if there are errors.
        /// </summary>
        private static Uri Convert(string uri)
        {
            Uri fullUri = null;

            if (!string.IsNullOrEmpty(uri))
            {
                Uri.TryCreate(uri, UriKind.Absolute, out fullUri);
            }

            return fullUri;
        }

        public bool IsListed => true;

        // The prefix reservation is not applicable to local packages
        public bool PrefixReserved => false;

        public LicenseMetadata LicenseMetadata => _nuspec.GetLicenseMetadata();
    }
}
