// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.CommandLine.XPlat.Commands.PackageSearch;

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
        public List<IPackageSearchResultPackage> Packages { get; set; }

        public PackageSearchResult(string source)
        {
            SourceName = source;
            Packages = new List<IPackageSearchResultPackage>();
        }

        public void AddPackage(IPackageSearchResultPackage package)
        {
            Packages.Add(package);
        }
    }
}
