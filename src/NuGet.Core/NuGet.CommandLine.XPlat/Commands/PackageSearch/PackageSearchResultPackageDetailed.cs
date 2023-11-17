// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultPackageDetailed : PackageSearchResultPackageNormal
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("vulnerable")]
        public bool? Vulnerable { get; set; }

        [JsonProperty("deprecation")]
        public string Deprecation { get; set; }

        [JsonProperty("projectUrl")]
        public Uri ProjectUrl { get; set; }

        public PackageSearchResultPackageDetailed() : base()
        {
        }

        public PackageSearchResultPackageDetailed(IPackageSearchMetadata packageSearchMetadata, string deprecation) : base(packageSearchMetadata)
        {
            Description = packageSearchMetadata.Description;

            if (packageSearchMetadata.Vulnerabilities != null && packageSearchMetadata.Vulnerabilities.Any())
            {
                Vulnerable = true;
            }

            Deprecation = deprecation;
            ProjectUrl = packageSearchMetadata.ProjectUrl;
        }
    }
}
