// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using Newtonsoft.Json;
using NuGet.Protocol.Converters;
using NuGet.Versioning;
using StjJsonPropertyNameAttribute = System.Text.Json.Serialization.JsonPropertyNameAttribute;
using StjJsonConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute;

namespace NuGet.Protocol
{
    public class AlternatePackageMetadata
    {
        [JsonProperty(PropertyName = JsonProperties.PackageId)]
        [StjJsonPropertyName("id")]
        public string PackageId { get; internal set; }

        [JsonProperty(PropertyName = JsonProperties.Range, ItemConverterType = typeof(VersionRangeConverter))]
        [StjJsonPropertyName("range")]
        [StjJsonConverter(typeof(VersionRangeStjConverter))]
        public VersionRange Range { get; internal set; }
    }
}
