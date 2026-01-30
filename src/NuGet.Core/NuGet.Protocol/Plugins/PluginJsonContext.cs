// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Text.Json.Serialization;

namespace NuGet.Protocol.Plugins
{
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        UseStringEnumConverter = true)]
    [JsonSerializable(typeof(GetCredentialsRequest))]
    [JsonSerializable(typeof(GetCredentialsResponse))]
    [JsonSerializable(typeof(GetServiceIndexRequest))]
    [JsonSerializable(typeof(GetServiceIndexResponse))]
    [JsonSerializable(typeof(MonitorNuGetProcessExitRequest))]
    [JsonSerializable(typeof(MonitorNuGetProcessExitResponse))]
    [JsonSerializable(typeof(HandshakeRequest))]
    [JsonSerializable(typeof(HandshakeResponse))]
    [JsonSerializable(typeof(LogRequest))]
    [JsonSerializable(typeof(LogResponse))]
    [JsonSerializable(typeof(Fault))]
    [JsonSerializable(typeof(Progress))]
    [JsonSerializable(typeof(Message))]
    [JsonSerializable(typeof(System.Collections.Generic.IReadOnlyList<OperationClaim>))]
    [JsonSerializable(typeof(PackageSearchMetadata))]
    [JsonSerializable(typeof(Packaging.PackageDependencyGroup))]
    [JsonSerializable(typeof(Core.Types.VersionInfo))]
    [JsonSerializable(typeof(PackageDeprecationMetadata))]
    [JsonSerializable(typeof(PackageVulnerabilityMetadata))]
    [JsonSerializable(typeof(AlternatePackageMetadata))]
    [JsonSerializable(typeof(RepositoryCertificateInfo))]
    [JsonSerializable(typeof(GetOperationClaimsRequest))]
    [JsonSerializable(typeof(GetOperationClaimsResponse))]
    [JsonSerializable(typeof(InitializeRequest))]
    [JsonSerializable(typeof(InitializeResponse))]
    [JsonSerializable(typeof(GetPackageHashRequest))]
    [JsonSerializable(typeof(GetPackageHashResponse))]
    [JsonSerializable(typeof(CopyFilesInPackageRequest))]
    [JsonSerializable(typeof(CopyFilesInPackageResponse))]
    [JsonSerializable(typeof(CopyNupkgFileRequest))]
    [JsonSerializable(typeof(CopyNupkgFileResponse))]
    [JsonSerializable(typeof(GetFilesInPackageRequest))]
    [JsonSerializable(typeof(GetFilesInPackageResponse))]
    [JsonSerializable(typeof(PrefetchPackageRequest))]
    [JsonSerializable(typeof(PrefetchPackageResponse))]
    [JsonSerializable(typeof(GetPackageVersionsRequest))]
    [JsonSerializable(typeof(GetPackageVersionsResponse))]
    [JsonSerializable(typeof(SetLogLevelRequest))]
    [JsonSerializable(typeof(SetLogLevelResponse))]
    [JsonSerializable(typeof(SetCredentialsRequest))]
    [JsonSerializable(typeof(SetCredentialsResponse))]
    internal partial class PluginJsonContext : JsonSerializerContext
    {
    }
}
