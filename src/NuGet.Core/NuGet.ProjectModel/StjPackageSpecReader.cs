// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public static class StjPackageSpecReader
    {
        private static readonly char[] DelimitedStringSeparators = { ' ', ',' };
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
        //private static readonly byte[] Utf8 = Encoding.UTF8.GetBytes("");

        /// <summary>
        /// Load and parse a project.json file
        /// </summary>
        /// <param name="name">project name</param>
        /// <param name="packageSpecPath">file path</param>
        public static PackageSpec GetPackageSpec(string name, string packageSpecPath)
        {
            return FileUtility.SafeRead(filePath: packageSpecPath, read: (stream, filePath) => GetPackageSpec(stream, name, filePath, null));
        }

        public static PackageSpec GetPackageSpec(string json, string name, string packageSpecPath)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(json);
            using (var stream = new MemoryStream(byteArray))
            {
                return GetPackageSpec(stream, name, packageSpecPath, null);
            }
        }

        public static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue)
        {
            var packageSpec = JsonUtility.LoadJsonAsync<PackageSpec>(stream).Result;

            packageSpec.Name = name;
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

        internal static PackageSpec GetPackageSpec(ref Utf8JsonReader jsonReader, JsonSerializerOptions options, string name, string packageSpecPath, string snapshotValue)
        {
            var packageSpec = new PackageSpec();

            List<CompatibilityProfile> compatibilityProfiles = null;
            List<RuntimeDescription> runtimeDescriptions = null;
            var wasPackOptionsSet = false;
            var isMappingsNull = false;
            string filePath = name == null ? null : Path.GetFullPath(packageSpecPath);

            var stringArrayConverter = (JsonConverter<string[]>)options.GetConverter(typeof(string[]));

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals(string.Empty))
                    {
                        jsonReader.Skip();
                    }
#pragma warning disable CS0612 // Type or member is obsolete
                    else if (jsonReader.ValueTextEquals(Utf8Authors))
                    {
                        jsonReader.ReadNextToken();
                        if (jsonReader.TokenType == JsonTokenType.StartArray)
                        {
                            packageSpec.Authors = stringArrayConverter.Read(ref jsonReader, typeof(string[]), options);
                        }
                        else
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
                        ReadPackOptions(ref jsonReader, options, packageSpec, ref isMappingsNull);
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
                                throw FileFormatException.Create(ex, version, packageSpec.FilePath);
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

        private static PackageType CreatePackageType(ref Utf8JsonReader jsonReader)
        {
            var name = jsonReader.GetString();

            return new PackageType(name, Packaging.Core.PackageType.EmptyVersion);
        }

        [Obsolete]
        private static void ReadBuildOptions(ref Utf8JsonReader jsonReader, PackageSpec packageSpec)
        {
            packageSpec.BuildOptions = new BuildOptions();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
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
            ref Utf8JsonReader jsonReader,
            IDictionary<string, CentralPackageVersion> centralPackageVersions,
            string filePath)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();

                    if (string.IsNullOrEmpty(propertyName))
                    {
                        throw FileFormatException.Create(
                            "Unable to resolve central version ''.",
                            -1,
                            -1,
                            filePath);
                    }

                    string version = jsonReader.ReadNextTokenAsString();

                    if (string.IsNullOrEmpty(version))
                    {
                        throw FileFormatException.Create(
                            "The version cannot be null or empty.",
                            -1,
                            -1,
                            filePath);
                    }

                    centralPackageVersions[propertyName] = new CentralPackageVersion(propertyName, VersionRange.Parse(version));
                }
            }
        }

        private static CompatibilityProfile ReadCompatibilityProfile(ref Utf8JsonReader jsonReader, string profileName)
        {
            List<FrameworkRuntimePair> sets = null;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    sets = sets ?? new List<FrameworkRuntimePair>();

                    IReadOnlyList<string> values = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList() ?? Array.Empty<string>();

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

        internal static void ReadDependencies(
                    ref Utf8JsonReader jsonReader,
                    IList<LibraryDependency> results,
                    string packageSpecPath,
                    bool isGacOrFrameworkReference)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    if (string.IsNullOrEmpty(propertyName))
                    {
                        // Advance the reader's position to be able to report the line and column for the property value.
                        jsonReader.ReadNextToken();

                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            -1,
                            -1,
                            packageSpecPath);
                    }
                    // Support
                    // "dependencies" : {
                    //    "Name" : "1.0"
                    // }

                    if (jsonReader.ReadNextToken())
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
                            while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                IEnumerable<string> values = null;
                                if (jsonReader.ValueTextEquals(Utf8AutoReferenced))
                                {
                                    autoReferenced = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpecPath);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Exclude))
                                {
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                }
                                else if (jsonReader.ValueTextEquals(Utf8GeneratePathProperty))
                                {
                                    generatePathProperty = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpecPath);
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
                                    if (jsonReader.ReadNextToken())
                                    {
                                        //versionLine = jsonReader.LineNumber;
                                        //versionColumn = jsonReader.LinePosition;

                                        dependencyVersionValue = jsonReader.GetString();
                                    }
                                }
                                else if (jsonReader.ValueTextEquals(Utf8VersionOverride))
                                {
                                    if (jsonReader.ReadNextToken())
                                    {
                                        try
                                        {
                                            versionOverride = VersionRange.Parse(jsonReader.GetString());
                                        }
                                        catch (Exception ex)
                                        {
                                            throw FileFormatException.Create(
                                                ex,
                                                -1,
                                                -1,
                                                packageSpecPath);
                                        }
                                    }
                                }
                                else if (jsonReader.ValueTextEquals(Utf8VersionCentrallyManaged))
                                {
                                    versionCentrallyManaged = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpecPath);
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
                                throw FileFormatException.Create(
                                    ex,
                                    -1,
                                    -1,
                                    packageSpecPath);
                            }
                        }

                        // Projects and References may have empty version ranges, Packages may not
                        if (dependencyVersionRange == null)
                        {
                            if ((targetFlagsValue & LibraryDependencyTarget.Package) == LibraryDependencyTarget.Package)
                            {
                                throw FileFormatException.Create(
                                    new ArgumentException(Strings.MissingVersionOnDependency),
                                    -1,
                                    -1,
                                    packageSpecPath);
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

        internal static void ReadCentralTransitiveDependencyGroup(
            ref Utf8JsonReader jsonReader,
            IList<LibraryDependency> results,
            string packageSpecPath)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    if (string.IsNullOrEmpty(propertyName))
                    {
                        // Advance the reader's position to be able to report the line and column for the property value.
                        jsonReader.ReadNextToken();

                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            -1,
                            -1,
                            packageSpecPath);
                    }

                    if (jsonReader.ReadNextToken())
                    {
                        int dependencyValueLine = -1;
                        int dependencyValueColumn = -1;
                        var versionLine = 0;
                        var versionColumn = 0;

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
                            while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
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
                                    if (jsonReader.ReadNextToken())
                                    {
                                        versionLine = -1;
                                        versionColumn = -1;
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
                                throw FileFormatException.Create(
                                    ex,
                                    versionLine,
                                    versionColumn,
                                    packageSpecPath);
                            }
                        }

                        if (dependencyVersionRange == null)
                        {
                            throw FileFormatException.Create(
                                    new ArgumentException(Strings.MissingVersionOnDependency),
                                    dependencyValueLine,
                                    dependencyValueColumn,
                                    packageSpecPath);
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

        private static void ReadDownloadDependencies(
            ref Utf8JsonReader jsonReader,
            IList<DownloadDependency> downloadDependencies,
            string packageSpecPath)
        {
            var seenIds = new HashSet<string>();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartArray)
            {
                do
                {
                    string name = null;
                    string versionValue = null;
                    var isNameDefined = false;
                    var isVersionDefined = false;
                    int line = -1;
                    int column = -1;
                    int versionLine = 0;
                    int versionColumn = 0;

                    if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (jsonReader.ValueTextEquals(Utf8Name))
                            {
                                isNameDefined = true;
                                name = jsonReader.ReadNextTokenAsString();
                            }
                            else if (jsonReader.ValueTextEquals(Utf8Version))
                            {
                                isVersionDefined = true;
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
                        throw FileFormatException.Create(
                            "Unable to resolve downloadDependency ''.",
                            line,
                            column,
                            packageSpecPath);
                    }

                    if (!seenIds.Add(name))
                    {
                        // package ID already seen, only use first definition.
                        continue;
                    }

                    if (string.IsNullOrEmpty(versionValue))
                    {
                        throw FileFormatException.Create(
                            "The version cannot be null or empty",
                            isVersionDefined ? versionLine : line,
                            isVersionDefined ? versionColumn : column,
                            packageSpecPath);
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
                            throw FileFormatException.Create(
                                ex,
                                isVersionDefined ? versionLine : line,
                                isVersionDefined ? versionColumn : column,
                                packageSpecPath);
                        }
                    }
                } while (jsonReader.TokenType == JsonTokenType.EndObject);
            }
        }

        private static IReadOnlyList<string> ReadEnumerableOfString(ref Utf8JsonReader jsonReader)
        {
            string value = jsonReader.ReadNextTokenAsString();

            return value.Split(DelimitedStringSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ReadFrameworkReferences(
            ref Utf8JsonReader jsonReader,
            ISet<FrameworkDependency> frameworkReferences,
            string packageSpecPath)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var frameworkName = jsonReader.GetString();
                    if (string.IsNullOrEmpty(frameworkName))
                    {
                        // Advance the reader's position to be able to report the line and column for the property value.
                        jsonReader.ReadNextToken();

                        throw FileFormatException.Create(
                            "Unable to resolve frameworkReference.",
                            -1,
                            -1,
                            packageSpecPath);
                    }

                    var privateAssets = FrameworkDependencyFlagsUtils.Default;

                    if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (jsonReader.ValueTextEquals(Utf8PrivateAssets))
                            {
                                IEnumerable<string> strings = ReadEnumerableOfString(ref jsonReader);

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

        private static void ReadFrameworks(ref Utf8JsonReader reader, PackageSpec packageSpec)
        {
            if (reader.ReadNextToken() && reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    try
                    {
                        ReadTargetFrameworks(packageSpec, ref reader);
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, -1, -1, packageSpec.FilePath);
                    }
                }
            }
        }

        private static void ReadImports(PackageSpec packageSpec, ref Utf8JsonReader jsonReader, TargetFrameworkInformation targetFrameworkInformation)
        {
            IReadOnlyList<string> imports = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();

            if (imports != null && imports.Count > 0)
            {
                foreach (string import in imports.Where(element => !string.IsNullOrEmpty(element)))
                {
                    NuGetFramework framework = NuGetFramework.Parse(import);

                    if (!framework.IsSpecificFramework)
                    {
                        throw FileFormatException.Create(
                            string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Log_InvalidImportFramework,
                                import,
                                PackageSpec.PackageSpecFileName),
                            -1,
                            -1,
                            packageSpec.FilePath);
                    }

                    targetFrameworkInformation.Imports.Add(framework);
                }
            }
        }

        private static void ReadMappings(ref Utf8JsonReader jsonReader, string mappingKey, IDictionary<string, IncludeExcludeFiles> mappings)
        {
            if (jsonReader.ReadNextToken())
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

                            while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (jsonReader.ValueTextEquals(Utf8ExcludeFiles))
                                {
                                    excludeFiles = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Exclude))
                                {
                                    exclude = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8IncludeFiles))
                                {
                                    includeFiles = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
                                }
                                else if (jsonReader.ValueTextEquals(Utf8Include))
                                {
                                    include = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
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
                    default:
                        {
                            // Convert switch statement to if else
                            throw new JsonException($"Unexpected token type: {jsonReader.TokenType}");
                        }
                }
            }
        }

        private static void ReadMSBuildMetadata(ref Utf8JsonReader jsonReader, PackageSpec packageSpec)
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

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    switch (propertyName)
                    {
                        case "centralPackageVersionsManagementEnabled":
                            centralPackageVersionsManagementEnabled = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;

                        case "centralPackageVersionOverrideDisabled":
                            centralPackageVersionOverrideDisabled = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;

                        case "CentralPackageTransitivePinningEnabled":
                            CentralPackageTransitivePinningEnabled = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;

                        case "configFilePaths":
                            configFilePaths = jsonReader.ReadStringArrayAsList();
                            break;

                        case "crossTargeting":
                            crossTargeting = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;

                        case "fallbackFolders":
                            fallbackFolders = jsonReader.ReadStringArrayAsList();
                            break;

                        case "files":
                            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                            {
                                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                {
                                    var filePropertyName = jsonReader.GetString();
                                    files = files ?? new List<ProjectRestoreMetadataFile>();

                                    files.Add(new ProjectRestoreMetadataFile(filePropertyName, jsonReader.ReadNextTokenAsString()));
                                }
                            }
                            break;

                        case "frameworks":
                            targetFrameworks = ReadTargetFrameworks(ref jsonReader);
                            break;

                        case "legacyPackagesDirectory":
                            legacyPackagesDirectory = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;

                        case "originalTargetFrameworks":
                            originalTargetFrameworks = jsonReader.ReadStringArrayAsList();
                            break;

                        case "outputPath":
                            outputPath = jsonReader.ReadNextTokenAsString();
                            break;

                        case "packagesConfigPath":
                            packagesConfigPath = jsonReader.ReadNextTokenAsString();
                            break;

                        case "packagesPath":
                            packagesPath = jsonReader.ReadNextTokenAsString();
                            break;

                        case "projectJsonPath":
                            projectJsonPath = jsonReader.ReadNextTokenAsString();
                            break;

                        case "projectName":
                            projectName = jsonReader.ReadNextTokenAsString();
                            break;

                        case "projectPath":
                            projectPath = jsonReader.ReadNextTokenAsString();
                            break;

                        case "projectStyle":
                            string projectStyleString = jsonReader.ReadNextTokenAsString();

                            if (!string.IsNullOrEmpty(projectStyleString)
                                && Enum.TryParse(projectStyleString, ignoreCase: true, result: out ProjectStyle projectStyleValue))
                            {
                                projectStyle = projectStyleValue;
                            }
                            break;

                        case "projectUniqueName":
                            projectUniqueName = jsonReader.ReadNextTokenAsString();
                            break;

                        case "restoreLockProperties":
                            string nuGetLockFilePath = null;
                            var restoreLockedMode = false;
                            string restorePackagesWithLockFile = null;

                            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                            {
                                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                {
                                    var restoreLockPropertiesPropertyName = jsonReader.GetString();
                                    switch (restoreLockPropertiesPropertyName)
                                    {
                                        case "nuGetLockFilePath":
                                            nuGetLockFilePath = jsonReader.ReadNextTokenAsString();
                                            break;

                                        case "restoreLockedMode":
                                            restoreLockedMode = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                                            break;

                                        case "restorePackagesWithLockFile":
                                            restorePackagesWithLockFile = jsonReader.ReadNextTokenAsString();
                                            break;

                                        default:
                                            jsonReader.Skip();
                                            break;
                                    }
                                }
                            }
                            restoreLockProperties = new RestoreLockProperties(restorePackagesWithLockFile, nuGetLockFilePath, restoreLockedMode);
                            break;

                        case "restoreAuditProperties":
                            string enableAudit = null, auditLevel = null, auditMode = null;
                            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                            {
                                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                {
                                    var auditPropertyName = jsonReader.GetString();
                                    switch (auditPropertyName)
                                    {
                                        case "enableAudit":
                                            enableAudit = jsonReader.ReadNextTokenAsString();
                                            break;

                                        case "auditLevel":
                                            auditLevel = jsonReader.ReadNextTokenAsString();
                                            break;

                                        case "auditMode":
                                            auditMode = jsonReader.ReadNextTokenAsString();
                                            break;

                                        default:
                                            jsonReader.Skip();
                                            break;
                                    }
                                }
                            }
                            auditProperties = new RestoreAuditProperties()
                            {
                                EnableAudit = enableAudit,
                                AuditLevel = auditLevel,
                                AuditMode = auditMode,
                            };
                            break;

                        case "skipContentFileWrite":
                            skipContentFileWrite = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;

                        case "sources":
                            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                            {
                                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                {
                                    var sourcePropertyName = jsonReader.GetString();
                                    sources = sources ?? new List<PackageSource>();

                                    sources.Add(new PackageSource(sourcePropertyName));
                                    jsonReader.Skip();
                                }
                            }
                            break;

                        case "validateRuntimeAssets":
                            validateRuntimeAssets = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;

                        case "warningProperties":
                            var allWarningsAsErrors = false;
                            var noWarn = new HashSet<NuGetLogCode>();
                            var warnAsError = new HashSet<NuGetLogCode>();
                            var warningsNotAsErrors = new HashSet<NuGetLogCode>();

                            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                            {
                                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                {
                                    var warningPropertiesPropertyName = jsonReader.GetString();
                                    switch (warningPropertiesPropertyName)
                                    {
                                        case "allWarningsAsErrors":
                                            allWarningsAsErrors = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                                            break;

                                        case "noWarn":
                                            ReadNuGetLogCodes(ref jsonReader, noWarn);
                                            break;

                                        case "warnAsError":
                                            ReadNuGetLogCodes(ref jsonReader, warnAsError);
                                            break;

                                        case "warnNotAsError":
                                            ReadNuGetLogCodes(ref jsonReader, warningsNotAsErrors);
                                            break;

                                        default:
                                            jsonReader.Skip();
                                            break;
                                    }
                                }
                            }

                            warningProperties = new WarningProperties(warnAsError, noWarn, allWarningsAsErrors, warningsNotAsErrors);
                            break;
                        default:
                            jsonReader.Skip();
                            break;
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

        //Not sure if this is necessary, if the token type is boolean why would getting the bool value fail? 
        private static bool ReadNextTokenAsBoolOrFalse(ref Utf8JsonReader jsonReader, string filePath)
        {
            if (jsonReader.ReadNextToken() && (jsonReader.TokenType == JsonTokenType.False || jsonReader.TokenType == JsonTokenType.True))
            {
                try
                {
                    return jsonReader.GetBoolean();
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, filePath);
                }
            }

            return false;
        }

        private static void ReadNuGetLogCodes(ref Utf8JsonReader jsonReader, HashSet<NuGetLogCode> hashCodes)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartArray)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonTokenType.EndArray)
                {
                    if (jsonReader.TokenType == JsonTokenType.String && Enum.TryParse(jsonReader.GetString(), out NuGetLogCode code))
                    {
                        hashCodes.Add(code);
                    }
                }
            }
        }

        private static List<NuGetLogCode> ReadNuGetLogCodesList(ref Utf8JsonReader jsonReader)
        {
            List<NuGetLogCode> items = null;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartArray)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonTokenType.EndArray)
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

        private static void ReadPackageTypes(PackageSpec packageSpec, ref Utf8JsonReader jsonReader)
        {
            var errorLine = 0;
            var errorColumn = 0;

            IReadOnlyList<PackageType> packageTypes = null;
            PackageType packageType = null;

            try
            {
                if (jsonReader.ReadNextToken())
                {
                    switch (jsonReader.TokenType)
                    {
                        case JsonTokenType.String:
                            packageType = CreatePackageType(ref jsonReader);

                            packageTypes = new[] { packageType };
                            break;

                        case JsonTokenType.StartArray:
                            var types = new List<PackageType>();

                            while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonTokenType.EndArray)
                            {
                                if (jsonReader.TokenType != JsonTokenType.String)
                                {
                                    throw FileFormatException.Create(
                                        string.Format(
                                            CultureInfo.CurrentCulture,
                                            Strings.InvalidPackageType,
                                            PackageSpec.PackageSpecFileName),
                                        errorLine,
                                        errorColumn,
                                        packageSpec.FilePath);
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
                throw FileFormatException.Create(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidPackageType,
                        PackageSpec.PackageSpecFileName),
                    errorLine,
                    errorColumn,
                    packageSpec.FilePath);
            }
        }

        [Obsolete]
        private static void ReadPackInclude(ref Utf8JsonReader jsonReader, PackageSpec packageSpec)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = jsonReader.GetString();
                    string propertyValue = jsonReader.ReadNextTokenAsString();

                    packageSpec.PackInclude.Add(new KeyValuePair<string, string>(propertyName, propertyValue));
                }
            }
        }

        [Obsolete]
        private static void ReadPackOptions(ref Utf8JsonReader jsonReader, JsonSerializerOptions options, PackageSpec packageSpec, ref bool isMappingsNull)
        {
            var wasMappingsRead = false;
            bool isPackOptionsValueAnObject = false;
            var stringArrayConverter = (JsonConverter<string[]>)options.GetConverter(typeof(string[]));

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                isPackOptionsValueAnObject = true;
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    switch (propertyName)
                    {
                        case "files":
                            wasMappingsRead = ReadPackOptionsFiles(packageSpec, ref jsonReader, wasMappingsRead);
                            break;
                        case "iconUrl":
                            packageSpec.IconUrl = jsonReader.ReadNextTokenAsString();
                            break;
                        case "licenseUrl":
                            packageSpec.LicenseUrl = jsonReader.ReadNextTokenAsString();
                            break;
                        case "owners":
                            jsonReader.ReadNextToken();
                            string[] owners = stringArrayConverter.Read(ref jsonReader, typeof(string[]), options);
                            if (owners != null)
                            {
                                packageSpec.Owners = owners;
                            }
                            break;
                        case "packageType":
                            ReadPackageTypes(packageSpec, ref jsonReader);
                            break;
                        case "projectUrl":
                            packageSpec.ProjectUrl = jsonReader.ReadNextTokenAsString();
                            break;
                        case "releaseNotes":
                            packageSpec.ReleaseNotes = jsonReader.ReadNextTokenAsString();
                            break;
                        case "requireLicenseAcceptance":
                            packageSpec.RequireLicenseAcceptance = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                            break;
                        case "summary":
                            packageSpec.Summary = jsonReader.ReadNextTokenAsString();
                            break;
                        case "tags":
                            jsonReader.ReadNextToken();
                            string[] tags = stringArrayConverter.Read(ref jsonReader, typeof(string[]), options);

                            if (tags != null)
                            {
                                packageSpec.Tags = tags;
                            }
                            break;
                        default:
                            jsonReader.Skip();
                            break;
                    }
                }
            }

            isMappingsNull = isPackOptionsValueAnObject && !wasMappingsRead;
        }

        [Obsolete]
        private static bool ReadPackOptionsFiles(PackageSpec packageSpec, ref Utf8JsonReader jsonReader, bool wasMappingsRead)
        {
            IReadOnlyList<string> excludeFiles = null;
            IReadOnlyList<string> exclude = null;
            IReadOnlyList<string> includeFiles = null;
            IReadOnlyList<string> include = null;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var filesPropertyName = jsonReader.GetString();
                    switch (filesPropertyName)
                    {
                        case "excludeFiles":
                            excludeFiles = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
                            break;
                        case "exclude":
                            exclude = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
                            break;
                        case "includeFiles":
                            includeFiles = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
                            break;
                        case "include":
                            include = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList();
                            break;
                        case "mappings":
                            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                            {
                                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                {
                                    wasMappingsRead = true;
                                    var mappingsPropertyName = jsonReader.GetString();
                                    ReadMappings(ref jsonReader, mappingsPropertyName, packageSpec.PackOptions.Mappings);
                                }
                            }
                            break;
                        default:
                            jsonReader.Skip();
                            break;
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

        private static RuntimeDependencySet ReadRuntimeDependencySet(ref Utf8JsonReader jsonReader, string dependencySetName)
        {
            List<RuntimePackageDependency> dependencies = null;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
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

        private static RuntimeDescription ReadRuntimeDescription(ref Utf8JsonReader jsonReader, string runtimeName)
        {
            List<string> inheritedRuntimes = null;
            List<RuntimeDependencySet> additionalDependencies = null;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    if (propertyName == "#import")
                    {
                        inheritedRuntimes = jsonReader.ReadStringArrayAsList();
                    }
                    else
                    {
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

        private static List<RuntimeDescription> ReadRuntimes(ref Utf8JsonReader jsonReader)
        {
            var runtimeDescriptions = new List<RuntimeDescription>();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    RuntimeDescription runtimeDescription = ReadRuntimeDescription(ref jsonReader, jsonReader.GetString());

                    runtimeDescriptions.Add(runtimeDescription);
                }
            }

            return runtimeDescriptions;
        }

        [Obsolete]
        private static void ReadScripts(ref Utf8JsonReader jsonReader, PackageSpec packageSpec)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    if (jsonReader.ReadNextToken())
                    {
                        if (jsonReader.TokenType == JsonTokenType.String)
                        {
                            packageSpec.Scripts[propertyName] = new string[] { (string)jsonReader.GetString() };
                        }
                        else if (jsonReader.TokenType == JsonTokenType.StartArray)
                        {
                            var list = new List<string>();

                            while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.String)
                            {
                                list.Add(jsonReader.GetString());
                            }

                            packageSpec.Scripts[propertyName] = list;
                        }
                        else
                        {
                            throw FileFormatException.Create(
                                string.Format(CultureInfo.CurrentCulture, "The value of a script in '{0}' can only be a string or an array of strings", PackageSpec.PackageSpecFileName),
                                -1,
                                -1,
                                packageSpec.FilePath);
                        }
                    }
                }
            }
        }

        private static List<CompatibilityProfile> ReadSupports(ref Utf8JsonReader jsonReader)
        {
            var compatibilityProfiles = new List<CompatibilityProfile>();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = jsonReader.GetString();
                    CompatibilityProfile compatibilityProfile = ReadCompatibilityProfile(ref jsonReader, propertyName);

                    compatibilityProfiles.Add(compatibilityProfile);
                }
            }
            return compatibilityProfiles;
        }

        private static LibraryDependencyTarget ReadTarget(
           ref Utf8JsonReader jsonReader,
           string packageSpecPath,
           LibraryDependencyTarget targetFlagsValue)
        {
            if (jsonReader.ReadNextToken())
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

                    throw FileFormatException.Create(
                        message,
                        -1,
                        -1,
                        packageSpecPath);
                }
            }

            return targetFlagsValue;
        }

        private static List<ProjectRestoreMetadataFrameworkInfo> ReadTargetFrameworks(ref Utf8JsonReader jsonReader)
        {
            var targetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var frameworkPropertyName = jsonReader.GetString();
                    NuGetFramework framework = NuGetFramework.Parse(frameworkPropertyName);
                    var frameworkGroup = new ProjectRestoreMetadataFrameworkInfo(framework);

                    if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                        {
                            var propertyName = jsonReader.GetString();
                            if (propertyName == "projectReferences")
                            {
                                if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                                {
                                    while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                    {
                                        var projectReferencePropertyName = jsonReader.GetString();
                                        string excludeAssets = null;
                                        string includeAssets = null;
                                        string privateAssets = null;
                                        string projectReferenceProjectPath = null;

                                        if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
                                        {
                                            while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                                            {
                                                var projectReferenceObjectPropertyName = jsonReader.GetString();
                                                switch (projectReferenceObjectPropertyName)
                                                {
                                                    case "excludeAssets":
                                                        excludeAssets = jsonReader.ReadNextTokenAsString();
                                                        break;

                                                    case "includeAssets":
                                                        includeAssets = jsonReader.ReadNextTokenAsString();
                                                        break;

                                                    case "privateAssets":
                                                        privateAssets = jsonReader.ReadNextTokenAsString();
                                                        break;

                                                    case "projectPath":
                                                        projectReferenceProjectPath = jsonReader.ReadNextTokenAsString();
                                                        break;

                                                    default:
                                                        jsonReader.Skip();
                                                        break;
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
                            else if (propertyName == "targetAlias")
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

        private static void ReadTargetFrameworks(PackageSpec packageSpec, ref Utf8JsonReader jsonReader)
        {
            var frameworkName = NuGetFramework.Parse(jsonReader.GetString());

            var targetFrameworkInformation = new TargetFrameworkInformation();
            NuGetFramework secondaryFramework = default;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.ValueTextEquals("assetTargetFallback"))
                    {
                        targetFrameworkInformation.AssetTargetFallback = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
                    }
                    else if (jsonReader.ValueTextEquals("secondaryFramework"))
                    {
                        var secondaryFrameworkString = jsonReader.ReadNextTokenAsString();
                        if (!string.IsNullOrEmpty(secondaryFrameworkString))
                        {
                            secondaryFramework = NuGetFramework.Parse(secondaryFrameworkString);
                        }
                    }
                    else if (jsonReader.ValueTextEquals("centralPackageVersions"))
                    {
                        ReadCentralPackageVersions(
                            ref jsonReader,
                            targetFrameworkInformation.CentralPackageVersions,
                            packageSpec.FilePath);
                    }
                    else if (jsonReader.ValueTextEquals("dependencies"))
                    {
                        ReadDependencies(
                            ref jsonReader,
                            targetFrameworkInformation.Dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: false);
                    }
                    else if (jsonReader.ValueTextEquals("downloadDependencies"))
                    {
                        ReadDownloadDependencies(
                            ref jsonReader,
                            targetFrameworkInformation.DownloadDependencies,
                            packageSpec.FilePath);
                    }
                    else if (jsonReader.ValueTextEquals("frameworkAssemblies"))
                    {
                        ReadDependencies(
                            ref jsonReader,
                            targetFrameworkInformation.Dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: true);
                    }
                    else if (jsonReader.ValueTextEquals("frameworkReferences"))
                    {
                        ReadFrameworkReferences(
                            ref jsonReader,
                            targetFrameworkInformation.FrameworkReferences,
                            packageSpec.FilePath);
                    }
                    else if (jsonReader.ValueTextEquals("imports"))
                    {
                        ReadImports(packageSpec, ref jsonReader, targetFrameworkInformation);
                    }
                    else if (jsonReader.ValueTextEquals("runtimeIdentifierGraphPath"))
                    {
                        targetFrameworkInformation.RuntimeIdentifierGraphPath = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals("targetAlias"))
                    {
                        targetFrameworkInformation.TargetAlias = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals("warn"))
                    {
                        targetFrameworkInformation.Warn = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpec.FilePath);
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
