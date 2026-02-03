// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Protocol.Converters;
using StjJsonConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute;
using StjJsonPropertyNameAttribute = System.Text.Json.Serialization.JsonPropertyNameAttribute;

namespace NuGet.Protocol
{
    public class RepositoryCertificateInfo : IRepositoryCertificateInfo
    {
        [JsonProperty(PropertyName = JsonProperties.Fingerprints)]
        [StjJsonPropertyName(JsonProperties.Fingerprints)]
        [StjJsonConverter(typeof(FingerprintsStjConverter))]
        public Fingerprints Fingerprints { get; init; }

        [JsonProperty(PropertyName = JsonProperties.Subject)]
        [StjJsonPropertyName(JsonProperties.Subject)]
        public string Subject { get; init; }

        [JsonProperty(PropertyName = JsonProperties.Issuer)]
        [StjJsonPropertyName(JsonProperties.Issuer)]
        public string Issuer { get; init; }

        [JsonProperty(PropertyName = JsonProperties.NotBefore)]
        [StjJsonPropertyName(JsonProperties.NotBefore)]
        public DateTimeOffset NotBefore { get; init; }

        [JsonProperty(PropertyName = JsonProperties.NotAfter)]
        [StjJsonPropertyName(JsonProperties.NotAfter)]
        public DateTimeOffset NotAfter { get; init; }

        [JsonProperty(PropertyName = JsonProperties.ContentUrl)]
        [StjJsonPropertyName(JsonProperties.ContentUrl)]
        public string ContentUrl { get; init; }
    }
}
