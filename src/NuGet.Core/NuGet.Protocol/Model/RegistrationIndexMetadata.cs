// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace NuGet.Protocol.Model
{
    /// <summary>
    /// The optional package ID-level <c>metadata</c> container on the registration index root.
    /// </summary>
    internal class RegistrationIndexMetadata
    {
        [JsonProperty("sponsorshipUrls")]
        [JsonPropertyName("sponsorshipUrls")]
        public List<string>? SponsorshipUrls { get; set; }
    }
}
