// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public static class JsonPackageSpecReader
    {
        private static readonly char[] DelimitedStringSeparators = { ' ', ',' };
        private static readonly char[] VersionSeparators = new[] { ';' };

        public static readonly string RestoreOptions = "restore";
        public static readonly string RestoreSettings = "restoreSettings";
        public static readonly string HideWarningsAndErrors = "hideWarningsAndErrors";
        public static readonly string PackOptions = "packOptions";
        public static readonly string PackageType = "packageType";
        public static readonly string Files = "files";

        private static readonly byte[] Utf8Authors = Encoding.UTF8.GetBytes("authors");
        private static readonly byte[] BuildOptionsUtf8 = Encoding.UTF8.GetBytes("buildOptions");
        private static readonly byte[] Utf8OutputName = Encoding.UTF8.GetBytes("outputName");
        private static readonly byte[] Utf8ContentFiles = Encoding.UTF8.GetBytes("contentFiles");
        private static readonly byte[] Utf8Copyright = Encoding.UTF8.GetBytes("copyright");
        private static readonly byte[] Utf8Dependencies = Encoding.UTF8.GetBytes("dependencies");
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
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return GetPackageSpec(ms, name, packageSpecPath, null);
            }
        }

        [Obsolete("This method is obsolete and will be removed in a future release.")]
        public static PackageSpec GetPackageSpec(JObject json)
        {
            return GetPackageSpec(json, name: null, packageSpecPath: null, snapshotValue: null);
        }

        public static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue)
        {
            using (var streamReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return GetPackageSpec(jsonReader, name, packageSpecPath, snapshotValue);
            }
        }

        [Obsolete("This method is obsolete and will be removed in a future release.")]
        public static PackageSpec GetPackageSpec(JObject rawPackageSpec, string name, string packageSpecPath, string snapshotValue)
        {
            using (var stringReader = new StringReader(rawPackageSpec.ToString()))
            using (var jsonReader = new JsonTextReader(stringReader))
            {
                return GetPackageSpec(jsonReader, name, packageSpecPath, snapshotValue);
            }
        }

        public static PackageSpec GetPackageSpec(ref Utf8JsonReader jsonReader, JsonSerializerOptions options, string name, string packageSpecPath, string snapshotValue)
        {
            var packageSpec = new PackageSpec();

            List<CompatibilityProfile> compatibilityProfiles = null;
            List<RuntimeDescription> runtimeDescriptions = null;
            var wasPackOptionsSet = false;
            var isMappingsNull = false;
            string filePath = name == null ? null : Path.GetFullPath(packageSpecPath);

            var stringArrayConverter = (System.Text.Json.Serialization.JsonConverter<string[]>)options.GetConverter(typeof(string[]));

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
                        List<string> contentFiles = jsonReader.ReadStringArrayAsList();
                        if (contentFiles != null)
                        {
                            packageSpec.ContentFiles = contentFiles;
                        }
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Copyright))
                    {
                        packageSpec.Copyright = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals("description"))
                    {
                        packageSpec.Description = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals("language"))
                    {
                        packageSpec.Language = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals("packInclude"))
                    {
                        ReadPackInclude(ref jsonReader, packageSpec);
                    }
                    else if (jsonReader.ValueTextEquals("packOptions"))
                    {
                        ReadPackOptions(ref jsonReader, options, packageSpec, ref isMappingsNull, filePath);
                        wasPackOptionsSet = true;
                    }
                    else if (jsonReader.ValueTextEquals("scripts"))
                    {
                        ReadScripts(ref jsonReader, packageSpec);
                    }
#pragma warning restore CS0612 // Type or member is obsolete
                    else if (jsonReader.ValueTextEquals(""))
                    {
                        jsonReader.Skip();
                    }
                    else if (jsonReader.ValueTextEquals(Utf8Dependencies))
                    {
                        ReadDependencies(
                            ref jsonReader,
                            packageSpec.Dependencies,
                            filePath,
                            isGacOrFrameworkReference: false);
                    }
                    else if (jsonReader.ValueTextEquals("frameworks"))
                    {
                        ReadFrameworks(ref jsonReader, packageSpec, filePath);
                    }
                    else if (jsonReader.ValueTextEquals("restore"))
                    {
                        ReadMSBuildMetadata(ref jsonReader, packageSpec);
                    }
                    else if (jsonReader.ValueTextEquals("runtimes"))
                    {
                        runtimeDescriptions = ReadRuntimes(ref jsonReader);
                    }
                    else if (jsonReader.ValueTextEquals("supports"))
                    {
                        compatibilityProfiles = ReadSupports(ref jsonReader);
                    }
                    else if (jsonReader.ValueTextEquals("title"))
                    {
                        packageSpec.Title = jsonReader.ReadNextTokenAsString();
                    }
                    else if (jsonReader.ValueTextEquals("version"))
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

        internal static PackageSpec GetPackageSpec(JsonTextReader jsonReader, string packageSpecPath)
        {
            return GetPackageSpec(jsonReader, name: null, packageSpecPath, snapshotValue: null);
        }

        private static PackageSpec GetPackageSpec(JsonTextReader jsonReader, string name, string packageSpecPath, string snapshotValue)
        {
            var packageSpec = new PackageSpec();

            List<CompatibilityProfile> compatibilityProfiles = null;
            List<RuntimeDescription> runtimeDescriptions = null;
            var wasPackOptionsSet = false;
            var isMappingsNull = false;

            string filePath = name == null ? null : Path.GetFullPath(packageSpecPath);

            jsonReader.ReadObject(propertyName =>
            {
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    return;
                }

                switch (propertyName)
                {
#pragma warning disable CS0612 // Type or member is obsolete
                    case "authors":
                        packageSpec.Authors = ReadStringArray(jsonReader) ?? Array.Empty<string>();
                        break;

                    case "buildOptions":
                        ReadBuildOptions(jsonReader, packageSpec);
                        break;

                    case "contentFiles":
                        List<string> contentFiles = jsonReader.ReadStringArrayAsList();

                        if (contentFiles != null)
                        {
                            packageSpec.ContentFiles = contentFiles;
                        }
                        break;

                    case "copyright":
                        packageSpec.Copyright = jsonReader.ReadNextTokenAsString();
                        break;
#pragma warning restore CS0612 // Type or member is obsolete

                    case "dependencies":
                        ReadDependencies(
                            jsonReader,
                            packageSpec.Dependencies,
                            filePath,
                            isGacOrFrameworkReference: false);
                        break;

#pragma warning disable CS0612 // Type or member is obsolete
                    case "description":
                        packageSpec.Description = jsonReader.ReadNextTokenAsString();
                        break;
#pragma warning restore CS0612 // Type or member is obsolete

                    case "frameworks":
                        ReadFrameworks(jsonReader, packageSpec);
                        break;

#pragma warning disable CS0612 // Type or member is obsolete
                    case "language":
                        packageSpec.Language = jsonReader.ReadNextTokenAsString();
                        break;

                    case "packInclude":
                        ReadPackInclude(jsonReader, packageSpec);
                        break;

                    case "packOptions":
                        ReadPackOptions(jsonReader, packageSpec, ref isMappingsNull);
                        wasPackOptionsSet = true;
                        break;
#pragma warning restore CS0612 // Type or member is obsolete

                    case "restore":
                        ReadMSBuildMetadata(jsonReader, packageSpec);
                        break;

                    case "runtimes":
                        runtimeDescriptions = ReadRuntimes(jsonReader);
                        break;

#pragma warning disable CS0612 // Type or member is obsolete
                    case "scripts":
                        ReadScripts(jsonReader, packageSpec);
                        break;
#pragma warning restore CS0612 // Type or member is obsolete

                    case "supports":
                        compatibilityProfiles = ReadSupports(jsonReader);
                        break;

                    case "title":
                        packageSpec.Title = jsonReader.ReadNextTokenAsString();
                        break;

                    case "version":
                        string version = jsonReader.ReadAsString();

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
                        break;
                }
            });

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

        private static PackageType CreatePackageType(JsonTextReader jsonReader)
        {
            var name = (string)jsonReader.Value;

            return new PackageType(name, Packaging.Core.PackageType.EmptyVersion);
        }

        private static PackageType CreatePackageType(ref Utf8JsonReader jsonReader)
        {
            var name = jsonReader.GetString();

            return new PackageType(name, Packaging.Core.PackageType.EmptyVersion);
        }

        [Obsolete]
        private static void ReadBuildOptions(JsonTextReader jsonReader, PackageSpec packageSpec)
        {
            packageSpec.BuildOptions = new BuildOptions();

            jsonReader.ReadObject(buildOptionsPropertyName =>
            {
                if (buildOptionsPropertyName == "outputName")
                {
                    packageSpec.BuildOptions.OutputName = jsonReader.ReadNextTokenAsString();
                }
            });
        }

        [Obsolete]
        private static void ReadBuildOptions(ref Utf8JsonReader jsonReader, PackageSpec packageSpec)
        {
            packageSpec.BuildOptions = new BuildOptions();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    var buildOptionsPropertyName = jsonReader.GetString();

                    if (buildOptionsPropertyName == "outputName")
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
            JsonTextReader jsonReader,
            IDictionary<string, CentralPackageVersion> centralPackageVersions,
            string filePath)
        {
            jsonReader.ReadObject(propertyName =>
            {
                int line = jsonReader.LineNumber;
                int column = jsonReader.LinePosition;

                if (string.IsNullOrEmpty(propertyName))
                {
                    throw FileFormatException.Create(
                        "Unable to resolve central version ''.",
                        line,
                        column,
                        filePath);
                }

                string version = jsonReader.ReadNextTokenAsString();

                if (string.IsNullOrEmpty(version))
                {
                    throw FileFormatException.Create(
                        "The version cannot be null or empty.",
                        line,
                        column,
                        filePath);
                }

                centralPackageVersions[propertyName] = new CentralPackageVersion(propertyName, VersionRange.Parse(version));
            });
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

        private static CompatibilityProfile ReadCompatibilityProfile(JsonTextReader jsonReader, string profileName)
        {
            List<FrameworkRuntimePair> sets = null;

            jsonReader.ReadObject(propertyName =>
            {
                sets = sets ?? new List<FrameworkRuntimePair>();

                IEnumerable<FrameworkRuntimePair> profiles = ReadCompatibilitySets(jsonReader, propertyName);

                sets.AddRange(profiles);
            });

            return new CompatibilityProfile(profileName, sets ?? Enumerable.Empty<FrameworkRuntimePair>());
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

        private static IEnumerable<FrameworkRuntimePair> ReadCompatibilitySets(JsonTextReader jsonReader, string compatibilitySetName)
        {
            NuGetFramework framework = NuGetFramework.Parse(compatibilitySetName);

            IReadOnlyList<string> values = jsonReader.ReadStringOrArrayOfStringsAsReadOnlyList() ?? Array.Empty<string>();

            foreach (string value in values)
            {
                yield return new FrameworkRuntimePair(framework, value);
            }
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
                    if (jsonReader.TokenType == JsonTokenType.PropertyName)
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
                                    var dependenciesPropertyName = jsonReader.GetString();
                                    IEnumerable<string> values = null;
                                    switch (dependenciesPropertyName)
                                    {
                                        case "autoReferenced":
                                            autoReferenced = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpecPath);
                                            break;
                                        case "exclude":
                                            values = jsonReader.ReadDelimitedString();
                                            dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                            break;
                                        case "generatePathProperty":
                                            generatePathProperty = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpecPath);
                                            break;
                                        case "include":
                                            values = jsonReader.ReadDelimitedString();
                                            dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                            break;
                                        case "noWarn":
                                            noWarn = ReadNuGetLogCodesList(ref jsonReader);
                                            break;
                                        case "suppressParent":
                                            values = jsonReader.ReadDelimitedString();
                                            suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                            break;
                                        case "target":
                                            targetFlagsValue = ReadTarget(ref jsonReader, packageSpecPath, targetFlagsValue);
                                            break;
                                        case "version":
                                            if (jsonReader.ReadNextToken())
                                            {
                                                //versionLine = jsonReader.LineNumber;
                                                //versionColumn = jsonReader.LinePosition;

                                                dependencyVersionValue = jsonReader.GetString();
                                            }
                                            break;
                                        case "versionOverride":
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
                                            break;
                                        case "versionCentrallyManaged":
                                            versionCentrallyManaged = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpecPath);
                                            break;
                                        case "aliases":
                                            aliases = jsonReader.GetString();
                                            break;
                                        default:
                                            jsonReader.Skip();
                                            break;
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
        }

        internal static void ReadDependencies(
            JsonTextReader jsonReader,
            IList<LibraryDependency> results,
            string packageSpecPath,
            bool isGacOrFrameworkReference)
        {
            jsonReader.ReadObject(propertyName =>
            {
                if (string.IsNullOrEmpty(propertyName))
                {
                    // Advance the reader's position to be able to report the line and column for the property value.
                    jsonReader.ReadNextToken();

                    throw FileFormatException.Create(
                        "Unable to resolve dependency ''.",
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }

                // Support
                // "dependencies" : {
                //    "Name" : "1.0"
                // }

                if (jsonReader.ReadNextToken())
                {
                    int dependencyValueLine = jsonReader.LineNumber;
                    int dependencyValueColumn = jsonReader.LinePosition;
                    var versionLine = 0;
                    var versionColumn = 0;

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

                    if (jsonReader.TokenType == JsonToken.String)
                    {
                        dependencyVersionValue = (string)jsonReader.Value;
                    }
                    else if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        jsonReader.ReadProperties(dependenciesPropertyName =>
                        {
                            IEnumerable<string> values = null;

                            switch (dependenciesPropertyName)
                            {
                                case "autoReferenced":
                                    autoReferenced = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpecPath);
                                    break;

                                case "exclude":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "generatePathProperty":
                                    generatePathProperty = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpecPath);
                                    break;

                                case "include":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "noWarn":
                                    noWarn = ReadNuGetLogCodesList(jsonReader);
                                    break;

                                case "suppressParent":
                                    values = jsonReader.ReadDelimitedString();
                                    suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "target":
                                    targetFlagsValue = ReadTarget(jsonReader, packageSpecPath, targetFlagsValue);
                                    break;

                                case "version":
                                    if (jsonReader.ReadNextToken())
                                    {
                                        versionLine = jsonReader.LineNumber;
                                        versionColumn = jsonReader.LinePosition;

                                        dependencyVersionValue = (string)jsonReader.Value;
                                    }
                                    break;
                                case "versionOverride":
                                    if (jsonReader.ReadNextToken())
                                    {
                                        try
                                        {
                                            versionOverride = VersionRange.Parse((string)jsonReader.Value);
                                        }
                                        catch (Exception ex)
                                        {
                                            throw FileFormatException.Create(
                                                ex,
                                                jsonReader.LineNumber,
                                                jsonReader.LinePosition,
                                                packageSpecPath);
                                        }
                                    }
                                    break;
                                case "versionCentrallyManaged":
                                    versionCentrallyManaged = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpecPath);
                                    break;

                                case "aliases":
                                    aliases = jsonReader.ReadAsString();
                                    break;
                            }
                        });
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

                    // Projects and References may have empty version ranges, Packages may not
                    if (dependencyVersionRange == null)
                    {
                        if ((targetFlagsValue & LibraryDependencyTarget.Package) == LibraryDependencyTarget.Package)
                        {
                            throw FileFormatException.Create(
                                new ArgumentException(Strings.MissingVersionOnDependency),
                                dependencyValueLine,
                                dependencyValueColumn,
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
            });
        }

        internal static void ReadCentralTransitiveDependencyGroup(
            JsonTextReader jsonReader,
            IList<LibraryDependency> results,
            string packageSpecPath)
        {
            jsonReader.ReadObject(propertyName =>
            {
                if (string.IsNullOrEmpty(propertyName))
                {
                    // Advance the reader's position to be able to report the line and column for the property value.
                    jsonReader.ReadNextToken();

                    throw FileFormatException.Create(
                        "Unable to resolve dependency ''.",
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }

                if (jsonReader.ReadNextToken())
                {
                    int dependencyValueLine = jsonReader.LineNumber;
                    int dependencyValueColumn = jsonReader.LinePosition;
                    var versionLine = 0;
                    var versionColumn = 0;

                    var dependencyIncludeFlagsValue = LibraryIncludeFlags.All;
                    var dependencyExcludeFlagsValue = LibraryIncludeFlags.None;
                    var suppressParentFlagsValue = LibraryIncludeFlagUtils.DefaultSuppressParent;
                    string dependencyVersionValue = null;

                    if (jsonReader.TokenType == JsonToken.String)
                    {
                        dependencyVersionValue = (string)jsonReader.Value;
                    }
                    else if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        jsonReader.ReadProperties(dependenciesPropertyName =>
                        {
                            IEnumerable<string> values = null;

                            switch (dependenciesPropertyName)
                            {
                                case "exclude":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "include":
                                    values = jsonReader.ReadDelimitedString();
                                    dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "suppressParent":
                                    values = jsonReader.ReadDelimitedString();
                                    suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                    break;

                                case "version":
                                    if (jsonReader.ReadNextToken())
                                    {
                                        versionLine = jsonReader.LineNumber;
                                        versionColumn = jsonReader.LinePosition;
                                        dependencyVersionValue = (string)jsonReader.Value;
                                    }
                                    break;

                                default:
                                    break;
                            }
                        });
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
            });
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
                                var dependenciesPropertyName = jsonReader.GetString();
                                IEnumerable<string> values = null;

                                switch (dependenciesPropertyName)
                                {
                                    case "exclude":
                                        values = jsonReader.ReadDelimitedString();
                                        dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                        break;

                                    case "include":
                                        values = jsonReader.ReadDelimitedString();
                                        dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                        break;

                                    case "suppressParent":
                                        values = jsonReader.ReadDelimitedString();
                                        suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(values);
                                        break;

                                    case "version":
                                        if (jsonReader.ReadNextToken())
                                        {
                                            versionLine = -1;
                                            versionColumn = -1;
                                            dependencyVersionValue = jsonReader.GetString();
                                        }
                                        break;
                                    default:
                                        jsonReader.Skip();
                                        break;
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
            JsonTextReader jsonReader,
            IList<DownloadDependency> downloadDependencies,
            string packageSpecPath)
        {
            var seenIds = new HashSet<string>();

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.StartArray)
            {
                do
                {
                    string name = null;
                    string versionValue = null;
                    var isNameDefined = false;
                    var isVersionDefined = false;
                    int line = jsonReader.LineNumber;
                    int column = jsonReader.LinePosition;
                    int versionLine = 0;
                    int versionColumn = 0;

                    jsonReader.ReadObject(propertyName =>
                    {
                        switch (propertyName)
                        {
                            case "name":
                                isNameDefined = true;
                                name = jsonReader.ReadNextTokenAsString();
                                break;

                            case "version":
                                isVersionDefined = true;
                                versionValue = jsonReader.ReadNextTokenAsString();
                                versionLine = jsonReader.LineNumber;
                                versionColumn = jsonReader.LinePosition;
                                break;
                        }
                    }, out line, out column);

                    if (jsonReader.TokenType == JsonToken.EndArray)
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
                } while (jsonReader.TokenType == JsonToken.EndObject);
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
                            if (jsonReader.ValueTextEquals("name"))
                            {
                                isNameDefined = true;
                                name = jsonReader.ReadNextTokenAsString();
                            }
                            else if (jsonReader.ValueTextEquals("version"))
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

        private static IReadOnlyList<string> ReadEnumerableOfString(JsonTextReader jsonReader)
        {
            string value = jsonReader.ReadNextTokenAsString();

            return value.Split(DelimitedStringSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        private static IReadOnlyList<string> ReadEnumerableOfString(ref Utf8JsonReader jsonReader)
        {
            string value = jsonReader.ReadNextTokenAsString();

            return value.Split(DelimitedStringSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ReadFrameworkReferences(
            JsonTextReader jsonReader,
            ISet<FrameworkDependency> frameworkReferences,
            string packageSpecPath)
        {
            jsonReader.ReadObject(frameworkName =>
            {
                if (string.IsNullOrEmpty(frameworkName))
                {
                    // Advance the reader's position to be able to report the line and column for the property value.
                    jsonReader.ReadNextToken();

                    throw FileFormatException.Create(
                        "Unable to resolve frameworkReference.",
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }

                var privateAssets = FrameworkDependencyFlagsUtils.Default;

                jsonReader.ReadObject(propertyName =>
                {
                    if (propertyName == "privateAssets")
                    {
                        IEnumerable<string> strings = ReadEnumerableOfString(jsonReader);

                        privateAssets = FrameworkDependencyFlagsUtils.GetFlags(strings);
                    }
                });

                frameworkReferences.Add(new FrameworkDependency(frameworkName, privateAssets));
            });
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
                            var propertyName = jsonReader.GetString();
                            if (propertyName == "privateAssets")
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

        private static void ReadFrameworks(JsonTextReader jsonReader, PackageSpec packageSpec)
        {
            jsonReader.ReadObject(_ =>
            {
                var frameworkLine = 0;
                var frameworkColumn = 0;

                try
                {
                    ReadTargetFrameworks(packageSpec, jsonReader, out frameworkLine, out frameworkColumn);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, frameworkLine, frameworkColumn, packageSpec.FilePath);
                }
            });
        }

        private static void ReadFrameworks(ref Utf8JsonReader reader, PackageSpec packageSpec, string packageSpecPath)
        {
            if (reader.ReadNextToken() && reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.ReadNextToken() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    try
                    {
                        ReadTargetFrameworks(packageSpec, ref reader, packageSpecPath);
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, -1, -1, packageSpecPath);
                    }
                }
            }
        }

        private static void ReadImports(PackageSpec packageSpec, JsonTextReader jsonReader, TargetFrameworkInformation targetFrameworkInformation)
        {
            int lineNumber = jsonReader.LineNumber;
            int linePosition = jsonReader.LinePosition;

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
                            lineNumber,
                            linePosition,
                            packageSpec.FilePath);
                    }

                    targetFrameworkInformation.Imports.Add(framework);
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

        private static void ReadMappings(JsonTextReader jsonReader, string mappingKey, IDictionary<string, IncludeExcludeFiles> mappings)
        {
            if (jsonReader.ReadNextToken())
            {
                switch (jsonReader.TokenType)
                {
                    case JsonToken.String:
                        {
                            var files = new IncludeExcludeFiles()
                            {
                                Include = new[] { (string)jsonReader.Value }
                            };

                            mappings.Add(mappingKey, files);
                        }
                        break;

                    case JsonToken.StartArray:
                        {
                            IReadOnlyList<string> include = jsonReader.ReadStringArrayAsReadOnlyListFromArrayStart();

                            var files = new IncludeExcludeFiles()
                            {
                                Include = include
                            };

                            mappings.Add(mappingKey, files);
                        }
                        break;

                    case JsonToken.StartObject:
                        {
                            IReadOnlyList<string> excludeFiles = null;
                            IReadOnlyList<string> exclude = null;
                            IReadOnlyList<string> includeFiles = null;
                            IReadOnlyList<string> include = null;

                            jsonReader.ReadProperties(filesPropertyName =>
                            {
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
                                }
                            });

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
                                    default:
                                        jsonReader.Skip();
                                        break;
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

        private static void ReadMSBuildMetadata(JsonTextReader jsonReader, PackageSpec packageSpec)
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

            jsonReader.ReadObject(propertyName =>
            {
                switch (propertyName)
                {
                    case "centralPackageVersionsManagementEnabled":
                        centralPackageVersionsManagementEnabled = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "centralPackageVersionOverrideDisabled":
                        centralPackageVersionOverrideDisabled = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "CentralPackageTransitivePinningEnabled":
                        CentralPackageTransitivePinningEnabled = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "configFilePaths":
                        configFilePaths = jsonReader.ReadStringArrayAsList();
                        break;

                    case "crossTargeting":
                        crossTargeting = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "fallbackFolders":
                        fallbackFolders = jsonReader.ReadStringArrayAsList();
                        break;

                    case "files":
                        jsonReader.ReadObject(filePropertyName =>
                        {
                            files = files ?? new List<ProjectRestoreMetadataFile>();

                            files.Add(new ProjectRestoreMetadataFile(filePropertyName, jsonReader.ReadNextTokenAsString()));
                        });
                        break;

                    case "frameworks":
                        targetFrameworks = ReadTargetFrameworks(jsonReader);
                        break;

                    case "legacyPackagesDirectory":
                        legacyPackagesDirectory = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
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
                            && Enum.TryParse<ProjectStyle>(projectStyleString, ignoreCase: true, result: out ProjectStyle projectStyleValue))
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

                        jsonReader.ReadObject(restoreLockPropertiesPropertyName =>
                        {
                            switch (restoreLockPropertiesPropertyName)
                            {
                                case "nuGetLockFilePath":
                                    nuGetLockFilePath = jsonReader.ReadNextTokenAsString();
                                    break;

                                case "restoreLockedMode":
                                    restoreLockedMode = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                                    break;

                                case "restorePackagesWithLockFile":
                                    restorePackagesWithLockFile = jsonReader.ReadNextTokenAsString();
                                    break;
                            }
                        });

                        restoreLockProperties = new RestoreLockProperties(restorePackagesWithLockFile, nuGetLockFilePath, restoreLockedMode);
                        break;

                    case "restoreAuditProperties":
                        string enableAudit = null, auditLevel = null, auditMode = null;
                        jsonReader.ReadObject(auditPropertyName =>
                        {

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
                            }
                        });
                        auditProperties = new RestoreAuditProperties()
                        {
                            EnableAudit = enableAudit,
                            AuditLevel = auditLevel,
                            AuditMode = auditMode,
                        };
                        break;

                    case "skipContentFileWrite":
                        skipContentFileWrite = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "sources":
                        jsonReader.ReadObject(sourcePropertyName =>
                        {
                            sources = sources ?? new List<PackageSource>();

                            sources.Add(new PackageSource(sourcePropertyName));
                        });
                        break;

                    case "validateRuntimeAssets":
                        validateRuntimeAssets = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "warningProperties":
                        var allWarningsAsErrors = false;
                        var noWarn = new HashSet<NuGetLogCode>();
                        var warnAsError = new HashSet<NuGetLogCode>();
                        var warningsNotAsErrors = new HashSet<NuGetLogCode>();

                        jsonReader.ReadObject(warningPropertiesPropertyName =>
                        {
                            switch (warningPropertiesPropertyName)
                            {
                                case "allWarningsAsErrors":
                                    allWarningsAsErrors = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                                    break;

                                case "noWarn":
                                    ReadNuGetLogCodes(jsonReader, noWarn);
                                    break;

                                case "warnAsError":
                                    ReadNuGetLogCodes(jsonReader, warnAsError);
                                    break;

                                case "warnNotAsError":
                                    ReadNuGetLogCodes(jsonReader, warningsNotAsErrors);
                                    break;
                            }
                        });

                        warningProperties = new WarningProperties(warnAsError, noWarn, allWarningsAsErrors, warningsNotAsErrors);
                        break;
                }
            });

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
                                && Enum.TryParse<ProjectStyle>(projectStyleString, ignoreCase: true, result: out ProjectStyle projectStyleValue))
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

        private static bool ReadNextTokenAsBoolOrFalse(JsonTextReader jsonReader, string filePath)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.Boolean)
            {
                try
                {
                    return (bool)jsonReader.Value;
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, jsonReader.LineNumber, jsonReader.LinePosition, filePath);
                }
            }

            return false;
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

        private static void ReadNuGetLogCodes(JsonTextReader jsonReader, HashSet<NuGetLogCode> hashCodes)
        {
            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.StartArray)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType == JsonToken.String && Enum.TryParse((string)jsonReader.Value, out NuGetLogCode code))
                    {
                        hashCodes.Add(code);
                    }
                }
            }
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

        private static List<NuGetLogCode> ReadNuGetLogCodesList(JsonTextReader jsonReader)
        {
            List<NuGetLogCode> items = null;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.StartArray)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType == JsonToken.String && Enum.TryParse((string)jsonReader.Value, out NuGetLogCode code))
                    {
                        items = items ?? new List<NuGetLogCode>();

                        items.Add(code);
                    }
                }
            }

            return items;
        }

        private static List<NuGetLogCode> ReadNuGetLogCodesList(ref Utf8JsonReader jsonReader)
        {
            List<NuGetLogCode> items = null;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartArray)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonTokenType.EndArray)
                {
                    if (jsonReader.TokenType == JsonTokenType.String && Enum.TryParse((string)jsonReader.GetString(), out NuGetLogCode code))
                    {
                        items = items ?? new List<NuGetLogCode>();

                        items.Add(code);
                    }
                }
            }

            return items;
        }

        private static void ReadPackageTypes(PackageSpec packageSpec, JsonTextReader jsonReader)
        {
            var errorLine = 0;
            var errorColumn = 0;

            IReadOnlyList<PackageType> packageTypes = null;
            PackageType packageType = null;

            try
            {
                if (jsonReader.ReadNextToken())
                {
                    errorLine = jsonReader.LineNumber;
                    errorColumn = jsonReader.LinePosition;

                    switch (jsonReader.TokenType)
                    {
                        case JsonToken.String:
                            packageType = CreatePackageType(jsonReader);

                            packageTypes = new[] { packageType };
                            break;

                        case JsonToken.StartArray:
                            var types = new List<PackageType>();

                            while (jsonReader.ReadNextToken() && jsonReader.TokenType != JsonToken.EndArray)
                            {
                                if (jsonReader.TokenType != JsonToken.String)
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

                                packageType = CreatePackageType(jsonReader);

                                types.Add(packageType);
                            }

                            packageTypes = types;
                            break;

                        case JsonToken.Null:
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
        private static void ReadPackInclude(JsonTextReader jsonReader, PackageSpec packageSpec)
        {
            jsonReader.ReadObject(propertyName =>
            {
                string propertyValue = jsonReader.ReadAsString();

                packageSpec.PackInclude.Add(new KeyValuePair<string, string>(propertyName, propertyValue));
            });
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
        private static void ReadPackOptions(JsonTextReader jsonReader, PackageSpec packageSpec, ref bool isMappingsNull)
        {
            var wasMappingsRead = false;

            bool isPackOptionsValueAnObject = jsonReader.ReadObject(propertyName =>
            {
                switch (propertyName)
                {
                    case "files":
                        wasMappingsRead = ReadPackOptionsFiles(packageSpec, jsonReader, wasMappingsRead);
                        break;

                    case "iconUrl":
                        packageSpec.IconUrl = jsonReader.ReadNextTokenAsString();
                        break;

                    case "licenseUrl":
                        packageSpec.LicenseUrl = jsonReader.ReadNextTokenAsString();
                        break;

                    case "owners":
                        string[] owners = ReadStringArray(jsonReader);

                        if (owners != null)
                        {
                            packageSpec.Owners = owners;
                        }
                        break;

                    case "packageType":
                        ReadPackageTypes(packageSpec, jsonReader);
                        break;

                    case "projectUrl":
                        packageSpec.ProjectUrl = jsonReader.ReadNextTokenAsString();
                        break;

                    case "releaseNotes":
                        packageSpec.ReleaseNotes = jsonReader.ReadNextTokenAsString();
                        break;

                    case "requireLicenseAcceptance":
                        packageSpec.RequireLicenseAcceptance = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "summary":
                        packageSpec.Summary = jsonReader.ReadNextTokenAsString();
                        break;

                    case "tags":
                        string[] tags = ReadStringArray(jsonReader);

                        if (tags != null)
                        {
                            packageSpec.Tags = tags;
                        }
                        break;
                }
            });

            isMappingsNull = isPackOptionsValueAnObject && !wasMappingsRead;
        }

        [Obsolete]
        private static void ReadPackOptions(ref Utf8JsonReader jsonReader, JsonSerializerOptions options, PackageSpec packageSpec, ref bool isMappingsNull, string filePath)
        {
            var wasMappingsRead = false;
            bool isPackOptionsValueAnObject = false;
            var stringArrayConverter = (System.Text.Json.Serialization.JsonConverter<string[]>)options.GetConverter(typeof(string[]));

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
        private static bool ReadPackOptionsFiles(PackageSpec packageSpec, JsonTextReader jsonReader, bool wasMappingsRead)
        {
            IReadOnlyList<string> excludeFiles = null;
            IReadOnlyList<string> exclude = null;
            IReadOnlyList<string> includeFiles = null;
            IReadOnlyList<string> include = null;

            jsonReader.ReadObject(filesPropertyName =>
            {
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
                        jsonReader.ReadObject(mappingsPropertyName =>
                        {
                            wasMappingsRead = true;

                            ReadMappings(jsonReader, mappingsPropertyName, packageSpec.PackOptions.Mappings);
                        });
                        break;
                }
            });

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

        private static RuntimeDependencySet ReadRuntimeDependencySet(JsonTextReader jsonReader, string dependencySetName)
        {
            List<RuntimePackageDependency> dependencies = null;

            jsonReader.ReadObject(propertyName =>
            {
                dependencies ??= new List<RuntimePackageDependency>();

                var dependency = new RuntimePackageDependency(propertyName, VersionRange.Parse(jsonReader.ReadNextTokenAsString()));

                dependencies.Add(dependency);
            });

            return new RuntimeDependencySet(
                dependencySetName,
                dependencies);
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

        private static RuntimeDescription ReadRuntimeDescription(JsonTextReader jsonReader, string runtimeName)
        {
            List<string> inheritedRuntimes = null;
            List<RuntimeDependencySet> additionalDependencies = null;

            jsonReader.ReadObject(propertyName =>
            {
                if (propertyName == "#import")
                {
                    inheritedRuntimes = jsonReader.ReadStringArrayAsList();
                }
                else
                {
                    additionalDependencies ??= new List<RuntimeDependencySet>();

                    RuntimeDependencySet dependency = ReadRuntimeDependencySet(jsonReader, propertyName);

                    additionalDependencies.Add(dependency);
                }
            });

            return new RuntimeDescription(
                runtimeName,
                inheritedRuntimes,
                additionalDependencies);
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

        private static List<RuntimeDescription> ReadRuntimes(JsonTextReader jsonReader)
        {
            var runtimeDescriptions = new List<RuntimeDescription>();

            jsonReader.ReadObject(propertyName =>
            {
                RuntimeDescription runtimeDescription = ReadRuntimeDescription(jsonReader, propertyName);

                runtimeDescriptions.Add(runtimeDescription);
            });

            return runtimeDescriptions;
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
        private static void ReadScripts(JsonTextReader jsonReader, PackageSpec packageSpec)
        {
            jsonReader.ReadObject(propertyName =>
            {
                if (jsonReader.ReadNextToken())
                {
                    if (jsonReader.TokenType == JsonToken.String)
                    {
                        packageSpec.Scripts[propertyName] = new string[] { (string)jsonReader.Value };
                    }
                    else if (jsonReader.TokenType == JsonToken.StartArray)
                    {
                        var list = new List<string>();

                        while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonToken.String)
                        {
                            list.Add((string)jsonReader.Value);
                        }

                        packageSpec.Scripts[propertyName] = list;
                    }
                    else
                    {
                        throw FileFormatException.Create(
                            string.Format(CultureInfo.CurrentCulture, "The value of a script in '{0}' can only be a string or an array of strings", PackageSpec.PackageSpecFileName),
                            jsonReader.LineNumber,
                            jsonReader.LinePosition,
                            packageSpec.FilePath);
                    }
                }
            });
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

        private static string[] ReadStringArray(JsonTextReader jsonReader)
        {
            List<string> list = jsonReader.ReadStringArrayAsList();

            return list?.ToArray();
        }

        private static List<CompatibilityProfile> ReadSupports(JsonTextReader jsonReader)
        {
            var compatibilityProfiles = new List<CompatibilityProfile>();

            jsonReader.ReadObject(propertyName =>
            {
                CompatibilityProfile compatibilityProfile = ReadCompatibilityProfile(jsonReader, propertyName);

                compatibilityProfiles.Add(compatibilityProfile);
            });

            return compatibilityProfiles;
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
            JsonTextReader jsonReader,
            string packageSpecPath,
            LibraryDependencyTarget targetFlagsValue)
        {
            if (jsonReader.ReadNextToken())
            {
                var targetString = (string)jsonReader.Value;

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
                        jsonReader.LineNumber,
                        jsonReader.LinePosition,
                        packageSpecPath);
                }
            }

            return targetFlagsValue;
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

        private static List<ProjectRestoreMetadataFrameworkInfo> ReadTargetFrameworks(JsonTextReader jsonReader)
        {
            var targetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>();

            jsonReader.ReadObject(frameworkPropertyName =>
            {
                NuGetFramework framework = NuGetFramework.Parse(frameworkPropertyName);
                var frameworkGroup = new ProjectRestoreMetadataFrameworkInfo(framework);

                jsonReader.ReadObject(propertyName =>
                {
                    if (propertyName == "projectReferences")
                    {
                        jsonReader.ReadObject(projectReferencePropertyName =>
                        {
                            string excludeAssets = null;
                            string includeAssets = null;
                            string privateAssets = null;
                            string projectReferenceProjectPath = null;

                            jsonReader.ReadObject(projectReferenceObjectPropertyName =>
                            {
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
                                }
                            });

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
                        });
                    }
                    else if (propertyName == "targetAlias")
                    {
                        frameworkGroup.TargetAlias = jsonReader.ReadNextTokenAsString();
                    }
                });

                targetFrameworks.Add(frameworkGroup);
            });

            return targetFrameworks;
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

        private static void ReadTargetFrameworks(PackageSpec packageSpec, JsonTextReader jsonReader, out int frameworkLine, out int frameworkColumn)
        {
            frameworkLine = 0;
            frameworkColumn = 0;

            NuGetFramework frameworkName = NuGetFramework.Parse((string)jsonReader.Value);

            var targetFrameworkInformation = new TargetFrameworkInformation();
            NuGetFramework secondaryFramework = default;
            jsonReader.ReadObject(propertyName =>
            {
                switch (propertyName)
                {
                    case "assetTargetFallback":
                        targetFrameworkInformation.AssetTargetFallback = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;

                    case "secondaryFramework":
                        var secondaryFrameworkString = jsonReader.ReadAsString();
                        if (!string.IsNullOrEmpty(secondaryFrameworkString))
                        {
                            secondaryFramework = NuGetFramework.Parse(secondaryFrameworkString);
                        }
                        break;

                    case "centralPackageVersions":
                        ReadCentralPackageVersions(
                            jsonReader,
                            targetFrameworkInformation.CentralPackageVersions,
                            packageSpec.FilePath);
                        break;

                    case "dependencies":
                        ReadDependencies(
                            jsonReader,
                            targetFrameworkInformation.Dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: false);
                        break;

                    case "downloadDependencies":
                        ReadDownloadDependencies(
                            jsonReader,
                            targetFrameworkInformation.DownloadDependencies,
                            packageSpec.FilePath);
                        break;

                    case "frameworkAssemblies":
                        ReadDependencies(
                            jsonReader,
                            targetFrameworkInformation.Dependencies,
                            packageSpec.FilePath,
                            isGacOrFrameworkReference: true);
                        break;

                    case "frameworkReferences":
                        ReadFrameworkReferences(
                            jsonReader,
                            targetFrameworkInformation.FrameworkReferences,
                            packageSpec.FilePath);
                        break;

                    case "imports":
                        ReadImports(packageSpec, jsonReader, targetFrameworkInformation);
                        break;

                    case "runtimeIdentifierGraphPath":
                        targetFrameworkInformation.RuntimeIdentifierGraphPath = jsonReader.ReadNextTokenAsString();
                        break;

                    case "targetAlias":
                        targetFrameworkInformation.TargetAlias = jsonReader.ReadNextTokenAsString();
                        break;

                    case "warn":
                        targetFrameworkInformation.Warn = ReadNextTokenAsBoolOrFalse(jsonReader, packageSpec.FilePath);
                        break;
                }
            }, out frameworkLine, out frameworkColumn);

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

        private static void ReadTargetFrameworks(PackageSpec packageSpec, ref Utf8JsonReader jsonReader, string packageSpecPath)
        {
            var frameworkName = NuGetFramework.Parse(jsonReader.GetString());

            var targetFrameworkInformation = new TargetFrameworkInformation();
            NuGetFramework secondaryFramework = default;

            if (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.StartObject)
            {
                while (jsonReader.ReadNextToken() && jsonReader.TokenType == JsonTokenType.PropertyName)
                {
                    if (jsonReader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (jsonReader.ValueTextEquals("assetTargetFallback"))
                        {
                            targetFrameworkInformation.AssetTargetFallback = ReadNextTokenAsBoolOrFalse(ref jsonReader, packageSpecPath);
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
                                packageSpecPath);
                        }
                        else if (jsonReader.ValueTextEquals("dependencies"))
                        {
                            ReadDependencies(
                                ref jsonReader,
                                targetFrameworkInformation.Dependencies,
                                packageSpecPath,
                                isGacOrFrameworkReference: false);
                        }
                        else if (jsonReader.ValueTextEquals("downloadDependencies"))
                        {
                            ReadDownloadDependencies(
                                ref jsonReader,
                                targetFrameworkInformation.DownloadDependencies,
                                packageSpecPath);
                        }
                        else if (jsonReader.ValueTextEquals("frameworkAssemblies"))
                        {
                            ReadDependencies(
                                ref jsonReader,
                                targetFrameworkInformation.Dependencies,
                                packageSpecPath,
                                isGacOrFrameworkReference: true);
                        }
                        else if (jsonReader.ValueTextEquals("frameworkReferences"))
                        {
                            ReadFrameworkReferences(
                                ref jsonReader,
                                targetFrameworkInformation.FrameworkReferences,
                                packageSpecPath);
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
