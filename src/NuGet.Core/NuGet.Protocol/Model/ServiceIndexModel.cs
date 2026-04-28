// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Model
{
    internal sealed class ServiceIndexModel
    {
        [JsonPropertyName("version")]
        public string Version { get; }

        [JsonPropertyName("resources")]
        public List<ServiceIndexEntryModel>? Resources { get; }

        [JsonConstructor]
        public ServiceIndexModel(string? version, List<ServiceIndexEntryModel>? resources)
        {
            if (version is null)
            {
                throw new InvalidDataException(Strings.Protocol_MissingVersion);
            }
            Version = version;
            Resources = resources;
        }
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
