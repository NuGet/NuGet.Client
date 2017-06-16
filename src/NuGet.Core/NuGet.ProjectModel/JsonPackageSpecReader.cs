// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    public class JsonPackageSpecReader
    {
        public static readonly string RestoreOptions = "restore";
        public static readonly string RestoreSettings = "restoreSettings";
        public static readonly string HideWarningsAndErrors = "hideWarningsAndErrors";
        public static readonly string PackOptions = "packOptions";
        public static readonly string PackageType = "packageType";
        public static readonly string Files = "files";

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

        public static PackageSpec GetPackageSpec(JObject json)
        {
            return GetPackageSpec(json, name: null, packageSpecPath: null, snapshotValue: null);
        }

        public static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath, string snapshotValue)
        {
            // Load the raw JSON into the package spec object
            JObject rawPackageSpec;

            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                try
                {
                    rawPackageSpec = JObject.Load(reader);
                }
                catch (JsonReaderException ex)
                {
                    throw FileFormatException.Create(ex, packageSpecPath);
                }
            }

            return GetPackageSpec(rawPackageSpec, name, packageSpecPath, snapshotValue);
        }

        public static PackageSpec GetPackageSpec(JObject rawPackageSpec, string name, string packageSpecPath, string snapshotValue)
        {
            var packageSpec = new PackageSpec();

            // Parse properties we know about
            var version = rawPackageSpec["version"];
            var authors = rawPackageSpec["authors"];
            var contentFiles = rawPackageSpec["contentFiles"];

            packageSpec.Name = name;
            packageSpec.FilePath = name == null ? null : Path.GetFullPath(packageSpecPath);

            if (version != null)
            {
                try
                {
                    var versionString = version.Value<string>();
                    packageSpec.HasVersionSnapshot = PackageSpecUtility.IsSnapshotVersion(versionString);
                    packageSpec.Version = PackageSpecUtility.SpecifySnapshot(versionString, snapshotValue);
                }
                catch (Exception ex)
                {
                    var lineInfo = (IJsonLineInfo)version;

                    throw FileFormatException.Create(ex, version, packageSpec.FilePath);
                }
            }

            var packInclude = rawPackageSpec["packInclude"] as JObject;
            if (packInclude != null)
            {
                foreach (var include in packInclude)
                {
                    packageSpec.PackInclude.Add(new KeyValuePair<string, string>(include.Key, include.Value.ToString()));
                }
            }

            packageSpec.Title = rawPackageSpec.GetValue<string>("title");
            packageSpec.Description = rawPackageSpec.GetValue<string>("description");
            packageSpec.Authors = authors == null ? new string[] { } : authors.ValueAsArray<string>();
            packageSpec.ContentFiles = contentFiles == null ? new string[] { } : contentFiles.ValueAsArray<string>();
            packageSpec.Dependencies = new List<LibraryDependency>();
            packageSpec.Copyright = rawPackageSpec.GetValue<string>("copyright");
            packageSpec.Language = rawPackageSpec.GetValue<string>("language");


            var buildOptions = rawPackageSpec["buildOptions"] as JObject;
            if (buildOptions != null)
            {
                packageSpec.BuildOptions = new BuildOptions()
                {
                    OutputName = buildOptions.GetValue<string>("outputName")
                };
            }

            var scripts = rawPackageSpec["scripts"] as JObject;
            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    var value = script.Value;
                    if (value.Type == JTokenType.String)
                    {
                        packageSpec.Scripts[script.Key] = new string[] { value.Value<string>() };
                    }
                    else if (value.Type == JTokenType.Array)
                    {
                        packageSpec.Scripts[script.Key] = script.Value.ValueAsArray<string>();
                    }
                    else
                    {
                        throw FileFormatException.Create(
                            string.Format("The value of a script in '{0}' can only be a string or an array of strings", PackageSpec.PackageSpecFileName),
                            value,
                            packageSpec.FilePath);
                    }
                }
            }

            BuildTargetFrameworks(packageSpec, rawPackageSpec);

            PopulateDependencies(
                packageSpec.FilePath,
                packageSpec.Dependencies,
                rawPackageSpec,
                "dependencies",
                isGacOrFrameworkReference: false);

            packageSpec.PackOptions = GetPackOptions(packageSpec, rawPackageSpec);

            packageSpec.RestoreSettings = GetRestoreSettings(packageSpec, rawPackageSpec);

            packageSpec.RestoreMetadata = GetMSBuildMetadata(packageSpec, rawPackageSpec);

            // Read the runtime graph
            packageSpec.RuntimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(rawPackageSpec);

            // Read the name/path if it exists
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

        private static ProjectRestoreSettings GetRestoreSettings(PackageSpec packageSpec, JObject rawPackageSpec)
        {
            var rawRestoreSettings = rawPackageSpec.Value<JToken>(RestoreSettings) as JObject;
            var restoreSettings = new ProjectRestoreSettings();

            if (rawRestoreSettings != null)
            {
                restoreSettings.HideWarningsAndErrors = GetBoolOrFalse(rawRestoreSettings, HideWarningsAndErrors, packageSpec.FilePath);
            }

            return restoreSettings;
        }

        private static ProjectRestoreMetadata GetMSBuildMetadata(PackageSpec packageSpec, JObject rawPackageSpec)
        {
            var rawMSBuildMetadata = rawPackageSpec.Value<JToken>(RestoreOptions) as JObject;
            if (rawMSBuildMetadata == null)
            {
                return null;
            }

            var msbuildMetadata = new ProjectRestoreMetadata();

            msbuildMetadata.ProjectUniqueName = rawMSBuildMetadata.GetValue<string>("projectUniqueName");
            msbuildMetadata.OutputPath = rawMSBuildMetadata.GetValue<string>("outputPath");

            var projectStyleString = rawMSBuildMetadata.GetValue<string>("projectStyle");

            ProjectStyle projectStyle;
            if (!string.IsNullOrEmpty(projectStyleString)
                && Enum.TryParse<ProjectStyle>(projectStyleString, ignoreCase: true, result: out projectStyle))
            {
                msbuildMetadata.ProjectStyle = projectStyle;
            }

            msbuildMetadata.PackagesPath = rawMSBuildMetadata.GetValue<string>("packagesPath");
            msbuildMetadata.ProjectJsonPath = rawMSBuildMetadata.GetValue<string>("projectJsonPath");
            msbuildMetadata.ProjectName = rawMSBuildMetadata.GetValue<string>("projectName");
            msbuildMetadata.ProjectPath = rawMSBuildMetadata.GetValue<string>("projectPath");
            msbuildMetadata.CrossTargeting = GetBoolOrFalse(rawMSBuildMetadata, "crossTargeting", packageSpec.FilePath);
            msbuildMetadata.LegacyPackagesDirectory = GetBoolOrFalse(rawMSBuildMetadata, "legacyPackagesDirectory", packageSpec.FilePath);
            msbuildMetadata.ValidateRuntimeAssets = GetBoolOrFalse(rawMSBuildMetadata, "validateRuntimeAssets", packageSpec.FilePath);
            msbuildMetadata.SkipContentFileWrite = GetBoolOrFalse(rawMSBuildMetadata, "skipContentFileWrite", packageSpec.FilePath);

            msbuildMetadata.Sources = new List<PackageSource>();

            var sourcesObj = rawMSBuildMetadata.GetValue<JObject>("sources");
            if (sourcesObj != null)
            {
                foreach (var prop in sourcesObj.Properties())
                {
                    msbuildMetadata.Sources.Add(new PackageSource(prop.Name));
                }
            }

            var filesObj = rawMSBuildMetadata.GetValue<JObject>("files");
            if (filesObj != null)
            {
                foreach (var prop in filesObj.Properties())
                {
                    msbuildMetadata.Files.Add(new ProjectRestoreMetadataFile(prop.Name, prop.Value.ToObject<string>()));
                }
            }

            var frameworksObj = rawMSBuildMetadata.GetValue<JObject>("frameworks");
            if (frameworksObj != null)
            {
                foreach (var frameworkProperty in frameworksObj.Properties())
                {
                    var framework = NuGetFramework.Parse(frameworkProperty.Name);
                    var frameworkGroup = new ProjectRestoreMetadataFrameworkInfo(framework);

                    var projectsObj = frameworkProperty.Value.GetValue<JObject>("projectReferences");
                    if (projectsObj != null)
                    {
                        foreach (var prop in projectsObj.Properties())
                        {
                            frameworkGroup.ProjectReferences.Add(new ProjectRestoreReference()
                            {
                                ProjectUniqueName = prop.Name,
                                ProjectPath = prop.Value.GetValue<string>("projectPath"),

                                IncludeAssets = LibraryIncludeFlagUtils.GetFlags(
                                    flags: prop.Value.GetValue<string>("includeAssets"),
                                    defaultFlags: LibraryIncludeFlags.All),

                                ExcludeAssets = LibraryIncludeFlagUtils.GetFlags(
                                    flags: prop.Value.GetValue<string>("excludeAssets"),
                                    defaultFlags: LibraryIncludeFlags.None),

                                PrivateAssets = LibraryIncludeFlagUtils.GetFlags(
                                    flags: prop.Value.GetValue<string>("privateAssets"),
                                    defaultFlags: LibraryIncludeFlagUtils.DefaultSuppressParent),
                            });
                        }
                    }

                    msbuildMetadata.TargetFrameworks.Add(frameworkGroup);
                }
            }
            // Add the config file paths to the equals method
            msbuildMetadata.ConfigFilePaths = new List<string>();

            var configFilePaths = rawMSBuildMetadata.GetValue<JArray>("configFilePaths");
            if (configFilePaths != null)
            {
                foreach (var fallbackFolder in configFilePaths.Select(t => t.Value<string>()))
                {
                    msbuildMetadata.ConfigFilePaths.Add(fallbackFolder);
                }
            }


            msbuildMetadata.FallbackFolders = new List<string>();

            var fallbackObj = rawMSBuildMetadata.GetValue<JArray>("fallbackFolders");
            if (fallbackObj != null)
            {
                foreach (var fallbackFolder in fallbackObj.Select(t => t.Value<string>()))
                {
                    msbuildMetadata.FallbackFolders.Add(fallbackFolder);
                }
            }

            msbuildMetadata.OriginalTargetFrameworks = new List<string>();

            var originalFrameworksObj = rawMSBuildMetadata.GetValue<JArray>("originalTargetFrameworks");
            if (originalFrameworksObj != null)
            {
                foreach (var orignalFramework in originalFrameworksObj.Select(t => t.Value<string>()))
                {
                    msbuildMetadata.OriginalTargetFrameworks.Add(orignalFramework);
                }
            }

            var warningPropertiesObj = rawMSBuildMetadata.GetValue<JObject>("warningProperties");
            if (warningPropertiesObj != null)
            {
                var allWarningsAsErrors = warningPropertiesObj.GetValue<bool>("allWarningsAsErrors");
                var warnAsError = new HashSet<NuGetLogCode>(GetNuGetLogCodeEnumerableFromJArray(warningPropertiesObj["warnAsError"]));
                var noWarn = new HashSet<NuGetLogCode>(GetNuGetLogCodeEnumerableFromJArray(warningPropertiesObj["noWarn"]));

                msbuildMetadata.ProjectWideWarningProperties = new WarningProperties(warnAsError, noWarn, allWarningsAsErrors);
            }

            return msbuildMetadata;
        }

        private static PackOptions GetPackOptions(PackageSpec packageSpec, JObject rawPackageSpec)
        {
            var rawPackOptions = rawPackageSpec.Value<JToken>(PackOptions) as JObject;
            if (rawPackOptions == null)
            {
                packageSpec.Owners = new string[] { };
                packageSpec.Tags = new string[] { };
                return new PackOptions
                {
                    PackageType = new PackageType[0]
                };
            }
            var owners = rawPackOptions["owners"];
            var tags = rawPackOptions["tags"];
            packageSpec.Owners = owners == null ? new string[0] { } : owners.ValueAsArray<string>();
            packageSpec.Tags = tags == null ? new string[0] { } : tags.ValueAsArray<string>();
            packageSpec.ProjectUrl = rawPackOptions.GetValue<string>("projectUrl");
            packageSpec.IconUrl = rawPackOptions.GetValue<string>("iconUrl");
            packageSpec.Summary = rawPackOptions.GetValue<string>("summary");
            packageSpec.ReleaseNotes = rawPackOptions.GetValue<string>("releaseNotes");
            packageSpec.LicenseUrl = rawPackOptions.GetValue<string>("licenseUrl");

            packageSpec.RequireLicenseAcceptance = GetBoolOrFalse(rawPackOptions, "requireLicenseAcceptance", packageSpec.FilePath);

            var rawPackageType = rawPackOptions[PackageType];
            if (rawPackageType != null &&
                rawPackageType.Type != JTokenType.String &&
                (rawPackageType.Type != JTokenType.Array || // The array must be all strings.
                 rawPackageType.Type == JTokenType.Array && rawPackageType.Any(t => t.Type != JTokenType.String)) &&
                rawPackageType.Type != JTokenType.Null)
            {
                throw FileFormatException.Create(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidPackageType,
                        PackageSpec.PackageSpecFileName),
                    rawPackageType,
                    packageSpec.FilePath);
            }

            IEnumerable<string> packageTypeNames;
            if (!TryGetStringEnumerableFromJArray(rawPackageType, out packageTypeNames))
            {
                packageTypeNames = Enumerable.Empty<string>();
            }

            Dictionary<string, IncludeExcludeFiles> mappings = null;
            IncludeExcludeFiles files = null;
            var rawFiles = rawPackOptions[Files] as JObject;
            if (rawFiles != null)
            {
                files = new IncludeExcludeFiles();
                if (!files.HandleIncludeExcludeFiles(rawFiles))
                {
                    files = null;
                }

                var rawMappings = rawFiles["mappings"] as JObject;

                if (rawMappings != null)
                {
                    mappings = new Dictionary<string, IncludeExcludeFiles>();
                    foreach (var pair in rawMappings)
                    {
                        var key = pair.Key;
                        var value = pair.Value;
                        if (value.Type == JTokenType.String ||
                            value.Type == JTokenType.Array)
                        {
                            IEnumerable<string> includeFiles;
                            TryGetStringEnumerableFromJArray(value, out includeFiles);
                            var includeExcludeFiles = new IncludeExcludeFiles()
                            {
                                Include = includeFiles?.ToList()
                            };
                            mappings.Add(key, includeExcludeFiles);
                        }
                        else if (value.Type == JTokenType.Object)
                        {
                            var includeExcludeFiles = new IncludeExcludeFiles();
                            if (includeExcludeFiles.HandleIncludeExcludeFiles(value as JObject))
                            {
                                mappings.Add(key, includeExcludeFiles);
                            }
                        }
                    }
                }
            }

            return new PackOptions
            {
                PackageType = packageTypeNames
                    .Select(name => new PackageType(name, Packaging.Core.PackageType.EmptyVersion))
                    .ToList(),
                IncludeExcludeFiles = files,
                Mappings = mappings
            };
        }

        private static void PopulateDependencies(
            string packageSpecPath,
            IList<LibraryDependency> results,
            JObject settings,
            string propertyName,
            bool isGacOrFrameworkReference)
        {
            var dependencies = settings[propertyName] as JObject;
            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    if (string.IsNullOrEmpty(dependency.Key))
                    {
                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependency.Value,
                            packageSpecPath);
                    }

                    // Support
                    // "dependencies" : {
                    //    "Name" : "1.0"
                    // }

                    var dependencyValue = dependency.Value;
                    var dependencyTypeValue = LibraryDependencyType.Default;

                    var dependencyIncludeFlagsValue = LibraryIncludeFlags.All;
                    var dependencyExcludeFlagsValue = LibraryIncludeFlags.None;
                    var suppressParentFlagsValue = LibraryIncludeFlagUtils.DefaultSuppressParent;
                    var noWarn = new List<NuGetLogCode>();

                    // This method handles both the dependencies and framework assembly sections.
                    // Framework references should be limited to references.
                    // Dependencies should allow everything but framework references.
                    var targetFlagsValue = isGacOrFrameworkReference
                                                    ? LibraryDependencyTarget.Reference
                                                    : LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference;

                    var autoReferenced = false;

                    string dependencyVersionValue = null;
                    var dependencyVersionToken = dependencyValue;

                    if (dependencyValue.Type == JTokenType.String)
                    {
                        dependencyVersionValue = dependencyValue.Value<string>();
                    }
                    else
                    {
                        if (dependencyValue.Type == JTokenType.Object)
                        {
                            dependencyVersionToken = dependencyValue["version"];
                            if (dependencyVersionToken != null
                                && dependencyVersionToken.Type == JTokenType.String)
                            {
                                dependencyVersionValue = dependencyVersionToken.Value<string>();
                            }
                        }

                        IEnumerable<string> strings;
                        if (TryGetStringEnumerable(dependencyValue["type"], out strings))
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(strings);

                            // Types are used at pack time, they should be translated to suppressParent to 
                            // provide a matching effect for project to project references.
                            // This should be set before suppressParent is checked.
                            if (!dependencyTypeValue.Contains(LibraryDependencyTypeFlag.BecomesNupkgDependency))
                            {
                                suppressParentFlagsValue = LibraryIncludeFlags.All;
                            }
                            else if (dependencyTypeValue.Contains(LibraryDependencyTypeFlag.SharedFramework))
                            {
                                dependencyIncludeFlagsValue =
                                    LibraryIncludeFlags.Build |
                                    LibraryIncludeFlags.Compile |
                                    LibraryIncludeFlags.Analyzers;
                            }
                        }

                        if (TryGetStringEnumerable(dependencyValue["include"], out strings))
                        {
                            dependencyIncludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(strings);
                        }

                        if (TryGetStringEnumerable(dependencyValue["exclude"], out strings))
                        {
                            dependencyExcludeFlagsValue = LibraryIncludeFlagUtils.GetFlags(strings);
                        }

                        if (TryGetStringEnumerable(dependencyValue["suppressParent"], out strings))
                        {
                            // This overrides any settings that came from the type property.
                            suppressParentFlagsValue = LibraryIncludeFlagUtils.GetFlags(strings);
                        }

                        noWarn = GetNuGetLogCodeEnumerableFromJArray(dependencyValue["noWarn"])
                            .ToList();

                        var targetToken = dependencyValue["target"];

                        if (targetToken != null)
                        {
                            var targetString = targetToken.Value<string>();

                            targetFlagsValue = LibraryDependencyTargetUtils.Parse(targetString);

                            // Verify that the value specified is package, project, or external project
                            if (!ValidateDependencyTarget(targetFlagsValue))
                            {
                                var message = string.Format(
                                    CultureInfo.CurrentCulture,
                                    Strings.InvalidDependencyTarget,
                                    targetString);

                                throw FileFormatException.Create(message, targetToken, packageSpecPath);
                            }
                        }

                        autoReferenced = GetBoolOrFalse(dependencyValue, "autoReferenced", packageSpecPath);
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
                                dependencyVersionToken,
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
                                dependency.Value,
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

                    results.Add(new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange()
                        {
                            Name = dependency.Key,
                            TypeConstraint = targetFlagsValue,
                            VersionRange = dependencyVersionRange,
                        },
                        Type = dependencyTypeValue,
                        IncludeType = includeFlags,
                        SuppressParent = suppressParentFlagsValue,
                        AutoReferenced = autoReferenced,
                        NoWarn = noWarn.ToList()
                    });
                }
            }
        }

        private static bool TryGetStringEnumerable(JToken token, out IEnumerable<string> result)
        {
            IEnumerable<string> values;
            if (token == null)
            {
                result = null;
                return false;
            }
            else if (token.Type == JTokenType.String)
            {
                values = new[]
                    {
                        token.Value<string>()
                    };
            }
            else
            {
                values = token.Value<string[]>();
            }
            result = values
                .SelectMany(value => value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            return true;
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

        internal static bool TryGetStringEnumerableFromJArray(JToken token, out IEnumerable<string> result)
        {
            IEnumerable<string> values;
            if (token == null)
            {
                result = null;
                return false;
            }
            else if (token.Type == JTokenType.String)
            {
                values = new[]
                    {
                        token.Value<string>()
                    };
            }
            else if (token.Type == JTokenType.Array)
            {
                values = token.ValueAsArray<string>();
            }
            else
            {
                result = null;
                return false;
            }

            result = values;
            return true;
        }

        internal static IEnumerable<NuGetLogCode> GetNuGetLogCodeEnumerableFromJArray(JToken token)
        {
            var items = new List<NuGetLogCode>();
            var array = (JArray)token;
            if (array != null)
            {
                foreach (var child in array)
                {
                    if (child.Type == JTokenType.String && Enum.TryParse(child.Value<string>(), out NuGetLogCode code))
                    {
                        items.Add(code);
                    }
                }
            }
            return items;
        }

        private static void BuildTargetFrameworks(PackageSpec packageSpec, JObject rawPackageSpec)
        {
            // The frameworks node is where target frameworks go
            /*
                {
                    "frameworks": {
                        "net45": {
                        },
                        "aspnet50": {
                        }
                    }
                }
            */

            var frameworks = rawPackageSpec["frameworks"] as JObject;
            if (frameworks != null)
            {
                foreach (var framework in frameworks)
                {
                    try
                    {
                        BuildTargetFrameworkNode(packageSpec, framework, packageSpec.FilePath);
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, framework.Value, packageSpec.FilePath);
                    }
                }
            }
        }

        private static bool BuildTargetFrameworkNode(PackageSpec packageSpec, KeyValuePair<string, JToken> targetFramework, string filePath)
        {
            var frameworkName = GetFramework(targetFramework.Key);

            var properties = targetFramework.Value.Value<JObject>();

            var importFrameworks = GetFrameworksFromArray(properties["imports"], packageSpec);
            var assetTargetFallbackFrameworks = GetFrameworksFromArray(properties["assetTargetFallback"], packageSpec);

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = PackageSpecUtility.GetFallbackFramework(frameworkName, importFrameworks, assetTargetFallbackFrameworks),
                Dependencies = new List<LibraryDependency>(),
                Imports = importFrameworks,
                AssetTargetFallback = assetTargetFallbackFrameworks,
                Warn = GetWarnSetting(properties)
            };

            PopulateDependencies(
                packageSpec.FilePath,
                targetFrameworkInformation.Dependencies,
                properties,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
                packageSpec.FilePath,
                frameworkAssemblies,
                properties,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            frameworkAssemblies.ForEach(d => targetFrameworkInformation.Dependencies.Add(d));

            packageSpec.TargetFrameworks.Add(targetFrameworkInformation);

            return true;
        }

        private static List<NuGetFramework> GetFrameworksFromArray(JToken property, PackageSpec packageSpec)
        {
            var frameworks = new List<NuGetFramework>();

            if (property != null)
            {
                IEnumerable<string> importArray = new List<string>();
                if (TryGetStringEnumerableFromJArray(property, out importArray))
                {
                    frameworks = importArray.Where(p => !string.IsNullOrEmpty(p)).Select(p => NuGetFramework.Parse(p)).ToList();
                }
            }

            if (frameworks.Any(p => !p.IsSpecificFramework))
            {
                throw FileFormatException.Create(
                           string.Format(
                               Strings.Log_InvalidImportFramework,
                               property.ToString().Replace(Environment.NewLine, string.Empty),
                               PackageSpec.PackageSpecFileName),
                           property,
                           packageSpec.FilePath);
            }

            return frameworks;
        }

        private static bool GetWarnSetting(JObject properties)
        {
            var warn = false;

            var warnProperty = properties["warn"];

            if (warnProperty != null)
            {
                warn = warnProperty.ToObject<bool>();
            }

            return warn;
        }

        private static NuGetFramework GetFramework(string key)
        {
            return NuGetFramework.Parse(key);
        }

        /// <summary>
        /// Returns true if the property is set to true. Otherwise false.
        /// </summary>
        private static bool GetBoolOrFalse(JToken parent, string propertyName, string filePath)
        {
            var jObj = parent as JObject;

            if (jObj != null)
            {
                return GetBoolOrFalse(jObj, propertyName, filePath);
            }

            return false;
        }

        /// <summary>
        /// Returns true if the property is set to true. Otherwise false.
        /// </summary>
        private static bool GetBoolOrFalse(JObject parent, string propertyName, string filePath)
        {
            var token = parent[propertyName];

            if (token != null)
            {
                try
                {
                    return parent.GetValue<bool?>(propertyName) ?? false;
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, token, filePath);
                }
            }

            return false;
        }
    }
}
