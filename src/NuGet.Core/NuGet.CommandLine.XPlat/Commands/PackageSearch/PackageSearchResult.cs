// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Represents the result of a package search for a specific source.
    /// </summary>
    internal class PackageSearchResult
    {
        public string SourceName { get; set; }
        public List<Package> Packages { get; set; }

        public PackageSearchResult(string source)
        {
            SourceName = source;
            Packages = new List<Package>();
        }

        public void AddPackage(Package package)
        {
            Packages.Add(package);
        }
    }

    /// <summary>
    /// Represents a package with its metadata.
    /// </summary>
    internal class Package
    {
        public string Authors { get; set; }
        public string Deprecation { get; set; }
        public string Description { get; set; }
        public long? Downloads { get; set; }
        public string LatestVersion { get; set; }
        public string PackageId { get; set; }
        public Uri ProjectUri { get; set; }
        public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; set; }

        public Package(IPackageSearchMetadata searchMetadata, string deprecation)
        {
            Authors = searchMetadata.Authors;
            Deprecation = deprecation;
            Description = searchMetadata.Description;
            Downloads = searchMetadata.DownloadCount;
            LatestVersion = searchMetadata.Identity.Version.ToString();
            PackageId = searchMetadata.Identity.Id;
            ProjectUri = searchMetadata.ProjectUrl;
            Vulnerabilities = searchMetadata.Vulnerabilities;
        }
    }
}
