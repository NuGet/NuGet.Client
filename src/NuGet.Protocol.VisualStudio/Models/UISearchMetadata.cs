// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class VersionInfo
    {
        public VersionInfo(NuGetVersion version, int? downloadCount)
        {
            Version = version;
            DownloadCount = downloadCount;
        }

        public NuGetVersion Version { get; private set; }

        public int? DownloadCount { get; private set; }
    }

    /// <summary>
    /// Model for Search results displayed by Visual Studio Package Manager dialog UI.
    /// </summary>
    public sealed class UISearchMetadata
    {
        public UISearchMetadata(PackageIdentity identity, string title, string summary, Uri iconUrl, IEnumerable<VersionInfo> versions, UIPackageMetadata latestPackageMetadata)
        {
            Identity = identity;
            Title = title;
            Summary = summary;
            IconUrl = iconUrl;
            Versions = versions;
            LatestPackageMetadata = latestPackageMetadata;
        }

        public PackageIdentity Identity { get; private set; }

        public string Summary { get; private set; }

        public Uri IconUrl { get; private set; }

        public IEnumerable<VersionInfo> Versions { get; private set; }

        public UIPackageMetadata LatestPackageMetadata { get; private set; }

        public string Title { get; private set; }
    }
}
