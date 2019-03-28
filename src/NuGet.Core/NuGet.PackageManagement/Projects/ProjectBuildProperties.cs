// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Collection of constants representing project build property names.
    /// </summary>
    public static class ProjectBuildProperties
    {
        public const string MSBuildProjectExtensionsPath = nameof(MSBuildProjectExtensionsPath);
        public const string PackageTargetFallback = nameof(PackageTargetFallback);
        public const string AssetTargetFallback = nameof(AssetTargetFallback);
        public const string PackageVersion = nameof(PackageVersion);
        public const string RestoreProjectStyle = nameof(RestoreProjectStyle);
        public const string RuntimeIdentifier = nameof(RuntimeIdentifier);
        public const string RuntimeIdentifiers = nameof(RuntimeIdentifiers);
        public const string RuntimeSupports = nameof(RuntimeSupports);
        public const string TargetFramework = nameof(TargetFramework);
        public const string TargetFrameworkMoniker = nameof(TargetFrameworkMoniker);
        public const string TargetFrameworks = nameof(TargetFrameworks);
        public const string TargetPlatformIdentifier = nameof(TargetPlatformIdentifier);
        public const string TargetPlatformMinVersion = nameof(TargetPlatformMinVersion);
        public const string TargetPlatformVersion = nameof(TargetPlatformVersion);
        public const string Version = nameof(Version);
        public const string RestorePackagesPath = nameof(RestorePackagesPath);
        public const string RestoreSources = nameof(RestoreSources);
        public const string RestoreFallbackFolders = nameof(RestoreFallbackFolders);
        public const string ProjectTypeGuids = nameof(ProjectTypeGuids);
        public const string RestoreAdditionalProjectSources = nameof(RestoreAdditionalProjectSources);
        public const string RestoreAdditionalProjectFallbackFolders = nameof(RestoreAdditionalProjectFallbackFolders);
        public const string RestoreAdditionalProjectFallbackFoldersExcludes = nameof(RestoreAdditionalProjectFallbackFoldersExcludes);
        public const string NoWarn = nameof(NoWarn);
        public const string WarningsAsErrors = nameof(WarningsAsErrors);
        public const string TreatWarningsAsErrors = nameof(TreatWarningsAsErrors);
        public const string DotnetCliToolTargetFramework = nameof(DotnetCliToolTargetFramework);
        public const string RestorePackagesWithLockFile = nameof(RestorePackagesWithLockFile);
        public const string NuGetLockFilePath = nameof(NuGetLockFilePath);
        public const string RestoreLockedMode = nameof(RestoreLockedMode);
        public const string PackageId = nameof(PackageId);
        public const string IncludeAssets = nameof(IncludeAssets);
        public const string ExcludeAssets = nameof(ExcludeAssets);
        public const string PrivateAssets = nameof(PrivateAssets);
        public const string ReferenceOutputAssembly = nameof(ReferenceOutputAssembly);
        public const string Clear = nameof(Clear);
    }
}
