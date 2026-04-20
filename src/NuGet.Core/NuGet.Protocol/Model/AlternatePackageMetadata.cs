// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using NuGet.Protocol.Converters;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class AlternatePackageMetadata
    {
        [JsonPropertyName(JsonProperties.PackageId)]
        [JsonInclude]
        public string? PackageId { get; internal set; }

        [JsonPropertyName(JsonProperties.Range)]
        [JsonConverter(typeof(VersionRangeStjConverter))]
        [JsonInclude]
        public VersionRange? Range { get; internal set; }
    }
}
