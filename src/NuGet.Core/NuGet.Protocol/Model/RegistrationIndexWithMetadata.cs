// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace NuGet.Protocol.Model
{
    // Version-scoped registration readers must ignore unrelated package-ID metadata.
    internal class RegistrationIndexWithMetadata : RegistrationIndex
    {
        [JsonProperty("metadata")]
        [JsonPropertyName("metadata")]
        public RegistrationIndexMetadata? Metadata { get; set; }
    }
}
