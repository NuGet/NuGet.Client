// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    internal static class Utf8JsonStreamPackageSpecReader
    {
        private static readonly char[] VersionSeparators = new[] { ';' };
        private static readonly byte[] Utf8Authors = Encoding.UTF8.GetBytes("authors");
        private static readonly byte[] BuildOptionsUtf8 = Encoding.UTF8.GetBytes("buildOptions");
        private static readonly byte[] Utf8ContentFiles = Encoding.UTF8.GetBytes("contentFiles");
        private static readonly byte[] Utf8Copyright = Encoding.UTF8.GetBytes("copyright");
        private static readonly byte[] Utf8Dependencies = Encoding.UTF8.GetBytes("dependencies");
        private static readonly byte[] Utf8Description = Encoding.UTF8.GetBytes("description");
        private static readonly byte[] Utf8Language = Encoding.UTF8.GetBytes("language");
        private static readonly byte[] Utf8PackInclude = Encoding.UTF8.GetBytes("packInclude");
        private static readonly byte[] Utf8PackOptions = Encoding.UTF8.GetBytes("packOptions");
        private static readonly byte[] Utf8Scripts = Encoding.UTF8.GetBytes("scripts");
        private static readonly byte[] Utf8Frameworks = Encoding.UTF8.GetBytes("frameworks");
        private static readonly byte[] Utf8Restore = Encoding.UTF8.GetBytes("restore");
        private static readonly byte[] Utf8Runtimes = Encoding.UTF8.GetBytes("runtimes");
        private static readonly byte[] Utf8Supports = Encoding.UTF8.GetBytes("supports");
        private static readonly byte[] Utf8Title = Encoding.UTF8.GetBytes("title");
        private static readonly byte[] Utf8Version = Encoding.UTF8.GetBytes("version");
        private static readonly byte[] Utf8OutputName = Encoding.UTF8.GetBytes("outputName");
        private static readonly byte[] Utf8AutoReferenced = Encoding.UTF8.GetBytes("autoReferenced");
        private static readonly byte[] Utf8Exclude = Encoding.UTF8.GetBytes("exclude");
        private static readonly byte[] Utf8GeneratePathProperty = Encoding.UTF8.GetBytes("generatePathProperty");
        private static readonly byte[] Utf8Include = Encoding.UTF8.GetBytes("include");
        private static readonly byte[] Utf8NoWarn = Encoding.UTF8.GetBytes("noWarn");
        private static readonly byte[] Utf8SuppressParent = Encoding.UTF8.GetBytes("suppressParent");
        private static readonly byte[] Utf8Target = Encoding.UTF8.GetBytes("target");
        private static readonly byte[] Utf8VersionOverride = Encoding.UTF8.GetBytes("versionOverride");
        private static readonly byte[] Utf8VersionCentrallyManaged = Encoding.UTF8.GetBytes("versionCentrallyManaged");
        private static readonly byte[] Utf8Aliases = Encoding.UTF8.GetBytes("aliases");
        private static readonly byte[] Utf8Name = Encoding.UTF8.GetBytes("name");
        private static readonly byte[] Utf8PrivateAssets = Encoding.UTF8.GetBytes("privateAssets");
        private static readonly byte[] Utf8ExcludeFiles = Encoding.UTF8.GetBytes("excludeFiles");
        private static readonly byte[] Utf8IncludeFiles = Encoding.UTF8.GetBytes("includeFiles");
        private static readonly byte[] Utf8CentralPackageVersionsManagementEnabled = Encoding.UTF8.GetBytes("centralPackageVersionsManagementEnabled");
        private static readonly byte[] Utf8CentralPackageVersionOverrideDisabled = Encoding.UTF8.GetBytes("centralPackageVersionOverrideDisabled");
        private static readonly byte[] Utf8CentralPackageTransitivePinningEnabled = Encoding.UTF8.GetBytes("CentralPackageTransitivePinningEnabled");
        private static readonly byte[] Utf8ConfigFilePaths = Encoding.UTF8.GetBytes("configFilePaths");
        private static readonly byte[] Utf8CrossTargeting = Encoding.UTF8.GetBytes("crossTargeting");
        private static readonly byte[] Utf8FallbackFolders = Encoding.UTF8.GetBytes("fallbackFolders");
        private static readonly byte[] Utf8Files = Encoding.UTF8.GetBytes("files");
        private static readonly byte[] Utf8LegacyPackagesDirectory = Encoding.UTF8.GetBytes("legacyPackagesDirectory");
        private static readonly byte[] Utf8OriginalTargetFrameworks = Encoding.UTF8.GetBytes("originalTargetFrameworks");
        private static readonly byte[] Utf8OutputPath = Encoding.UTF8.GetBytes("outputPath");
        private static readonly byte[] Utf8PackagesConfigPath = Encoding.UTF8.GetBytes("packagesConfigPath");
        private static readonly byte[] Utf8PackagesPath = Encoding.UTF8.GetBytes("packagesPath");
        private static readonly byte[] Utf8ProjectJsonPath = Encoding.UTF8.GetBytes("projectJsonPath");
        private static readonly byte[] Utf8ProjectName = Encoding.UTF8.GetBytes("projectName");
        private static readonly byte[] Utf8ProjectPath = Encoding.UTF8.GetBytes("projectPath");
        private static readonly byte[] Utf8ProjectStyle = Encoding.UTF8.GetBytes("projectStyle");
        private static readonly byte[] Utf8ProjectUniqueName = Encoding.UTF8.GetBytes("projectUniqueName");
        private static readonly byte[] Utf8RestoreLockProperties = Encoding.UTF8.GetBytes("restoreLockProperties");
        private static readonly byte[] Utf8NuGetLockFilePath = Encoding.UTF8.GetBytes("nuGetLockFilePath");
        private static readonly byte[] Utf8RestoreLockedMode = Encoding.UTF8.GetBytes("restoreLockedMode");
        private static readonly byte[] Utf8RestorePackagesWithLockFile = Encoding.UTF8.GetBytes("restorePackagesWithLockFile");
        private static readonly byte[] Utf8RestoreAuditProperties = Encoding.UTF8.GetBytes("restoreAuditProperties");
        private static readonly byte[] Utf8EnableAudit = Encoding.UTF8.GetBytes("enableAudit");
        private static readonly byte[] Utf8AuditLevel = Encoding.UTF8.GetBytes("auditLevel");
        private static readonly byte[] Utf8AuditMode = Encoding.UTF8.GetBytes("auditMode");
        private static readonly byte[] Utf8SkipContentFileWrite = Encoding.UTF8.GetBytes("skipContentFileWrite");
        private static readonly byte[] Utf8Sources = Encoding.UTF8.GetBytes("sources");
        private static readonly byte[] Utf8ValidateRuntimeAssets = Encoding.UTF8.GetBytes("validateRuntimeAssets");
        private static readonly byte[] Utf8WarningProperties = Encoding.UTF8.GetBytes("warningProperties");
        private static readonly byte[] Utf8AllWarningsAsErrors = Encoding.UTF8.GetBytes("allWarningsAsErrors");
        private static readonly byte[] Utf8WarnAsError = Encoding.UTF8.GetBytes("warnAsError");
        private static readonly byte[] Utf8WarnNotAsError = Encoding.UTF8.GetBytes("warnNotAsError");
        private static readonly byte[] Utf8ExcludeAssets = Encoding.UTF8.GetBytes("excludeAssets");
        private static readonly byte[] Utf8IncludeAssets = Encoding.UTF8.GetBytes("includeAssets");
        private static readonly byte[] Utf8TargetAlias = Encoding.UTF8.GetBytes("targetAlias");
        private static readonly byte[] Utf8AssetTargetFallback = Encoding.UTF8.GetBytes("assetTargetFallback");
        private static readonly byte[] Utf8SecondaryFramework = Encoding.UTF8.GetBytes("secondaryFramework");
        private static readonly byte[] Utf8CentralPackageVersions = Encoding.UTF8.GetBytes("centralPackageVersions");
        private static readonly byte[] Utf8DownloadDependencies = Encoding.UTF8.GetBytes("downloadDependencies");
        private static readonly byte[] Utf8FrameworkAssemblies = Encoding.UTF8.GetBytes("frameworkAssemblies");
        private static readonly byte[] Utf8FrameworkReferences = Encoding.UTF8.GetBytes("frameworkReferences");
        private static readonly byte[] Utf8Imports = Encoding.UTF8.GetBytes("imports");
        private static readonly byte[] Utf8RuntimeIdentifierGraphPath = Encoding.UTF8.GetBytes("runtimeIdentifierGraphPath");
        private static readonly byte[] Utf8Warn = Encoding.UTF8.GetBytes("warn");
        private static readonly byte[] Utf8IconUrl = Encoding.UTF8.GetBytes("iconUrl");
        private static readonly byte[] Utf8LicenseUrl = Encoding.UTF8.GetBytes("licenseUrl");
        private static readonly byte[] Utf8Owners = Encoding.UTF8.GetBytes("owners");
        private static readonly byte[] Utf8PackageType = Encoding.UTF8.GetBytes("packageType");
        private static readonly byte[] Utf8ProjectUrl = Encoding.UTF8.GetBytes("projectUrl");
        private static readonly byte[] Utf8ReleaseNotes = Encoding.UTF8.GetBytes("releaseNotes");
        private static readonly byte[] Utf8RequireLicenseAcceptance = Encoding.UTF8.GetBytes("requireLicenseAcceptance");
        private static readonly byte[] Utf8Summary = Encoding.UTF8.GetBytes("summary");
        private static readonly byte[] Utf8Tags = Encoding.UTF8.GetBytes("tags");
        private static readonly byte[] Utf8Mappings = Encoding.UTF8.GetBytes("mappings");
        private static readonly byte[] Utf8HashTagImport = Encoding.UTF8.GetBytes("#import");
        private static readonly byte[] Utf8ProjectReferences = Encoding.UTF8.GetBytes("projectReferences");
        private static readonly byte[] Utf8EmptyString = Encoding.UTF8.GetBytes(string.Empty);

        internal static PackageSpec GetPackageSpec(string json, string name, string packageSpecPath, string snapshotValue = null)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return GetPackageSpec(stream, name, packageSpecPath, snapshotValue);
            }
        }

        internal static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue)
        {
            var reader = new Utf8JsonStreamReader(stream);
            PackageSpec packageSpec;
            try
            {
                packageSpec = GetPackageSpec(ref reader, name, packageSpecPath, snapshotValue);
            }
            catch (JsonException innerException)
            {
                throw FileFormatException.Create(innerException, packageSpecPath);
            }

            if (!string.IsNullOrEmpty(name))
            {
                packageSpec.Name = name;
                if (!string.IsNullOrEmpty(packageSpecPath))
                {
                    packageSpec.FilePath = Path.GetFullPath(packageSpecPath);

                }
            }
            return packageSpec;
        }

        internal static PackageSpec GetPackageSpec(ref Utf8JsonStreamReader jsonReader, string name, string packageSpecPath, string snapshotValue)
        {
            var packageSpec = new PackageSpec();

            List<CompatibilityProfile> compatibilityProfiles = null;
            List<RuntimeDescription> runtimeDescriptions = null;
            var wasPackOptionsSet = false;
            var isMappingsNull = false;
            string filePath = name == null ? null : Path.GetFullPath(packageSpecPath);

            if (jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals(Utf8EmptyString))
                    {
                        jsonReader.Skip();
                    }
#pragma warning disable CS0612 // Type or member is obsolete
                    else if (jsonReader.ValueTextEquals(Utf8Authors))
                    {
                        jsonReader.Read();
                        if (jsonReader.TokenType == JsonTokenType.StartArray)
                        {
                            packageSpec.Authors = jsonReader.ReadStringArrayAsIList()?.ToArray();
                        }
                        if (packageSpec.Authors == null)
                        {
                            packageSpec.Authors = Array.Empty<string>();
                        }
                    }
                    else if (jsonReader.ValueTextEquals(BuildOptionsUtf8))
                    {
                        ReadBuildOptions(ref jsonReader, packageSpec);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ContentFiles))
                    {
                        jsonReader.Read();
                        jsonReader.ReadStringArrayAsIList(packageSpec.ContentFiles);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Copyright))
                    {
                        packageSpec.Copyright = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Description))
                    {
                        packageSpec.Description = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Language))
                    {
                        packageSpec.Language = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8PackInclude))
                    {
                        ReadPackInclude(ref jsonReader, packageSpec);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8PackOptions))
                    {
                        ReadPackOptions(ref jsonReader, packageSpec, ref isMappingsNull);
                        wasPackOptionsSet = true;
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Scripts))
                    {
                        ReadScripts(ref jsonReader, packageSpec);
                    }
#pragma warning restore CS0612 // Type or member is 
                    else if (jsonReader.ValueTextEquals(Utf8Dependencies))
                    {
                        ReadDependencies(
                            ref jsonReader,
                            packageSpec.Dependencies,
                            filePath,
                            isGacOrFrameworkReference: false);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Frameworks))
                    {
                        ReadFrameworks(ref jsonReader, packageSpec);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Restore))
                    {
                        ReadMSBuildMetadata(ref jsonReader, packageSpec);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Runtimes))
                    {
                        runtimeDescriptions = ReadRuntimes(ref jsonReader);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Supports))
                    {
                        compatibilityProfiles = ReadSupports(ref jsonReader);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Title))
                    {
                        packageSpec.Title = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Version))
                    {
                        string version = jsonReader.ReadNextTokenAsString();
                        if (version != null)
                        {
                            try
                            {
#pragma warning disable CS0612 // Type or member is obsolete
                                packageSpec.HasVersionSnapshot = PackageSpecUtility.IsSnapshotVersion(version);
#pragma warning restore CS0612 // Type or member is obsolete
                                packageSpec.Version = PackageSpecUtility.SpecifySnapshot(version, snapshotValue);
                            }
                            catch (Exception ex)
                            {
                                throw new JsonException($"Error processing versino property. version: {version}", innerException: ex);
                            }
                        }
                    }
                    else
                    {
                        jsonReader.Skip();
                    }
                }
            }
            packageSpec.Name = name;
            packageSpec.FilePath = name == null ? null : Path.GetFullPath(packageSpecPath);

#pragma warning disable CS0612 // Type or member is obsolete
            if (!wasPackOptionsSet)
            {
                packageSpec.Owners = Array.Empty<string>();
                packageSpec.PackOptions = new PackOptions()
                {
                    PackageType = Array.Empty<PackageType>()
                };
                packageSpec.Tags = Array.Empty<string>();
            }

            if (isMappingsNull)
            {
                packageSpec.PackOptions.Mappings = null;
            }
#pragma warning restore CS0612 // Type or member is obsolete

            packageSpec.RuntimeGraph = new RuntimeGraph(
                runtimeDescriptions ?? Enumerable.Empty<RuntimeDescription>(),
                compatibilityProfiles ?? Enumerable.Empty<CompatibilityProfile>());

            if (packageSpec.Name == null)
            {
                packageSpec.Name = packageSpec.RestoreMetadata?.ProjectName;
            }

            // Use the project.json path if one is set, otherwise use the project path
            if (packageSpec.FilePath == null)
            {
                packageSpec.FilePath = packageSpec.RestoreMetadata?.ProjectJsonPath
                    ?? packageSpec.RestoreMetadata?.ProjectPath;
            }

            return packageSpec;
        }

        internal static void ReadCentralTransitiveDependencyGroup(
            ref Utf8JsonStreamReader jsonReader,
            IList<LibraryDependency> results,
            string packageSpecPath)
        {
            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    if (string.IsNullOrEmpty(propertyName))
                    {
                        var exception = new JsonException("Unable to resolve dependency ''.");
                        exception.Data.Add(FileFormatException.SurfaceMessage, true);
                        throw exception;
                    }

                    if (jsonReader.Read())
                    {
                        var dependencyIncludeFlagsValue = LibraryIncludeFlags.All;
                        var dependencyExcludeFlagsValue = LibraryIncludeFlags.None;
                        var suppressParentFlagsValue = LibraryIncludeFlagUtils.DefaultSuppressParent;
                        string dependencyVersionValue = null;

                        if (jsonReader.TokenType == JsonTokenType.String)
                        {
                            dependencyVersionValue = jsonReader.GetString();
                        }
                        else if (jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                IEnumerable<string> values = null;

                                if (jsonReader.ValueTextEquals(Utf8Exclude))
                                {
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Include))
                                {
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8SuppressParent))
                                {
                                    values = jsonReader.ReadDelimitedString();
                                    suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Version))
                                {
                                    if (jsonReader.Read())
                                    {
                                        dependencyVersionValue = jsonReader.GetString();
                                    }
                                }
                                else
                                {
                                    jsonReader.Skip();
                                }
                            }
                        }

                        VersionRange dependencyVersionRange = null;

                        if (!string.IsNullOrEmpty(dependencyVersionValue))
                        {
                            try
                            {
                                dependencyVersionRange = VersionRange.Parse(dependencyVersionValue);
                            }
                            catch (Exception ex)
                            {
                                throw new JsonException(null, ex);
                            }
                        }

                        if (dependencyVersionRange == null)
                        {
                            throw new JsonException(Strings.MissingVersionOnDependency, new ArgumentException(Strings.MissingVersionOnDependency));
                        }

                        // the dependency flags are: Include flags - Exclude flags
                        var includeFlags = dependencyIncludeFlagsValue & ~dependencyExcludeFlagsValue;
                        var libraryDependency = new LibraryDependency()
                        {
                            LibraryRange = new LibraryRange()
                            {
                                Name = propertyName,
                                TypeConstraint = LibraryDependencyTarget.Package,
                                VersionRange = dependencyVersionRange
                            },

                            IncludeType = includeFlags,
                            SuppressParent = suppressParentFlagsValue,
                            VersionCentrallyManaged = true,
                            ReferenceType = LibraryDependencyReferenceType.Transitive
                        };

                        results.Add(libraryDependency);
                    }
                }
            }
        }

        private static void ReadDependencies(
            ref Utf8JsonStreamReader jsonReader,
            IList<LibraryDependency> results,
            string packageSpecPath,
            bool isGacOrFrameworkReference)
        {
            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    if (string.IsNullOrEmpty(propertyName))
                    {
                        var exception = new JsonException("Unable to resolve dependency ''.");
                        exception.Data.Add(FileFormatException.SurfaceMessage, true);
                        throw exception;
                    }

                    // Support
                    // "dependencies" : {
                    //    "Name" : "1.0"
                    // }

                    if (jsonReader.Read())
                    {
                        var dependencyIncludeFlagsValue = LibraryIncludeFlags.All;
                        var dependencyExcludeFlagsValue = LibraryIncludeFlags.None;
                        var suppressParentFlagsValue = LibraryIncludeFlagUtils.DefaultSuppressParent;
                        List<NuGetLogCode> noWarn = null;

                        // This method handles both the dependencies and framework assembly sections.
                        // Framework references should be limited to references.
                        // Dependencies should allow everything but framework references.
                        LibraryDependencyTarget targetFlagsValue = isGacOrFrameworkReference
                            ? LibraryDependencyTarget.Reference
                            : LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference;

                        var autoReferenced = false;
                        var generatePathProperty = false;
                        var versionCentrallyManaged = false;
                        string aliases = null;
                        string dependencyVersionValue = null;
                        VersionRange versionOverride = null;

                        if (jsonReader.TokenType == JsonTokenType.String)
                        {
                            dependencyVersionValue = jsonReader.GetString();
                        }
                        else if (jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                IEnumerable<string> values = null;
                                if (jsonReader.ValueTextEquals(Utf8AutoReferenced))
                                {
                                    autoReferenced = jsonReader.ReadNextTokenAsBoolOrFalse();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Exclude))
                                {
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8GeneratePathProperty))
                                {
                                    generatePathProperty = jsonReader.ReadNextTokenAsBoolOrFalse();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Include))
                                {
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8NoWarn))
                                {
                                    noWarn = ReadNuGetLogCodesList(ref jsonReader);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8SuppressParent))
                                {
                                    values = jsonReader.ReadDelimitedString();
                                    suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Target))
                                {
                                    targetFlagsValue = ReadTarget(ref jsonReader, packageSpecPath, targetFlagsValue);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Version))
                                {
                                    if (jsonReader.Read())
                                    {
                                        dependencyVersionValue = jsonReader.GetString();
                                    }
                                }
                                else if (jsonReader.ValueTextEquals(Utf8VersionOverride))
                                {
                                    if (jsonReader.Read())
                                    {
                                        var versionPropValue = jsonReader.GetString();
                                        try
                                        {
                                            versionOverride = VersionRange.Parse(versionPropValue);
                                        }
                                        catch (Exception ex)
                                        {
                                            throw new JsonException(ex.Message, innerException: ex);
                                        }
                                    }
                                }
                                else if (jsonReader.ValueTextEquals(Utf8VersionCentrallyManaged))
                                {
                                    versionCentrallyManaged = jsonReader.ReadNextTokenAsBoolOrFalse();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Aliases))
                                {
                                    aliases = jsonReader.ReadNextTokenAsString();
                                }
                                else
                                {
                                    jsonReader.Skip();
                                }
                            }
                        }

                        VersionRange dependencyVersionRange = null;

                        if (!string.IsNullOrEmpty(dependencyVersionValue))
                        {
                            try
                            {
                                dependencyVersionRange = VersionRange.Parse(dependencyVersionValue);
                            }
                            catch (Exception ex)
                            {
                                throw new JsonException(ex.Message, ex);
                            }
                        }

                        // Projects and References may have empty version ranges, Packages may not
                        if (dependencyVersionRange == null)
                        {
                            if ((targetFlagsValue & LibraryDependencyTarget.Package) == LibraryDependencyTarget.Package)
                            {
                                throw new JsonException(Strings.MissingVersionOnDependency, new ArgumentException(Strings.MissingVersionOnDependency));
                            }
                            else
                            {
                                // Projects and references with no version property allow all versions
                                dependencyVersionRange = VersionRange.All;
                            }
                        }

                        // the dependency flags are: Include flags - Exclude flags
                        var includeFlags = dependencyIncludeFlagsValue & ~dependencyExcludeFlagsValue;
                        var libraryDependency = new LibraryDependency()
                        {
                            LibraryRange = new LibraryRange()
                            {
                                Name = propertyName,
                                TypeConstraint = targetFlagsValue,
                                VersionRange = dependencyVersionRange
                            },
                            IncludeType = includeFlags,
                            SuppressParent = suppressParentFlagsValue,
                            AutoReferenced = autoReferenced,
                            GeneratePathProperty = generatePathProperty,
                            VersionCentrallyManaged = versionCentrallyManaged,
                            Aliases = aliases,
                            // The ReferenceType is not persisted to the assets file
                            // Default to LibraryDependencyReferenceType.Direct on Read
                            ReferenceType = LibraryDependencyReferenceType.Direct,
                            VersionOverride = versionOverride
                        };

                        if (noWarn != null)
                        {
                            libraryDependency.NoWarn = noWarn;
                        }

                        results.Add(libraryDependency);
                    }
                }
            }
        }

        private static PackageType CreatePackageType(ref Utf8JsonStreamReader jsonReader)
        {
            var name = jsonReader.GetString();

            return new PackageType(name, Packaging.Core.PackageType.EmptyVersion);
        }

        [Obsolete]
        private static void ReadBuildOptions(ref Utf8JsonStreamReader jsonReader, PackageSpec packageSpec)
        {
            packageSpec.BuildOptions = new BuildOptions();

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals(Utf8OutputName))
                    {
                        packageSpec.BuildOptions.OutputName = jsonReader.ReadNextTokenAsString();
                    }
                    else
                    {
                        jsonReader.Skip();
                    }
                }
            }
        }

        private static void ReadCentralPackageVersions(
            ref Utf8JsonStreamReader jsonReader,
            IDictionary<string, CentralPackageVersion> centralPackageVersions)
        {
            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();

                    if (string.IsNullOrEmpty(propertyName))
                    {
                        var exception = new JsonException("Unable to resolve central version ''.");
                        throw exception;
                    }

                    string version = jsonReader.ReadNextTokenAsString();

                    if (string.IsNullOrEmpty(version))
                    {
                        var exception = new JsonException("The version cannot be null or empty.");
                        throw exception;
                    }

                    centralPackageVersions[propertyName] = new CentralPackageVersion(propertyName, VersionRange.Parse(version));
                }
            }
        }

        private static CompatibilityProfile ReadCompatibilityProfile(ref Utf8JsonStreamReader jsonReader, string profileName)
        {
            List<FrameworkRuntimePair> sets = null;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    sets = sets ?? new List<FrameworkRuntimePair>();

                    IReadOnlyList<string> values = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList() ?? Array.Empty<string>();

                    IEnumerable<FrameworkRuntimePair> profiles = ReadCompatibilitySets(values, propertyName);

                    sets.AddRange(profiles);
                }
            }
            return new CompatibilityProfile(profileName, sets ?? Enumerable.Empty<FrameworkRuntimePair>());
        }

        private static IEnumerable<FrameworkRuntimePair> ReadCompatibilitySets(IReadOnlyList<string> values, string compatibilitySetName)
        {
            NuGetFramework framework = NuGetFramework.Parse(compatibilitySetName);

            foreach (string value in values)
            {
                yield return new FrameworkRuntimePair(framework, value);
            }
        }

        private static void ReadDownloadDependencies(
            ref Utf8JsonStreamReader jsonReader,
            IList<DownloadDependency> downloadDependencies,
            string packageSpecPath)
        {
            var seenIds = new HashSet<string>();

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartArray)
            {
                do
                {
                    string name = null;
                    string versionValue = null;
                    var isNameDefined = false;

                    if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (jsonReader.ValueTextEquals(Utf8Name))
                            {
                                isNameDefined = true;
                                name = jsonReader.ReadNextTokenAsString();
                            }
                            else if (jsonReader.ValueTextEquals(Utf8Version))
                            {
                                versionValue = jsonReader.ReadNextTokenAsString();
                            }
                            else
                            {
                                jsonReader.Skip();
                            }
                        }
                    }

                    if (jsonReader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }

                    if (!isNameDefined)
                    {
                        var exception = new JsonException("Unable to resolve downloadDependency ''.");
                        throw exception;
                    }

                    if (!seenIds.Add(name))
                    {
                        // package ID already seen, only use first definition.
                        continue;
                    }

                    if (string.IsNullOrEmpty(versionValue))
                    {
                        var exception = new JsonException("The version cannot be null or empty");
                        throw exception;
                    }

                    string[] versions = versionValue.Split(VersionSeparators, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string singleVersionValue in versions)
                    {
                        try
                        {
                            VersionRange version = VersionRange.Parse(singleVersionValue);

                            downloadDependencies.Add(new DownloadDependency(name, version));
                        }
                        catch (Exception ex)
                        {
                            throw new JsonException(ex.Message, ex);
                        }
                    }
                } while (jsonReader.TokenType == JsonTokenType.EndObject);
            }
        }

        private static void ReadFrameworkReferences(
            ref Utf8JsonStreamReader jsonReader,
            ISet<FrameworkDependency> frameworkReferences,
            string packageSpecPath)
        {
            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var frameworkName = jsonReader.GetString();
                    if (string.IsNullOrEmpty(frameworkName))
                    {
                        var exception = new JsonException("Unable to resolve frameworkReference.");
                        throw exception;
                    }

                    var privateAssets = FrameworkDependencyFlagsUtils.Default;

                    if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (jsonReader.ValueTextEquals(Utf8PrivateAssets))
                            {
                                IEnumerable<string> strings = jsonReader.ReadDelimitedString();

                                privateAssets = FrameworkDependencyFlagsUtils.GetFlags(strings);
                            }
                            else
                            {
                                jsonReader.Skip();
                            }
                        }
                    }

                    frameworkReferences.Add(new FrameworkDependency(frameworkName, privateAssets));
                }
            }
        }

        private static void ReadFrameworks(ref Utf8JsonStreamReader reader, PackageSpec packageSpec)
        {
            if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    ReadTargetFrameworks(packageSpec, ref reader);
                }
            }
        }

        private static void ReadImports(PackageSpec packageSpec, ref Utf8JsonStreamReader jsonReader, TargetFrameworkInformation targetFrameworkInformation)
        {
            IReadOnlyList<string> imports = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();

            if (imports != null && imports.Count > 0)
            {
                foreach (string import in imports.Where(element => !string.IsNullOrEmpty(element)))
                {
                    NuGetFramework framework = NuGetFramework.Parse(import);

                    if (!framework.IsSpecificFramework)
                    {
                        var exception = new JsonException(string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Log_InvalidImportFramework,
                                import,
                                PackageSpec.PackageSpecFileName));
                        throw exception;
                    }

                    targetFrameworkInformation.Imports.Add(framework);
                }
            }
        }

        private static void ReadMappings(ref Utf8JsonStreamReader jsonReader, string mappingKey, IDictionary<string, IncludeExcludeFiles> mappings)
        {
            if (jsonReader.Read())
            {
                switch (jsonReader.TokenType)
                {
                    case JsonTokenType.String:
                        {
                            var files = new IncludeExcludeFiles()
                            {
                                Include = new[] { (string)jsonReader.GetString() }
                            };

                            mappings.Add(mappingKey, files);
                        }
                        break;
                    case JsonTokenType.StartArray:
                        {
                            IReadOnlyList<string> include = jsonReader.ReadStringArrayAsReadOnlyListFromArrayStart();

                            var files = new IncludeExcludeFiles()
                            {
                                Include = include
                            };

                            mappings.Add(mappingKey, files);
                        }
                        break;
                    case JsonTokenType.StartObject:
                        {
                            IReadOnlyList<string> excludeFiles = null;
                            IReadOnlyList<string> exclude = null;
                            IReadOnlyList<string> includeFiles = null;
                            IReadOnlyList<string> include = null;

                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (jsonReader.ValueTextEquals(Utf8ExcludeFiles))
                                {
                                    excludeFiles = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Exclude))
                                {
                                    exclude = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8IncludeFiles))
                                {
                                    includeFiles = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Include))
                                {
                                    include = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                                }
                                else
                                {
                                    jsonReader.Skip();
                                }
                            }

                            if (include != null || includeFiles != null || exclude != null || excludeFiles != null)
                            {
                                var files = new IncludeExcludeFiles()
                                {
                                    ExcludeFiles = excludeFiles,
                                    Exclude = exclude,
                                    IncludeFiles = includeFiles,
                                    Include = include
                                };

                                mappings.Add(mappingKey, files);
                            }
                        }
                        break;
                }
            }
        }

        private static void ReadMSBuildMetadata(ref Utf8JsonStreamReader jsonReader, PackageSpec packageSpec)
        {
            var centralPackageVersionsManagementEnabled = false;
            var centralPackageVersionOverrideDisabled = false;
            var CentralPackageTransitivePinningEnabled = false;
            List<string> configFilePaths = null;
            var crossTargeting = false;
            List<string> fallbackFolders = null;
            List<ProjectRestoreMetadataFile> files = null;
            var legacyPackagesDirectory = false;
            ProjectRestoreMetadata msbuildMetadata = null;
            List<string> originalTargetFrameworks = null;
            string outputPath = null;
            string packagesConfigPath = null;
            string packagesPath = null;
            string projectJsonPath = null;
            string projectName = null;
            string projectPath = null;
            ProjectStyle? projectStyle = null;
            string projectUniqueName = null;
            RestoreLockProperties restoreLockProperties = null;
            var skipContentFileWrite = false;
            List<PackageSource> sources = null;
            List<ProjectRestoreMetadataFrameworkInfo> targetFrameworks = null;
            var validateRuntimeAssets = false;
            WarningProperties warningProperties = null;
            RestoreAuditProperties auditProperties = null;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals(Utf8CentralPackageVersionsManagementEnabled))
                    {
                        centralPackageVersionsManagementEnabled = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8CentralPackageVersionOverrideDisabled))
                    {
                        centralPackageVersionOverrideDisabled = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8CentralPackageTransitivePinningEnabled))
                    {
                        CentralPackageTransitivePinningEnabled = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ConfigFilePaths))
                    {
                        jsonReader.Read();
                        configFilePaths = jsonReader.ReadStringArrayAsIList() as List<string>;
                    }
                    else if (jsonReader.ValueTextEquals(Utf8CrossTargeting))
                    {
                        crossTargeting = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8FallbackFolders))
                    {
                        jsonReader.Read();
                        fallbackFolders = jsonReader.ReadStringArrayAsIList() as List<string>;
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Files))
                    {
                        if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                var filePropertyName = jsonReader.GetString();
                                files = files ?? new List<ProjectRestoreMetadataFile>();

                                files.Add(new ProjectRestoreMetadataFile(filePropertyName, jsonReader.ReadNextTokenAsString()));
                            }
                        }
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Frameworks))
                    {
                        targetFrameworks = ReadTargetFrameworks(ref jsonReader);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8LegacyPackagesDirectory))
                    {
                        legacyPackagesDirectory = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8OriginalTargetFrameworks))
                    {
                        jsonReader.Read();
                        originalTargetFrameworks = jsonReader.ReadStringArrayAsIList() as List<string>;
                    }
                    else if (jsonReader.ValueTextEquals(Utf8OutputPath))
                    {
                        outputPath = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8PackagesConfigPath))
                    {
                        packagesConfigPath = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8PackagesPath))
                    {
                        packagesPath = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ProjectJsonPath))
                    {
                        projectJsonPath = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ProjectName))
                    {
                        projectName = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ProjectPath))
                    {
                        projectPath = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ProjectStyle))
                    {
                        string projectStyleString = jsonReader.ReadNextTokenAsString();

                        if (!string.IsNullOrEmpty(projectStyleString)
                            && Enum.TryParse(projectStyleString, ignoreCase: true, result: out ProjectStyle projectStyleValue))
                        {
                            projectStyle = projectStyleValue;
                        }
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ProjectUniqueName))
                    {
                        projectUniqueName = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8RestoreLockProperties))
                    {
                        string nuGetLockFilePath = null;
                        var restoreLockedMode = false;
                        string restorePackagesWithLockFile = null;

                        if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (jsonReader.ValueTextEquals(Utf8NuGetLockFilePath))
                                {
                                    nuGetLockFilePath = jsonReader.ReadNextTokenAsString();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8RestoreLockedMode))
                                {
                                    restoreLockedMode = jsonReader.ReadNextTokenAsBoolOrFalse();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8RestorePackagesWithLockFile))
                                {
                                    restorePackagesWithLockFile = jsonReader.ReadNextTokenAsString();
                                }
                                else
                                {
                                    jsonReader.Skip();
                                }
                            }
                        }
                        restoreLockProperties = new RestoreLockProperties(restorePackagesWithLockFile, nuGetLockFilePath, restoreLockedMode);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8RestoreAuditProperties))
                    {
                        string enableAudit = null, auditLevel = null, auditMode = null;
                        if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (jsonReader.ValueTextEquals(Utf8EnableAudit))
                                {
                                    enableAudit = jsonReader.ReadNextTokenAsString();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8AuditLevel))
                                {
                                    auditLevel = jsonReader.ReadNextTokenAsString();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8AuditMode))
                                {
                                    auditMode = jsonReader.ReadNextTokenAsString();
                                }
                                else
                                {
                                    jsonReader.Skip();
                                }
                            }
                        }
                        auditProperties = new RestoreAuditProperties()
                        {
                            EnableAudit = enableAudit,
                            AuditLevel = auditLevel,
                            AuditMode = auditMode,
                        };
                    }
                    else if (jsonReader.ValueTextEquals(Utf8SkipContentFileWrite))
                    {
                        skipContentFileWrite = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Sources))
                    {
                        if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                var sourcePropertyName = jsonReader.GetString();
                                sources = sources ?? new List<PackageSource>();

                                sources.Add(new PackageSource(sourcePropertyName));
                                jsonReader.Skip();
                            }
                        }
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ValidateRuntimeAssets))
                    {
                        validateRuntimeAssets = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8WarningProperties))
                    {
                        var allWarningsAsErrors = false;
                        var noWarn = new HashSet<NuGetLogCode>();
                        var warnAsError = new HashSet<NuGetLogCode>();
                        var warningsNotAsErrors = new HashSet<NuGetLogCode>();

                        if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (jsonReader.ValueTextEquals(Utf8AllWarningsAsErrors))
                                {
                                    allWarningsAsErrors = jsonReader.ReadNextTokenAsBoolOrFalse();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8NoWarn))
                                {
                                    ReadNuGetLogCodes(ref jsonReader, noWarn);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8WarnAsError))
                                {
                                    ReadNuGetLogCodes(ref jsonReader, warnAsError);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8WarnNotAsError))
                                {
                                    ReadNuGetLogCodes(ref jsonReader, warningsNotAsErrors);
                                }
                                else
                                {
                                    jsonReader.Skip();
                                }
                            }
                        }

                        warningProperties = new WarningProperties(warnAsError, noWarn, allWarningsAsErrors, warningsNotAsErrors);
                    }
                    else
                    {
                        jsonReader.Skip();
                    }
                }
            }

            if (projectStyle == ProjectStyle.PackagesConfig)
            {
                msbuildMetadata = new PackagesConfigProjectRestoreMetadata()
                {
                    PackagesConfigPath = packagesConfigPath
                };
            }
            else
            {
                msbuildMetadata = new ProjectRestoreMetadata();
            }

            msbuildMetadata.CentralPackageVersionsEnabled = centralPackageVersionsManagementEnabled;
            msbuildMetadata.CentralPackageVersionOverrideDisabled = centralPackageVersionOverrideDisabled;
            msbuildMetadata.CentralPackageTransitivePinningEnabled = CentralPackageTransitivePinningEnabled;
            msbuildMetadata.RestoreAuditProperties = auditProperties;

            if (configFilePaths != null)
            {
                msbuildMetadata.ConfigFilePaths = configFilePaths;
            }

            msbuildMetadata.CrossTargeting = crossTargeting;

            if (fallbackFolders != null)
            {
                msbuildMetadata.FallbackFolders = fallbackFolders;
            }

            if (files != null)
            {
                msbuildMetadata.Files = files;
            }

            msbuildMetadata.LegacyPackagesDirectory = legacyPackagesDirectory;

            if (originalTargetFrameworks != null)
            {
                msbuildMetadata.OriginalTargetFrameworks = originalTargetFrameworks;
            }

            msbuildMetadata.OutputPath = outputPath;
            msbuildMetadata.PackagesPath = packagesPath;
            msbuildMetadata.ProjectJsonPath = projectJsonPath;
            msbuildMetadata.ProjectName = projectName;
            msbuildMetadata.ProjectPath = projectPath;

            if (projectStyle.HasValue)
            {
                msbuildMetadata.ProjectStyle = projectStyle.Value;
            }

            msbuildMetadata.ProjectUniqueName = projectUniqueName;

            if (restoreLockProperties != null)
            {
                msbuildMetadata.RestoreLockProperties = restoreLockProperties;
            }

            msbuildMetadata.SkipContentFileWrite = skipContentFileWrite;

            if (sources != null)
            {
                msbuildMetadata.Sources = sources;
            }

            if (targetFrameworks != null)
            {
                msbuildMetadata.TargetFrameworks = targetFrameworks;
            }

            msbuildMetadata.ValidateRuntimeAssets = validateRuntimeAssets;

            if (warningProperties != null)
            {
                msbuildMetadata.ProjectWideWarningProperties = warningProperties;
            }

            packageSpec.RestoreMetadata = msbuildMetadata;
        }

        private static void ReadNuGetLogCodes(ref Utf8JsonStreamReader jsonReader, HashSet<NuGetLogCode> hashCodes)
        {
            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartArray)
            {
                while (jsonReader.Read() && jsonReader.TokenType != JsonTokenType.EndArray)
                {
                    if (jsonReader.TokenType == JsonTokenType.String && Enum.TryParse(jsonReader.GetString(), out NuGetLogCode code))
                    {
                        hashCodes.Add(code);
                    }
                }
            }
        }

        private static List<NuGetLogCode> ReadNuGetLogCodesList(ref Utf8JsonStreamReader jsonReader)
        {
            List<NuGetLogCode> items = null;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartArray)
            {
                while (jsonReader.Read() && jsonReader.TokenType != JsonTokenType.EndArray)
                {
                    if (jsonReader.TokenType == JsonTokenType.String && Enum.TryParse(jsonReader.GetString(), out NuGetLogCode code))
                    {
                        items = items ?? new List<NuGetLogCode>();

                        items.Add(code);
                    }
                }
            }
            return items;
        }

        private static void ReadPackageTypes(PackageSpec packageSpec, ref Utf8JsonStreamReader jsonReader)
        {
            IReadOnlyList<PackageType> packageTypes = null;
            PackageType packageType = null;

            try
            {
                if (jsonReader.Read())
                {
                    switch (jsonReader.TokenType)
                    {
                        case JsonTokenType.String:
                            packageType = CreatePackageType(ref jsonReader);

                            packageTypes = new[] { packageType };
                            break;

                        case JsonTokenType.StartArray:
                            var types = new List<PackageType>();

                            while (jsonReader.Read() && jsonReader.TokenType != JsonTokenType.EndArray)
                            {
                                if (jsonReader.TokenType != JsonTokenType.String)
                                {
                                    var exception = new JsonException(string.Format(
                                        CultureInfo.CurrentCulture,
                                        Strings.InvalidPackageType,
                                        PackageSpec.PackageSpecFileName));
                                    exception.Data.Add(FileFormatException.SurfaceMessage, true);
                                    throw exception;
                                }

                                packageType = CreatePackageType(ref jsonReader);

                                types.Add(packageType);
                            }

                            packageTypes = types;
                            break;

                        case JsonTokenType.Null:
                            break;
                        default:
                            throw new InvalidCastException();
                    }

#pragma warning disable CS0612 // Type or member is obsolete
                    if (packageTypes != null)
                    {
                        packageSpec.PackOptions.PackageType = packageTypes;
                    }
#pragma warning restore CS0612 // Type or member is obsolete
                }
            }
            catch (Exception)
            {
                var exception = new JsonException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.InvalidPackageType,
                    PackageSpec.PackageSpecFileName));
                exception.Data.Add(FileFormatException.SurfaceMessage, true);
                throw exception;
            }
        }

        [Obsolete]
        private static void ReadPackInclude(ref Utf8JsonStreamReader jsonReader, PackageSpec packageSpec)
        {
            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = jsonReader.GetString();
                    string propertyValue = jsonReader.ReadNextTokenAsString();

                    packageSpec.PackInclude.Add(new KeyValuePair<string, string>(propertyName, propertyValue));
                }
            }
        }

        [Obsolete]
        private static void ReadPackOptions(ref Utf8JsonStreamReader jsonReader, PackageSpec packageSpec, ref bool isMappingsNull)
        {
            var wasMappingsRead = false;
            bool isPackOptionsValueAnObject = false;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                isPackOptionsValueAnObject = true;
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals(Utf8Files))
                    {
                        wasMappingsRead = ReadPackOptionsFiles(packageSpec, ref jsonReader, wasMappingsRead);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8IconUrl))
                    {
                        packageSpec.IconUrl = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8LicenseUrl))
                    {
                        packageSpec.LicenseUrl = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Owners))
                    {
                        jsonReader.Read();
                        string[] owners = jsonReader.ReadStringArrayAsIList()?.ToArray();
                        if (owners != null)
                        {
                            packageSpec.Owners = owners;
                        }
                    }
                    else if (jsonReader.ValueTextEquals(Utf8PackageType))
                    {
                        ReadPackageTypes(packageSpec, ref jsonReader);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ProjectUrl))
                    {
                        packageSpec.ProjectUrl = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8ReleaseNotes))
                    {
                        packageSpec.ReleaseNotes = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8RequireLicenseAcceptance))
                    {
                        packageSpec.RequireLicenseAcceptance = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Summary))
                    {
                        packageSpec.Summary = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Tags))
                    {
                        jsonReader.Read();
                        string[] tags = jsonReader.ReadStringArrayAsIList()?.ToArray();

                        if (tags != null)
                        {
                            packageSpec.Tags = tags;
                        }
                    }
                    else
                    {
                        jsonReader.Skip();
                    }
                }
            }
            isMappingsNull = isPackOptionsValueAnObject && !wasMappingsRead;
        }

        [Obsolete]
        private static bool ReadPackOptionsFiles(PackageSpec packageSpec, ref Utf8JsonStreamReader jsonReader, bool wasMappingsRead)
        {
            IReadOnlyList<string> excludeFiles = null;
            IReadOnlyList<string> exclude = null;
            IReadOnlyList<string> includeFiles = null;
            IReadOnlyList<string> include = null;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var filesPropertyName = jsonReader.GetString();
                    if (jsonReader.ValueTextEquals(Utf8ExcludeFiles))
                    {
                        excludeFiles = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Exclude))
                    {
                        exclude = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8IncludeFiles))
                    {
                        includeFiles = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Include))
                    {
                        include = jsonReader.ReadNextStringOrArrayOfStringsAsReadOnlyList();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Mappings))
                    {
                        if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                        {
                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                wasMappingsRead = true;
                                var mappingsPropertyName = jsonReader.GetString();
                                ReadMappings(ref jsonReader, mappingsPropertyName, packageSpec.PackOptions.Mappings);
                            }
                        }
                    }
                    else
                    {
                        jsonReader.Skip();
                    }
                }
            }

            if (include != null || includeFiles != null || exclude != null || excludeFiles != null)
            {
                packageSpec.PackOptions.IncludeExcludeFiles = new IncludeExcludeFiles()
                {
                    ExcludeFiles = excludeFiles,
                    Exclude = exclude,
                    IncludeFiles = includeFiles,
                    Include = include
                };
            }

            return wasMappingsRead;
        }

        private static RuntimeDependencySet ReadRuntimeDependencySet(ref Utf8JsonStreamReader jsonReader, string dependencySetName)
        {
            List<RuntimePackageDependency> dependencies = null;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    dependencies ??= new List<RuntimePackageDependency>();

                    var dependency = new RuntimePackageDependency(propertyName, VersionRange.Parse(jsonReader.ReadNextTokenAsString()));

                    dependencies.Add(dependency);
                }
            }

            return new RuntimeDependencySet(
                dependencySetName,
                dependencies);
        }

        private static RuntimeDescription ReadRuntimeDescription(ref Utf8JsonStreamReader jsonReader, string runtimeName)
        {
            List<string> inheritedRuntimes = null;
            List<RuntimeDependencySet> additionalDependencies = null;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals(Utf8HashTagImport))
                    {
                        jsonReader.Read();
                        inheritedRuntimes = jsonReader.ReadStringArrayAsIList() as List<string>;
                    }
                    else
                    {
                        var propertyName = jsonReader.GetString();
                        additionalDependencies ??= new List<RuntimeDependencySet>();

                        RuntimeDependencySet dependency = ReadRuntimeDependencySet(ref jsonReader, propertyName);

                        additionalDependencies.Add(dependency);
                    }
                }
            }

            return new RuntimeDescription(
                runtimeName,
                inheritedRuntimes,
                additionalDependencies);
        }

        private static List<RuntimeDescription> ReadRuntimes(ref Utf8JsonStreamReader jsonReader)
        {
            var runtimeDescriptions = new List<RuntimeDescription>();

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    RuntimeDescription runtimeDescription = ReadRuntimeDescription(ref jsonReader, jsonReader.GetString());

                    runtimeDescriptions.Add(runtimeDescription);
                }
            }

            return runtimeDescriptions;
        }

        [Obsolete]
        private static void ReadScripts(ref Utf8JsonStreamReader jsonReader, PackageSpec packageSpec)
        {
            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    if (jsonReader.Read())
                    {
                        if (jsonReader.TokenType == JsonTokenType.String)
                        {
                            packageSpec.Scripts[propertyName] = new string[] { (string)jsonReader.GetString() };
                        }
                        else if (jsonReader.TokenType == JsonTokenType.StartArray)
                        {
                            var list = new List<string>();

                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.String)
                            {
                                list.Add(jsonReader.GetString());
                            }

                            packageSpec.Scripts[propertyName] = list;
                        }
                        else
                        {
                            var exception = new JsonException(string.Format(CultureInfo.CurrentCulture, "The value of a script in '{0}' can only be a string or an array of strings", PackageSpec.PackageSpecFileName));
                            exception.Data.Add(FileFormatException.SurfaceMessage, true);
                            throw exception;
                        }
                    }
                }
            }
        }

        private static List<CompatibilityProfile> ReadSupports(ref Utf8JsonStreamReader jsonReader)
        {
            var compatibilityProfiles = new List<CompatibilityProfile>();

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    CompatibilityProfile compatibilityProfile = ReadCompatibilityProfile(ref jsonReader, propertyName);

                    compatibilityProfiles.Add(compatibilityProfile);
                }
            }
            return compatibilityProfiles;
        }

        private static LibraryDependencyTarget ReadTarget(
           ref Utf8JsonStreamReader jsonReader,
           string packageSpecPath,
           LibraryDependencyTarget targetFlagsValue)
        {
            if (jsonReader.Read())
            {
                var targetString = jsonReader.GetString();

                targetFlagsValue = LibraryDependencyTargetUtils.Parse(targetString);

                // Verify that the value specified is package, project, or external project
                if (!ValidateDependencyTarget(targetFlagsValue))
                {
                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidDependencyTarget,
                        targetString);
                    var exception = new JsonException(message);
                    throw exception;
                }
            }

            return targetFlagsValue;
        }

        private static List<ProjectRestoreMetadataFrameworkInfo> ReadTargetFrameworks(ref Utf8JsonStreamReader jsonReader)
        {
            var targetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>();

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var frameworkPropertyName = jsonReader.GetString();
                    NuGetFramework framework = NuGetFramework.Parse(frameworkPropertyName);
                    var frameworkGroup = new ProjectRestoreMetadataFrameworkInfo(framework);

                    if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (jsonReader.ValueTextEquals(Utf8ProjectReferences))
                            {
                                if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                                {
                                    while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                    {
                                        var projectReferencePropertyName = jsonReader.GetString();
                                        string excludeAssets = null;
                                        string includeAssets = null;
                                        string privateAssets = null;
                                        string projectReferenceProjectPath = null;

                                        if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
                                        {
                                            while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                            {
                                                if (jsonReader.ValueTextEquals(Utf8ExcludeAssets))
                                                {
                                                    excludeAssets = jsonReader.ReadNextTokenAsString();
                                                }
                                                else if (jsonReader.ValueTextEquals(Utf8IncludeAssets))
                                                {
                                                    includeAssets = jsonReader.ReadNextTokenAsString();
                                                }
                                                else if (jsonReader.ValueTextEquals(Utf8PrivateAssets))
                                                {
                                                    privateAssets = jsonReader.ReadNextTokenAsString();
                                                }
                                                else if (jsonReader.ValueTextEquals(Utf8ProjectPath))
                                                {
                                                    projectReferenceProjectPath = jsonReader.ReadNextTokenAsString();
                                                }
                                                else
                                                {
                                                    jsonReader.Skip();
                                                }

                                            }
                                        }

                                        frameworkGroup.ProjectReferences.Add(new ProjectRestoreReference()
                                        {
                                            ProjectUniqueName = projectReferencePropertyName,
                                            ProjectPath = projectReferenceProjectPath,

                                            IncludeAssets = LibraryIncludeFlagUtils.GetFlags(
                                                flags: includeAssets,
                                                defaultFlags: LibraryIncludeFlags.All),

                                            ExcludeAssets = LibraryIncludeFlagUtils.GetFlags(
                                                flags: excludeAssets,
                                                defaultFlags: LibraryIncludeFlags.None),

                                            PrivateAssets = LibraryIncludeFlagUtils.GetFlags(
                                                flags: privateAssets,
                                                defaultFlags: LibraryIncludeFlagUtils.DefaultSuppressParent),
                                        });
                                    }
                                }
                            }
                            else if (jsonReader.ValueTextEquals(Utf8TargetAlias))
                            {
                                frameworkGroup.TargetAlias = jsonReader.ReadNextTokenAsString();
                            }
                            else
                            {
                                jsonReader.Skip();
                            }
                        }

                        targetFrameworks.Add(frameworkGroup);
                    }
                }
            }
            return targetFrameworks;
        }

        private static void ReadTargetFrameworks(PackageSpec packageSpec, ref Utf8JsonStreamReader jsonReader)
        {
            var frameworkName = NuGetFramework.Parse(jsonReader.GetString());

            var targetFrameworkInformation = new TargetFrameworkInformation();
            NuGetFramework secondaryFramework = default;

            if (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.Read() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals(Utf8AssetTargetFallback))
                    {
                        targetFrameworkInformation.AssetTargetFallback = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8SecondaryFramework))
                    {
                        var secondaryFrameworkString = jsonReader.ReadNextTokenAsString();
                        if (!string.IsNullOrEmpty(secondaryFrameworkString))
                        {
                            secondaryFramework = NuGetFramework.Parse(secondaryFrameworkString);
                        }
                    }
                    else if (jsonReader.ValueTextEquals(Utf8CentralPackageVersions))
                    {
                        ReadCentralPackageVersions(
                            ref jsonReader,
                            targetFrameworkInformation.CentralPackageVersions);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Dependencies))
                    {
                        ReadDependencies(
                            ref jsonReader,
                            targetFrameworkInformation.Dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: false);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8DownloadDependencies))
                    {
                        ReadDownloadDependencies(
                            ref jsonReader,
                            targetFrameworkInformation.DownloadDependencies,
                            packageSpec.FilePath);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8FrameworkAssemblies))
                    {
                        ReadDependencies(
                            ref jsonReader,
                            targetFrameworkInformation.Dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: true);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8FrameworkReferences))
                    {
                        ReadFrameworkReferences(
                            ref jsonReader,
                            targetFrameworkInformation.FrameworkReferences,
                            packageSpec.FilePath);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Imports))
                    {
                        ReadImports(packageSpec, ref jsonReader, targetFrameworkInformation);
                    }
                    else if (jsonReader.ValueTextEquals(Utf8RuntimeIdentifierGraphPath))
                    {
                        targetFrameworkInformation.RuntimeIdentifierGraphPath = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8TargetAlias))
                    {
                        targetFrameworkInformation.TargetAlias = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Warn))
                    {
                        targetFrameworkInformation.Warn = jsonReader.ReadNextTokenAsBoolOrFalse();
                    }
                    else
                    {
                        jsonReader.Skip();
                    }
                }
            }

            NuGetFramework updatedFramework = frameworkName;

            if (targetFrameworkInformation.Imports.Count > 0)
            {
                NuGetFramework[] imports = targetFrameworkInformation.Imports.ToArray();

                if (targetFrameworkInformation.AssetTargetFallback)
                {
                    updatedFramework = new AssetTargetFallbackFramework(GetDualCompatibilityFrameworkIfNeeded(frameworkName, secondaryFramework), imports);
                }
                else
                {
                    updatedFramework = new FallbackFramework(GetDualCompatibilityFrameworkIfNeeded(frameworkName, secondaryFramework), imports);
                }
            }
            else
            {
                updatedFramework = GetDualCompatibilityFrameworkIfNeeded(frameworkName, secondaryFramework);
            }

            targetFrameworkInformation.FrameworkName = updatedFramework;

            packageSpec.TargetFrameworks.Add(targetFrameworkInformation);
        }

        private static NuGetFramework GetDualCompatibilityFrameworkIfNeeded(NuGetFramework frameworkName, NuGetFramework secondaryFramework)
        {
            if (secondaryFramework != default)
            {
                return new DualCompatibilityFramework(frameworkName, secondaryFramework);
            }

            return frameworkName;
        }

        private static bool ValidateDependencyTarget(LibraryDependencyTarget targetValue)
        {
            var isValid = false;

            switch (targetValue)
            {
                case LibraryDependencyTarget.Package:
                case LibraryDependencyTarget.Project:
                case LibraryDependencyTarget.ExternalProject:
                    isValid = true;
                    break;
            }

            return isValid;
        }
    }
}
