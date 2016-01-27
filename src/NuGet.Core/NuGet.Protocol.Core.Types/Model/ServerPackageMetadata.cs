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
    /// Package metadata from a server feed.
    /// </summary>
    public class ServerPackageMetadata : PackageMetadata
    {
        private readonly int _downloadCount;
        private readonly int _downloadCountForVersion;
        private readonly string[] _owners;
        private readonly IEnumerable<string> _packageTypes;

        public ServerPackageMetadata(PackageIdentity identity, string title, string summary, string description, IEnumerable<string> authors, Uri iconUrl,
            Uri licenseUrl, Uri projectUrl, IEnumerable<string> tags, DateTimeOffset? published, IEnumerable<PackageDependencyGroup> dependencySets,
            bool requireLicenseAcceptance, NuGetVersion minClientVersion, int downloadCount, int downloadCountForVersion, IEnumerable<string> owners, IEnumerable<string> packageTypes)
            : base(identity, title, summary, description, authors, iconUrl,
                licenseUrl, projectUrl, tags, published, dependencySets,
                requireLicenseAcceptance, minClientVersion)
        {
            _downloadCount = downloadCount;
            _downloadCountForVersion = downloadCountForVersion;
            _owners = owners == null ? new string[0] : owners.ToArray();
            _packageTypes = packageTypes == null ? new string[0] : packageTypes.ToArray();
        }

        public int DownloadCount
        {
            get { return _downloadCount; }
        }

        public int DownloadCountForVersion
        {
            get { return _downloadCountForVersion; }
        }

        public IEnumerable<string> Owners
        {
            get { return _owners; }
        }

        public IEnumerable<string> PackageTypes
        {
            get { return _packageTypes; }
        }
    }
}
