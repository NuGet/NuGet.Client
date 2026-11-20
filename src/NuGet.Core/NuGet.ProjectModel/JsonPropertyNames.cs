// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;

namespace NuGet.ProjectModel
{
    // Keep these names unescaped: the legacy writer adapter uses JsonEncodedText.Value as the property name.
    internal static class JsonPropertyNames
    {
        internal static readonly JsonEncodedText Aliases = JsonEncodedText.Encode("aliases"u8);
        internal static readonly JsonEncodedText AllWarningsAsErrors = JsonEncodedText.Encode("allWarningsAsErrors"u8);
        internal static readonly JsonEncodedText AssetTargetFallback = JsonEncodedText.Encode("assetTargetFallback"u8);
        internal static readonly JsonEncodedText AuditLevel = JsonEncodedText.Encode("auditLevel"u8);
        internal static readonly JsonEncodedText AuditMode = JsonEncodedText.Encode("auditMode"u8);
        internal static readonly JsonEncodedText AutoReferenced = JsonEncodedText.Encode("autoReferenced"u8);
        internal static readonly JsonEncodedText CentralPackageFloatingVersionsEnabled =
            JsonEncodedText.Encode("centralPackageFloatingVersionsEnabled"u8);
        internal static readonly JsonEncodedText CentralPackageVersionOverrideDisabled =
            JsonEncodedText.Encode("centralPackageVersionOverrideDisabled"u8);
        internal static readonly JsonEncodedText CentralPackageVersions = JsonEncodedText.Encode("centralPackageVersions"u8);
        internal static readonly JsonEncodedText CentralPackageVersionsManagementEnabled =
            JsonEncodedText.Encode("centralPackageVersionsManagementEnabled"u8);
        internal static readonly JsonEncodedText CentralPackageTransitivePinningEnabled =
            JsonEncodedText.Encode("CentralPackageTransitivePinningEnabled"u8);
        internal static readonly JsonEncodedText ConfigFilePaths = JsonEncodedText.Encode("configFilePaths"u8);
        internal static readonly JsonEncodedText CrossTargeting = JsonEncodedText.Encode("crossTargeting"u8);
        internal static readonly JsonEncodedText Dependencies = JsonEncodedText.Encode("dependencies"u8);
        internal static readonly JsonEncodedText DownloadDependencies = JsonEncodedText.Encode("downloadDependencies"u8);
        internal static readonly JsonEncodedText EnableAudit = JsonEncodedText.Encode("enableAudit"u8);
        internal static readonly JsonEncodedText ExcludeAssets = JsonEncodedText.Encode("excludeAssets"u8);
        internal static readonly JsonEncodedText FallbackFolders = JsonEncodedText.Encode("fallbackFolders"u8);
        internal static readonly JsonEncodedText Files = JsonEncodedText.Encode("files"u8);
        internal static readonly JsonEncodedText Format = JsonEncodedText.Encode("format"u8);
        internal static readonly JsonEncodedText Framework = JsonEncodedText.Encode("framework"u8);
        internal static readonly JsonEncodedText FrameworkAssemblies = JsonEncodedText.Encode("frameworkAssemblies"u8);
        internal static readonly JsonEncodedText FrameworkReferences = JsonEncodedText.Encode("frameworkReferences"u8);
        internal static readonly JsonEncodedText Frameworks = JsonEncodedText.Encode("frameworks"u8);
        internal static readonly JsonEncodedText GeneratePathProperty = JsonEncodedText.Encode("generatePathProperty"u8);
        internal static readonly JsonEncodedText Imports = JsonEncodedText.Encode("imports"u8);
        internal static readonly JsonEncodedText Include = JsonEncodedText.Encode("include"u8);
        internal static readonly JsonEncodedText IncludeAssets = JsonEncodedText.Encode("includeAssets"u8);
        internal static readonly JsonEncodedText LegacyPackagesDirectory = JsonEncodedText.Encode("legacyPackagesDirectory"u8);
        internal static readonly JsonEncodedText Name = JsonEncodedText.Encode("name"u8);
        internal static readonly JsonEncodedText NoWarn = JsonEncodedText.Encode("noWarn"u8);
        internal static readonly JsonEncodedText NuGetLockFilePath = JsonEncodedText.Encode("nuGetLockFilePath"u8);
        internal static readonly JsonEncodedText OriginalTargetFrameworks = JsonEncodedText.Encode("originalTargetFrameworks"u8);
        internal static readonly JsonEncodedText OutputPath = JsonEncodedText.Encode("outputPath"u8);
        internal static readonly JsonEncodedText PackagesConfigPath = JsonEncodedText.Encode("packagesConfigPath"u8);
        internal static readonly JsonEncodedText PackagesPath = JsonEncodedText.Encode("packagesPath"u8);
        internal static readonly JsonEncodedText PackagesToPrune = JsonEncodedText.Encode("packagesToPrune"u8);
        internal static readonly JsonEncodedText PrivateAssets = JsonEncodedText.Encode("privateAssets"u8);
        internal static readonly JsonEncodedText ProjectJsonPath = JsonEncodedText.Encode("projectJsonPath"u8);
        internal static readonly JsonEncodedText ProjectName = JsonEncodedText.Encode("projectName"u8);
        internal static readonly JsonEncodedText ProjectPath = JsonEncodedText.Encode("projectPath"u8);
        internal static readonly JsonEncodedText ProjectReferences = JsonEncodedText.Encode("projectReferences"u8);
        internal static readonly JsonEncodedText Projects = JsonEncodedText.Encode("projects"u8);
        internal static readonly JsonEncodedText ProjectStyle = JsonEncodedText.Encode("projectStyle"u8);
        internal static readonly JsonEncodedText ProjectUniqueName = JsonEncodedText.Encode("projectUniqueName"u8);
        internal static readonly JsonEncodedText Restore = JsonEncodedText.Encode("restore"u8);
        internal static readonly JsonEncodedText RestoreAuditProperties = JsonEncodedText.Encode("restoreAuditProperties"u8);
        internal static readonly JsonEncodedText RestoreDoNotWriteDependencyGraphSpec =
            JsonEncodedText.Encode("restoreDoNotWriteDependencyGraphSpec"u8);
        internal static readonly JsonEncodedText RestoreEnableAnalyzerAssets =
            JsonEncodedText.Encode("restoreEnableAnalyzerAssets"u8);
        internal static readonly JsonEncodedText RestoreLockedMode = JsonEncodedText.Encode("restoreLockedMode"u8);
        internal static readonly JsonEncodedText RestoreLockProperties = JsonEncodedText.Encode("restoreLockProperties"u8);
        internal static readonly JsonEncodedText RestorePackagesWithLockFile =
            JsonEncodedText.Encode("restorePackagesWithLockFile"u8);
        internal static readonly JsonEncodedText RestoreUseLegacyDependencyResolver =
            JsonEncodedText.Encode("restoreUseLegacyDependencyResolver"u8);
        internal static readonly JsonEncodedText RuntimeIdentifierGraphPath =
            JsonEncodedText.Encode("runtimeIdentifierGraphPath"u8);
        internal static readonly JsonEncodedText SdkAnalysisLevel = JsonEncodedText.Encode("SdkAnalysisLevel"u8);
        internal static readonly JsonEncodedText SecondaryFramework = JsonEncodedText.Encode("secondaryFramework"u8);
        internal static readonly JsonEncodedText SkipContentFileWrite = JsonEncodedText.Encode("skipContentFileWrite"u8);
        internal static readonly JsonEncodedText Sources = JsonEncodedText.Encode("sources"u8);
        internal static readonly JsonEncodedText SuppressedAdvisories = JsonEncodedText.Encode("suppressedAdvisories"u8);
        internal static readonly JsonEncodedText SuppressParent = JsonEncodedText.Encode("suppressParent"u8);
        internal static readonly JsonEncodedText Target = JsonEncodedText.Encode("target"u8);
        internal static readonly JsonEncodedText TargetAlias = JsonEncodedText.Encode("targetAlias"u8);
        internal static readonly JsonEncodedText UsingMicrosoftNETSdk = JsonEncodedText.Encode("UsingMicrosoftNETSdk"u8);
        internal static readonly JsonEncodedText ValidateRuntimeAssets = JsonEncodedText.Encode("validateRuntimeAssets"u8);
        internal static readonly JsonEncodedText Version = JsonEncodedText.Encode("version"u8);
        internal static readonly JsonEncodedText VersionCentrallyManaged =
            JsonEncodedText.Encode("versionCentrallyManaged"u8);
        internal static readonly JsonEncodedText VersionOverride = JsonEncodedText.Encode("versionOverride"u8);
        internal static readonly JsonEncodedText Warn = JsonEncodedText.Encode("warn"u8);
        internal static readonly JsonEncodedText WarnAsError = JsonEncodedText.Encode("warnAsError"u8);
        internal static readonly JsonEncodedText WarningProperties = JsonEncodedText.Encode("warningProperties"u8);
        internal static readonly JsonEncodedText WarnNotAsError = JsonEncodedText.Encode("warnNotAsError"u8);
    }
}
