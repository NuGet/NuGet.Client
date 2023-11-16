// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultPackageNormal : PackageSearchResultPackageMinimal
    {
        [JsonProperty("downloads")]
        public long? Downloads { get; set; }

        [JsonProperty("authors")]
        public string Authors { get; set; }

        public PackageSearchResultPackageNormal(IPackageSearchMetadata packageSearchMetadata) : base(packageSearchMetadata)
        {
            Downloads = packageSearchMetadata.DownloadCount;
            Authors = packageSearchMetadata.Authors;
        }
    }
}
