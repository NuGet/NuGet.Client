// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NuGet.Protocol
{
    public class PackageDeprecationMetadata
    {
        [JsonPropertyName(JsonProperties.DeprecationMessage)]
        [JsonInclude]
        public string? Message { get; internal set; }

        [JsonPropertyName(JsonProperties.DeprecationReasons)]
        [JsonInclude]
        public IEnumerable<string> Reasons { get; internal set; } = Array.Empty<string>();

        [JsonPropertyName(JsonProperties.AlternatePackage)]
        [JsonInclude]
        public AlternatePackageMetadata? AlternatePackage { get; internal set; }
    }
}
