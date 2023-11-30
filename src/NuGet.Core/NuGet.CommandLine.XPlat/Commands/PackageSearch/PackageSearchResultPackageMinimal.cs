// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.CommandLine.XPlat.Commands.PackageSearch;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultPackageMinimal : IPackageSearchResultPackage
    {
        [JsonProperty("id")]
        public string PackageId { get; set; }

        [JsonProperty("latestVersion")]
        public string LatestVersion { get; set; }

        public PackageSearchResultPackageMinimal() { }

        public PackageSearchResultPackageMinimal(IPackageSearchMetadata packageSearchMetadata)
        {
            PackageId = packageSearchMetadata.Identity.Id;
            LatestVersion = packageSearchMetadata.Identity.Version.ToString();
        }
    }
}
