// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using Newtonsoft.Json;
using NuGet.Protocol.Converters;
using StjJsonPropertyNameAttribute = System.Text.Json.Serialization.JsonPropertyNameAttribute;
using StjJsonConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute;

namespace NuGet.Protocol
{
    /// <summary>
    /// Subclass of <see cref="PackageSearchMetadata"/> containing the information in the registration blobs that are not present in the V2 feed or search.
    /// Returned by <see cref="PackageMetadataResourceV3"/>.
    /// </summary>
    public class PackageSearchMetadataRegistration : PackageSearchMetadata
    {
        /// <summary>
        /// The <see cref="Uri"/> of this package in the catalog.
        /// </summary>
        [JsonProperty(PropertyName = JsonProperties.SubjectId)]
        [StjJsonPropertyName(JsonProperties.SubjectId)]
        [StjJsonConverter(typeof(SafeUriStjConverter))]
        public Uri CatalogUri { get; init; }
    }
}
