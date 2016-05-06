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
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class JsonPackageSpecReader
    {
        public static readonly string PackOptions = "packOptions";
        public static readonly string PackageType = "packageType";

        /// <summary>
        /// Load and parse a project.json file
        /// </summary>
        /// <param name="name">project name</param>
        /// <param name="packageSpecPath">file path</param>
        public static PackageSpec GetPackageSpec(string name, string packageSpecPath)
        {
            using (var stream = new FileStream(packageSpecPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return GetPackageSpec(stream, name, packageSpecPath);
            }
        }

        public static PackageSpec GetPackageSpec(string json, string name, string packageSpecPath)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return GetPackageSpec(ms, name, packageSpecPath);
        }

        public static PackageSpec GetPackageSpec(Stream stream, string name, string packageSpecPath)
        {
            // Load the raw JSON into the package spec object
            var reader = new JsonTextReader(new StreamReader(stream));

            JObject rawPackageSpec;

            try
            {
                rawPackageSpec = JObject.Load(reader);
            }
            catch (JsonReaderException ex)
            {
                throw FileFormatException.Create(ex, packageSpecPath);
            }

            var packageSpec = new PackageSpec(rawPackageSpec);

            // Parse properties we know about
            var version = rawPackageSpec["version"];
            var authors = rawPackageSpec["authors"];
            var owners = rawPackageSpec["owners"];
            var tags = rawPackageSpec["tags"];
            var contentFiles = rawPackageSpec["contentFiles"];

            packageSpec.Name = name;
            packageSpec.FilePath = Path.GetFullPath(packageSpecPath);

            if (version == null)
            {
                packageSpec.Version = new NuGetVersion("1.0.0");
                packageSpec.IsDefaultVersion = true;
            }
            else
            {
                try
                {
                    packageSpec.Version = SpecifySnapshot(version.Value<string>(), snapshotValue: string.Empty);
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
            packageSpec.Owners = owners == null ? new string[] { } : owners.ValueAsArray<string>();
            packageSpec.ContentFiles = contentFiles == null ? new string[] { } : contentFiles.ValueAsArray<string>();
            packageSpec.Dependencies = new List<LibraryDependency>();
            packageSpec.ProjectUrl = rawPackageSpec.GetValue<string>("projectUrl");
            packageSpec.IconUrl = rawPackageSpec.GetValue<string>("iconUrl");
            packageSpec.LicenseUrl = rawPackageSpec.GetValue<string>("licenseUrl");
            packageSpec.Copyright = rawPackageSpec.GetValue<string>("copyright");
            packageSpec.Language = rawPackageSpec.GetValue<string>("language");
            packageSpec.Summary = rawPackageSpec.GetValue<string>("summary");
            packageSpec.ReleaseNotes = rawPackageSpec.GetValue<string>("releaseNotes");

            var requireLicenseAcceptance = rawPackageSpec["requireLicenseAcceptance"];

            if (requireLicenseAcceptance != null)
            {
                try
                {
                    packageSpec.RequireLicenseAcceptance = rawPackageSpec.GetValue<bool?>("requireLicenseAcceptance") ?? false;
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, requireLicenseAcceptance, packageSpecPath);
                }
            }

            packageSpec.Tags = tags == null ? new string[] { } : tags.ValueAsArray<string>();

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

            packageSpec.Tools = ReadTools(packageSpec, rawPackageSpec).ToList();

            packageSpec.PackOptions = GetPackOptions(packageSpec, rawPackageSpec);

            // Read the runtime graph
            packageSpec.RuntimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(rawPackageSpec);

            return packageSpec;
        }

        private static NuGetVersion SpecifySnapshot(string version, string snapshotValue)
        {
            if (version.EndsWith("-*"))
            {
                if (string.IsNullOrEmpty(snapshotValue))
                {
                    version = version.Substring(0, version.Length - 2);
                }
                else
                {
                    version = version.Substring(0, version.Length - 1) + snapshotValue;
                }
            }

            return new NuGetVersion(version);
        }

        private static PackOptions GetPackOptions(PackageSpec packageSpec, JObject rawPackageSpec)
        {
            var rawPackOptions = rawPackageSpec.Value<JToken>(PackOptions) as JObject;
            if (rawPackOptions == null)
            {
                return new PackOptions
                {
                    PackageType = new PackageType[0]
                };
            }

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

            return new PackOptions
            {
                PackageType = packageTypeNames
                    .Select(name => new PackageType(name, Packaging.Core.PackageType.EmptyVersion))
                    .ToList()
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

                    // This method handles both the dependencies and framework assembly sections.
                    // Framework references should be limited to references.
                    // Dependencies should allow everything but framework references.
                    var targetFlagsValue = isGacOrFrameworkReference
                                                    ? LibraryDependencyTarget.Reference
                                                    : LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference;

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
                            else if(dependencyTypeValue.Contains(LibraryDependencyTypeFlag.SharedFramework))
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
                        SuppressParent = suppressParentFlagsValue
                    });
                }
            }
        }

        private static IEnumerable<ToolDependency> ReadTools(PackageSpec packageSpec, JObject rawPackageSpec)
        {
            var tools = rawPackageSpec["tools"] as JObject;
            if (tools != null)
            {
                foreach (var tool in tools)
                {
                    if (string.IsNullOrEmpty(tool.Key))
                    {
                        throw FileFormatException.Create(
                            Strings.MissingToolName,
                            tool.Value,
                            packageSpec.FilePath);
                    }

                    var value = tool.Value;
                    JToken versionToken = null;
                    string versionValue = null;
                    var imports = new List<NuGetFramework>();
                    if (value.Type == JTokenType.String)
                    {
                        versionToken = value;
                        versionValue = value.Value<string>();
                    }
                    else
                    {
                        if (value.Type == JTokenType.Object)
                        {
                            versionToken = value["version"];
                            if (versionToken != null && versionToken.Type == JTokenType.String)
                            {
                                versionValue = versionToken.Value<string>();
                            }
                            
                            imports.AddRange(GetImports((JObject) value, packageSpec));
                        }
                    }

                    if (versionValue == null)
                    {
                        throw FileFormatException.Create(
                            Strings.MissingVersionOnTool,
                            tool.Value,
                            packageSpec.FilePath);
                    }

                    VersionRange versionRange;
                    try
                    {
                        versionRange = VersionRange.Parse(versionValue);
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(
                            ex,
                            versionToken,
                            packageSpec.FilePath);
                    }

                    yield return new ToolDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = tool.Key,
                            TypeConstraint = LibraryDependencyTarget.Package,
                            VersionRange = versionRange
                        },
                        Imports = imports
                    };
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

        private static bool TryGetStringEnumerableFromJArray(JToken token, out IEnumerable<string> result)
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
            else if(token.Type == JTokenType.Array)
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
                        BuildTargetFrameworkNode(packageSpec, framework);
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, framework.Value, packageSpec.FilePath);
                    }
                }
            }
        }

        private static bool BuildTargetFrameworkNode(PackageSpec packageSpec, KeyValuePair<string, JToken> targetFramework)
        {
            var frameworkName = GetFramework(targetFramework.Key);

            var properties = targetFramework.Value.Value<JObject>();

            var importFrameworks = GetImports(properties, packageSpec);

            // If a fallback framework exists, update the framework to contain both.
            var updatedFramework = frameworkName;

            if (importFrameworks.Count != 0)
            {
                updatedFramework = new FallbackFramework(frameworkName, importFrameworks);
            }

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = updatedFramework,
                Dependencies = new List<LibraryDependency>(),
                Imports = importFrameworks,
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

        private static List<NuGetFramework> GetImports(JObject properties, PackageSpec packageSpec)
        {
            List<NuGetFramework> frameworks = new List<NuGetFramework>();

            var importsProperty = properties["imports"];

            if (importsProperty != null)
            {
                IEnumerable<string> importArray = new List<string>();
                if (TryGetStringEnumerableFromJArray(importsProperty, out importArray))
                {
                    frameworks = importArray.Where(p => !string.IsNullOrEmpty(p)).Select(p => NuGetFramework.Parse(p)).ToList();
                }
            }

            if (frameworks.Any(p => !p.IsSpecificFramework))
            {
                throw FileFormatException.Create(
                           string.Format(
                               Strings.Log_InvalidImportFramework,
                               importsProperty.ToString().Replace(Environment.NewLine, string.Empty),
                               PackageSpec.PackageSpecFileName),
                           importsProperty,
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
    }
}
