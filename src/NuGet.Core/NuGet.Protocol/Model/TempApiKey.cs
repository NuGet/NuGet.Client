// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace NuGet.Protocol.Model
{
    /// <summary>
    /// Response body of the nuget.org "create-verification-key" endpoint, which returns a temporary API key.
    /// </summary>
    internal class TempApiKey
    {
        [JsonProperty("Key")]
        [JsonPropertyName("Key")]
        public string? Key { get; set; }
    }
}
