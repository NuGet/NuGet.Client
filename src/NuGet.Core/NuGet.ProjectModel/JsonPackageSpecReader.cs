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
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class JsonPackageSpecReader
    {
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

            packageSpec.Name = name;
            packageSpec.FilePath = Path.GetFullPath(packageSpecPath);

            if (version == null)
            {
                packageSpec.Version = new NuGetVersion("1.0.0");
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

            packageSpec.Description = rawPackageSpec.GetValue<string>("description");
            packageSpec.Authors = authors == null ? new string[] { } : authors.ValueAsArray<string>();
            packageSpec.Owners = owners == null ? new string[] { } : owners.ValueAsArray<string>();
            packageSpec.Dependencies = new List<LibraryDependency>();
            packageSpec.ProjectUrl = rawPackageSpec.GetValue<string>("projectUrl");
            packageSpec.IconUrl = rawPackageSpec.GetValue<string>("iconUrl");
            packageSpec.LicenseUrl = rawPackageSpec.GetValue<string>("licenseUrl");
            packageSpec.Copyright = rawPackageSpec.GetValue<string>("copyright");
            packageSpec.Language = rawPackageSpec.GetValue<string>("language");

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
                                                    : ~LibraryDependencyTarget.Reference;

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
                                Strings.MissingVersionProperty,
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

            // If it's not unsupported then keep it
            if (frameworkName == NuGetFramework.UnsupportedFramework)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            var properties = targetFramework.Value.Value<JObject>();

            var importFramework = GetImports(properties, packageSpec);

            // If a fallback framework exists, update the framework to contain both.
            var updatedFramework = frameworkName;

            if (importFramework.Count != 0)
            {
                updatedFramework = new FallbackFramework(frameworkName, importFramework);
            }

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = updatedFramework,
                Dependencies = new List<LibraryDependency>(),
                Imports = GetImports(properties, packageSpec),
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
            List<NuGetFramework> framework = new List<NuGetFramework>();

            var importsProperty = properties["imports"];

            if (importsProperty != null)
            {
                IEnumerable<string> importArray = new List<string>();
                if (TryGetStringEnumerableFromJArray(importsProperty, out importArray))
                {
                    framework = importArray.Where(p => !string.IsNullOrEmpty(p)).Select(p => NuGetFramework.Parse(p)).ToList();
                }
            }

            if (framework.Any(p => !p.IsSpecificFramework))
            {
                throw FileFormatException.Create(
                           string.Format(Strings.Log_InvalidImportFramework, importsProperty.ToString().Replace(Environment.NewLine,string.Empty),
                                            PackageSpec.PackageSpecFileName),
                           importsProperty,
                           packageSpec.FilePath);
            }

            return framework;
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

        private static string GetDirectoryName(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar);
        }
    }
}
