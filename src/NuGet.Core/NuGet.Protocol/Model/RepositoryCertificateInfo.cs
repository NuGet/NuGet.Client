// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using System.Text.Json.Serialization;

namespace NuGet.Protocol
{
    public class RepositoryCertificateInfo : IRepositoryCertificateInfo
    {
        [JsonProperty(PropertyName = JsonProperties.Fingerprints, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.Fingerprints)]
        public Fingerprints Fingerprints { get; internal init; } = null!;

        [JsonProperty(PropertyName = JsonProperties.Subject, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.Subject)]
        public string Subject { get; internal init; } = null!;

        [JsonProperty(PropertyName = JsonProperties.Issuer, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.Issuer)]
        public string Issuer { get; internal init; } = null!;

        [JsonProperty(PropertyName = JsonProperties.NotBefore, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.NotBefore)]
        public DateTimeOffset NotBefore { get; internal init; }

        [JsonProperty(PropertyName = JsonProperties.NotAfter, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.NotAfter)]
        public DateTimeOffset NotAfter { get; internal init; }

        [JsonProperty(PropertyName = JsonProperties.ContentUrl, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.ContentUrl)]
        public string ContentUrl { get; internal init; } = null!;
    }
}
