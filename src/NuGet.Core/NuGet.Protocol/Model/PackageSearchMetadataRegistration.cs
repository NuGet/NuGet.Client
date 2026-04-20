// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Text.Json.Serialization;
using NuGet.Protocol.Converters;

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
        [JsonPropertyName(JsonProperties.SubjectId)]
        [JsonConverter(typeof(SafeUriStjConverter))]
        [JsonInclude]
        public Uri CatalogUri { get; internal set; }
    }
}
