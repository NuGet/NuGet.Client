// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using NuGet.Protocol.Converters;
using NuGet.Protocol.Model;

namespace NuGet.Protocol.Utility
{
#pragma warning disable CS3016 // Arrays as attribute arguments is not CLS-compliant
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        MaxDepth = 512,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UseStringEnumConverter = true,
        Converters = [
            typeof(NuGetVersionStjConverter),
            typeof(VersionInfoStjConverter),
            typeof(FingerprintsStjConverter),
            typeof(VersionRangeStjConverter),
            //typeof(PackageVulnerabilityInfoStjConverter),
            //typeof(NuGetFrameworkStjConverter)
            ])]
#pragma warning restore CS3016 // Arrays as attribute arguments is not CLS-compliant
    [JsonSerializable(typeof(HttpFileSystemBasedFindPackageByIdResource.FlatContainerVersionList))]
    [JsonSerializable(typeof(IReadOnlyList<V3VulnerabilityIndexEntry>), TypeInfoPropertyName = "VulnerabilityIndex")]
    [JsonSerializable(typeof(CaseInsensitiveDictionary<IReadOnlyList<PackageVulnerabilityInfo>>), TypeInfoPropertyName = "VulnerabilityPage")]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(RegistrationIndex))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
