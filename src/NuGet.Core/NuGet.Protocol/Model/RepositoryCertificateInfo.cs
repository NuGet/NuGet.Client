// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Protocol.Converters;
using StjJsonPropertyNameAttribute = System.Text.Json.Serialization.JsonPropertyNameAttribute;
using StjJsonConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute;

namespace NuGet.Protocol
{
    public class RepositoryCertificateInfo : IRepositoryCertificateInfo
    {
        [JsonProperty(PropertyName = JsonProperties.Fingerprints)]
        [StjJsonPropertyName("fingerprints")]
        [StjJsonConverter(typeof(FingerprintsStjConverter))]
        public Fingerprints Fingerprints { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Subject)]
        [StjJsonPropertyName("subject")]
        public string Subject { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.Issuer)]
        [StjJsonPropertyName("issuer")]
        public string Issuer { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.NotBefore)]
        [StjJsonPropertyName("notBefore")]
        public DateTimeOffset NotBefore { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.NotAfter)]
        [StjJsonPropertyName("notAfter")]
        public DateTimeOffset NotAfter { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.ContentUrl)]
        [StjJsonPropertyName("contentUrl")]
        public string ContentUrl { get; private set; }
    }
}
