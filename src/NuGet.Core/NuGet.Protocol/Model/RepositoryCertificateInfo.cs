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
        public Fingerprints Fingerprints { get; private set; } = null!;

        [JsonProperty(PropertyName = JsonProperties.Subject, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.Subject)]
        public string Subject { get; private set; } = null!;

        [JsonProperty(PropertyName = JsonProperties.Issuer, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.Issuer)]
        public string Issuer { get; private set; } = null!;

        [JsonProperty(PropertyName = JsonProperties.NotBefore, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.NotBefore)]
        public DateTimeOffset NotBefore { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.NotAfter, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.NotAfter)]
        public DateTimeOffset NotAfter { get; private set; }

        [JsonProperty(PropertyName = JsonProperties.ContentUrl, Required = Required.Always)]
        [JsonPropertyName(JsonProperties.ContentUrl)]
        public string ContentUrl { get; private set; } = null!;

        public RepositoryCertificateInfo() { }

        [System.Text.Json.Serialization.JsonConstructor]
        internal RepositoryCertificateInfo(
            Fingerprints fingerprints,
            string subject,
            string issuer,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            string contentUrl)
        {
            Fingerprints = fingerprints;
            Subject = subject;
            Issuer = issuer;
            NotBefore = notBefore;
            NotAfter = notAfter;
            ContentUrl = contentUrl;
        }
    }
}
