// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using NuGet.Commands;
using ILogger = NuGet.Common.ILogger;

namespace NuGet.Build.Tasks.Pack
{
    /// <summary>
    /// All of the properties provided by MSBuild to execute pack.
    /// </summary>
    /// <typeparam name="TItem">
    /// The item type. This will either be <see cref="ITaskItem"/> or a <see cref="IMSBuildItem"/>.
    /// </typeparam>
    public interface IPackTaskRequest<TItem>
    {
        string AssemblyName { get; }
        TItem[] AssemblyReferences { get; }
        string[] Authors { get; }
        string BuildOutputFolder { get; }
        string[] ContentTargetFolders { get; }
        bool ContinuePackingAfterGeneratingNuspec { get; }
        string Copyright { get; }
        string Description { get; }
        string IconUrl { get; }
        bool IncludeBuildOutput { get; }
        bool IncludeSource { get; }
        bool IncludeSymbols { get; }
        bool IsTool { get; }
        string LicenseUrl { get; }
        ILogger Logger { get; }
        string MinClientVersion { get; }
        bool NoPackageAnalysis { get; }
        string NuspecOutputPath { get; }
        TItem[] PackageFiles { get; }
        TItem[] PackageFilesToExclude { get; }
        string PackageId { get; }
        string PackageOutputPath { get; }
        string[] PackageTypes { get; }
        string PackageVersion { get; }
        TItem PackItem { get; }
        string ProjectUrl { get; }
        string ReleaseNotes { get; }
        string RepositoryType { get; }
        string RepositoryUrl { get; }
        bool RequireLicenseAcceptance { get; }
        string RestoreOutputPath { get; }
        bool Serviceable { get; }
        TItem[] SourceFiles { get; }
        string[] Tags { get; }
        string[] TargetFrameworks { get; }
        string[] TargetPathsToAssemblies { get; }
        string[] TargetPathsToSymbols { get; }
        string VersionSuffix { get; }
    }
}
