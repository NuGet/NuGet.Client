// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Collection of constants representing project build property names.
    /// </summary>
    public static class ProjectBuildProperties
    {
        public const string MSBuildProjectExtensionsPath = "MSBuildProjectExtensionsPath";
        public const string PackageTargetFallback = "PackageTargetFallback";
        public const string AssetTargetFallback = "AssetTargetFallback";
        public const string PackageVersion = "PackageVersion";
        public const string RestoreProjectStyle = "RestoreProjectStyle";
        public const string RuntimeIdentifier = "RuntimeIdentifier";
        public const string RuntimeIdentifiers = "RuntimeIdentifiers";
        public const string RuntimeSupports = "RuntimeSupports";
        public const string TargetFramework = "TargetFramework";
        public const string TargetFrameworkMoniker = "TargetFrameworkMoniker";
        public const string TargetFrameworks = "TargetFrameworks";
        public const string TargetPlatformIdentifier = "TargetPlatformIdentifier";
        public const string TargetPlatformMinVersion = "TargetPlatformMinVersion";
        public const string TargetPlatformVersion = "TargetPlatformVersion";
        public const string Version = "Version";
        public const string RestorePackagesPath = "RestorePackagesPath";
        public const string RestoreSources = "RestoreSources";
        public const string RestoreFallbackFolders = "RestoreFallbackFolders";
        public const string ProjectTypeGuids = "ProjectTypeGuids";
        public const string RestoreAdditionalProjectSources = nameof(RestoreAdditionalProjectSources);
        public const string RestoreAdditionalProjectFallbackFolders = nameof(RestoreAdditionalProjectFallbackFolders);
        public const string NoWarn = nameof(NoWarn);
        public const string WarningsAsErrors = nameof(WarningsAsErrors);
        public const string TreatWarningsAsErrors = nameof(TreatWarningsAsErrors);
        public const string DotnetCliToolTargetFramework = nameof(DotnetCliToolTargetFramework);
        public const string RestorePackagesWithLockFile = nameof(RestorePackagesWithLockFile);
        public const string NuGetLockFilePath = nameof(NuGetLockFilePath);
        public const string RestoreLockedMode = nameof(RestoreLockedMode);
    }
}
