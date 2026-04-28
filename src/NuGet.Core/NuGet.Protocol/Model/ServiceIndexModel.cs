// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Model
{
    internal sealed class ServiceIndexModel
    {
        [JsonRequired]
        [JsonPropertyName("version")]
        public string Version { get; set; } = null!;

        [JsonPropertyName("resources")]
        public List<ServiceIndexEntryModel>? Resources { get; set; }
    }

    internal sealed class ServiceIndexEntryModel
    {
        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        /// <summary>JSON string or array of strings.</summary>
        [JsonPropertyName("@type")]
        public JsonElement Type { get; set; }

        /// <summary>Optional JSON string or array of strings.</summary>
        [JsonPropertyName("clientVersion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement ClientVersion { get; set; }
    }
}
