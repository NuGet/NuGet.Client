// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Commands;
using NuGet.Common;

namespace NuGet.Build.Tasks.Pack
{
    public class PackTaskRequest : IPackTaskRequest<IMSBuildItem>
    {
        public string AssemblyName { get; set; }
        public IMSBuildItem[] AssemblyReferences { get; set; }
        public string[] Authors { get; set; }
        public string BuildOutputFolder { get; set; }
        public string[] ContentTargetFolders { get; set; }
        public bool ContinuePackingAfterGeneratingNuspec { get; set; }
        public string Copyright { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public bool IncludeBuildOutput { get; set; }
        public bool IncludeSource { get; set; }
        public bool IncludeSymbols { get; set; }
        public bool IsTool { get; set; }
        public string LicenseUrl { get; set; }
        public ILogger Logger { get; set; }
        public string MinClientVersion { get; set; }
        public bool NoPackageAnalysis { get; set; }
        public string NuspecOutputPath { get; set; }
        public IMSBuildItem[] PackageFiles { get; set; }
        public IMSBuildItem[] PackageFilesToExclude { get; set; }
        public string PackageId { get; set; }
        public string PackageOutputPath { get; set; }
        public string[] PackageTypes { get; set; }
        public string PackageVersion { get; set; }
        public IMSBuildItem PackItem { get; set; }
        public string ProjectUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public string RepositoryType { get; set; }
        public string RepositoryUrl { get; set; }
        public bool RequireLicenseAcceptance { get; set; }
        public string RestoreOutputPath { get; set; }
        public bool Serviceable { get; set; }
        public IMSBuildItem[] SourceFiles { get; set; }
        public string[] Tags { get; set; }
        public string[] TargetFrameworks { get; set; }
        public string[] TargetPathsToAssemblies { get; set; }
        public string[] TargetPathsToSymbols { get; set; }
        public string VersionSuffix { get; set; }
    }
}
