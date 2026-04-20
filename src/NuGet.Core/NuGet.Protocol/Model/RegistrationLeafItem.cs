// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Model
{
    /// <summary>
    /// Source: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-leaf-object-in-a-page
    /// </summary>
    internal class RegistrationLeafItem
    {
        [JsonPropertyName("catalogEntry")]
        public PackageSearchMetadataRegistration CatalogEntry { get; set; }

        [JsonPropertyName(JsonProperties.PackageContent)]
        public Uri PackageContent { get; set; }
    }
}
