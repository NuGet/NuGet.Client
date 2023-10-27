// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Represents the result of a package search for a specific source.
    /// </summary>
    internal class PackageSearchResult
    {
        [JsonProperty("sourceName")]
        public string SourceName { get; set; }

        [JsonProperty("packages")]
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
        [JsonProperty("authors")]
        public string Authors { get; set; }

        [JsonProperty("deprecation")]
        public string Deprecation { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("downloads")]
        public long? Downloads { get; set; }

        [JsonProperty("latestVersion")]
        public string LatestVersion { get; set; }

        [JsonProperty("packageId")]
        public string PackageId { get; set; }

        [JsonProperty("projectUrl")]
        public Uri ProjectUrl { get; set; }

        [JsonProperty("vulnerabilities")]
        public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; set; }

        public Package() { }
        public Package(IPackageSearchMetadata searchMetadata, string deprecation)
        {
            Authors = searchMetadata.Authors;
            Deprecation = deprecation;
            Description = searchMetadata.Description;
            Downloads = searchMetadata.DownloadCount;
            LatestVersion = searchMetadata.Identity.Version.ToString();
            PackageId = searchMetadata.Identity.Id;
            ProjectUrl = searchMetadata.ProjectUrl;
            Vulnerabilities = searchMetadata.Vulnerabilities;
        }
    }
}
