// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    /// <summary>
    /// Helpers for dealing with dg files and processing msbuild related inputs.
    /// </summary>
    public static class MSBuildRestoreUtility
    {
        /// <summary>
        /// Convert MSBuild items to a DependencyGraphSpec.
        /// </summary>
        public static DependencyGraphSpec GetDependencySpec(IEnumerable<IMSBuildItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var graphSpec = new DependencyGraphSpec();
            var itemsById = new Dictionary<string, List<IMSBuildItem>>(StringComparer.Ordinal);
            var restoreSpecs = new HashSet<string>(StringComparer.Ordinal);
            var validForRestore = new HashSet<string>(StringComparer.Ordinal);
            var toolItems = new List<IMSBuildItem>();

            // Sort items and add restore specs
            foreach (var item in items)
            {
                var type = item.GetProperty("Type")?.ToLowerInvariant();
                var projectUniqueName = item.GetProperty("ProjectUniqueName");

                if ("restorespec".Equals(type, StringComparison.Ordinal))
                {
                    restoreSpecs.Add(projectUniqueName);
                }
                else if (!string.IsNullOrEmpty(projectUniqueName))
                {
                    List<IMSBuildItem> idItems;
                    if (!itemsById.TryGetValue(projectUniqueName, out idItems))
                    {
                        idItems = new List<IMSBuildItem>(1);
                        itemsById.Add(projectUniqueName, idItems);
                    }

                    idItems.Add(item);
                }
            }

            // Add projects
            var validProjectSpecs = itemsById.Values.Select(GetPackageSpec).Where(e => e != null);

            foreach (var spec in validProjectSpecs)
            {
                if (spec.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference
                    || spec.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson
                    || spec.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool
                    || spec.RestoreMetadata.ProjectStyle == ProjectStyle.Standalone)
                {
                    validForRestore.Add(spec.RestoreMetadata.ProjectUniqueName);
                }

                graphSpec.AddProject(spec);
            }

            // Add valid projects to restore section
            foreach (var projectUniqueName in restoreSpecs.Intersect(validForRestore))
            {
                graphSpec.AddRestore(projectUniqueName);
            }

            return graphSpec;
        }

        /// <summary>
        /// Insert asset flags into dependency, based on ;-delimited string args
        /// </summary>
        public static void ApplyIncludeFlags(LibraryDependency dependency, string includeAssets, string excludeAssets, string privateAssets)
        {
            var includeFlags = GetIncludeFlags(includeAssets, LibraryIncludeFlags.All);
            var excludeFlags = GetIncludeFlags(excludeAssets, LibraryIncludeFlags.None);

            dependency.IncludeType = includeFlags & ~excludeFlags;
            dependency.SuppressParent = GetIncludeFlags(privateAssets, LibraryIncludeFlagUtils.DefaultSuppressParent);
        }

        /// <summary>
        /// Insert asset flags into project dependency, based on ;-delimited string args
        /// </summary>
        public static void ApplyIncludeFlags(ProjectRestoreReference dependency, string includeAssets, string excludeAssets, string privateAssets)
        {
            dependency.IncludeAssets = GetIncludeFlags(includeAssets, LibraryIncludeFlags.All);
            dependency.ExcludeAssets = GetIncludeFlags(excludeAssets, LibraryIncludeFlags.None);
            dependency.PrivateAssets = GetIncludeFlags(privateAssets, LibraryIncludeFlagUtils.DefaultSuppressParent);
        }

        /// <summary>
        /// Convert MSBuild items to a PackageSpec.
        /// </summary>
        public static PackageSpec GetPackageSpec(IEnumerable<IMSBuildItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            PackageSpec result = null;

            // There should only be one ProjectSpec per project in the item set, 
            // but if multiple do appear take only the first one in an effort
            // to handle this gracefully.
            var specItem = items.FirstOrDefault(item =>
                "projectSpec".Equals(item.GetProperty("Type"),
                StringComparison.OrdinalIgnoreCase));

            if (specItem != null)
            {
                var typeString = specItem.GetProperty("ProjectStyle");
                var restoreType = ProjectStyle.Unknown;

                if (!string.IsNullOrEmpty(typeString))
                {
                    Enum.TryParse(typeString, ignoreCase: true, result: out restoreType);
                }

                // Get base spec
                if (restoreType == ProjectStyle.ProjectJson)
                {
                    result = GetUAPSpec(specItem);
                }
                else
                {
                    // Read msbuild data for both non-nuget and .NET Core
                    result = GetBaseSpec(specItem);
                }

                // Applies to all types
                result.RestoreMetadata.ProjectStyle = restoreType;
                result.RestoreMetadata.ProjectPath = specItem.GetProperty("ProjectPath");
                result.RestoreMetadata.ProjectUniqueName = specItem.GetProperty("ProjectUniqueName");

                if (string.IsNullOrEmpty(result.Name))
                {
                    result.Name = result.RestoreMetadata.ProjectName
                        ?? result.RestoreMetadata.ProjectUniqueName
                        ?? Path.GetFileNameWithoutExtension(result.FilePath);
                }

                // Read project references for all
                AddProjectReferences(result, items);

                // Read package references for netcore, tools, and standalone
                if (restoreType == ProjectStyle.PackageReference
                    || restoreType == ProjectStyle.Standalone
                    || restoreType == ProjectStyle.DotnetCliTool)
                {
                    AddFrameworkAssemblies(result, items);
                    AddPackageReferences(result, items);
                    result.RestoreMetadata.OutputPath = specItem.GetProperty("OutputPath");

                    foreach (var source in MSBuildStringUtility.Split(specItem.GetProperty("Sources")))
                    {
                        result.RestoreMetadata.Sources.Add(new PackageSource(source));
                    }

                    foreach (var folder in MSBuildStringUtility.Split(specItem.GetProperty("FallbackFolders")))
                    {
                        result.RestoreMetadata.FallbackFolders.Add(folder);
                    }

                    result.RestoreMetadata.PackagesPath = specItem.GetProperty("PackagesPath");

                    // Store the original framework strings for msbuild conditionals
                    foreach (var originalFramework in GetFrameworksStrings(specItem))
                    {
                        result.RestoreMetadata.OriginalTargetFrameworks.Add(originalFramework);
                    }
                }

                if (restoreType == ProjectStyle.PackageReference
                    || restoreType == ProjectStyle.Standalone)
                {
                    // Set project version
                    result.Version = GetVersion(specItem);

                    // Add RIDs and Supports
                    result.RuntimeGraph = GetRuntimeGraph(specItem);

                    // Add PackageTargetFallback
                    AddPackageTargetFallbacks(result, items);

                    // Add CrossTargeting flag
                    result.RestoreMetadata.CrossTargeting = IsPropertyTrue(specItem, "CrossTargeting");

                    // Add RestoreLegacyPackagesDirectory flag
                    result.RestoreMetadata.LegacyPackagesDirectory = IsPropertyTrue(
                        specItem,
                        "RestoreLegacyPackagesDirectory");

                    // ValidateRuntimeAssets compat check
                    result.RestoreMetadata.ValidateRuntimeAssets = IsPropertyTrue(specItem, "ValidateRuntimeAssets");

                    // True for .NETCore projects.
                    result.RestoreMetadata.SkipContentFileWrite = IsPropertyTrue(specItem, "SkipContentFileWrite");
                }

                if (restoreType == ProjectStyle.ProjectJson)
                {
                    // Check runtime assets by default for project.json
                    result.RestoreMetadata.ValidateRuntimeAssets = true;
                }

                // File assets
                if (restoreType == ProjectStyle.PackageReference
                    || restoreType == ProjectStyle.ProjectJson
                    || restoreType == ProjectStyle.Unknown
                    || restoreType == ProjectStyle.PackagesConfig)
                {
                    var projectDir = string.Empty;

                    if (result.RestoreMetadata.ProjectPath != null)
                    {
                        projectDir = Path.GetDirectoryName(result.RestoreMetadata.ProjectPath);
                    }

                    AddPlaceHolderFiles(result, projectDir);
                }
            }

            return result;
        }

        /// <summary>
        /// Add placeholder files, these will be used under the compile and runtime sections.
        /// </summary>
        private static void AddPlaceHolderFiles(PackageSpec spec,  string projectDir)
        {
            var files = new List<ProjectRestoreMetadataFile>();

            // Create fake placeholder file names
            var assemblyName = $"{spec.RestoreMetadata.ProjectName}.dll";
            var absoluteRoot = Path.Combine(projectDir, "bin", "placeholder");

            if (spec.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference
                || spec.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson)
            {
                // Add paths based on actual frameworks if they exist
                foreach (var framework in spec.TargetFrameworks
                    .Select(tfi => tfi.FrameworkName)
                    .Where(f => f.IsSpecificFramework))
                {
                    var packagePath = $"lib/{framework.GetShortFolderName()}/{assemblyName}";
                    var absolutePath = Path.Combine(absoluteRoot, framework.GetShortFolderName(), assemblyName);

                    files.Add(new ProjectRestoreMetadataFile(packagePath, absolutePath));
                }
            }

            // If no TFMs exist use LIBANY which to guarantee something is added
            if (files.Count == 0)
            {
                var absolutePath = Path.Combine(absoluteRoot, "any", assemblyName);
                files.Add(new ProjectRestoreMetadataFile(LockFileUtils.LIBANY, absolutePath));
            }

            spec.RestoreMetadata.Files = files;
        }

        private static void AddPackageTargetFallbacks(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            foreach (var item in GetItemByType(items, "TargetFrameworkInformation"))
            {
                var frameworkString = item.GetProperty("TargetFramework");
                TargetFrameworkInformation targetFrameworkInfo = null;

                if (!string.IsNullOrEmpty(frameworkString))
                {
                    targetFrameworkInfo = spec.GetTargetFramework(NuGetFramework.Parse(frameworkString));
                }

                Debug.Assert(targetFrameworkInfo != null, $"PackageSpec does not contain the target framework: {frameworkString}");

                if (targetFrameworkInfo != null)
                {
                    var fallbackList = MSBuildStringUtility.Split(item.GetProperty("PackageTargetFallback"))
                        .Select(NuGetFramework.Parse)
                        .ToList();

                    targetFrameworkInfo.Imports = fallbackList;

                    // Update the PackageSpec framework to include fallback frameworks
                    if (targetFrameworkInfo.Imports.Count > 0)
                    {
                        targetFrameworkInfo.FrameworkName =
                            new FallbackFramework(targetFrameworkInfo.FrameworkName, fallbackList);
                    }
                }
            }
        }

        private static RuntimeGraph GetRuntimeGraph(IMSBuildItem specItem)
        {
            var runtimes = MSBuildStringUtility.Split(specItem.GetProperty("RuntimeIdentifiers"))
                .Distinct(StringComparer.Ordinal)
                .Select(rid => new RuntimeDescription(rid))
                .ToList();

            var supports = MSBuildStringUtility.Split(specItem.GetProperty("RuntimeSupports"))
                .Distinct(StringComparer.Ordinal)
                .Select(s => new CompatibilityProfile(s))
                .ToList();

            return new RuntimeGraph(runtimes, supports);
        }

        private static void AddProjectReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            // Add groups for each spec framework
            var frameworkGroups = new Dictionary<NuGetFramework, List<ProjectRestoreReference>>();
            foreach (var framework in spec.TargetFrameworks.Select(e => e.FrameworkName).Distinct())
            {
                frameworkGroups.Add(framework, new List<ProjectRestoreReference>());
            }

            var flatReferences = GetItemByType(items, "ProjectReference")
                .Select(GetProjectRestoreReference);

            // Add project paths
            foreach (var frameworkPair in flatReferences)
            {
                // If no frameworks were given, apply to all
                var addToFrameworks = frameworkPair.Item1.Count == 0
                    ? frameworkGroups.Keys.ToList()
                    : frameworkPair.Item1;

                foreach (var framework in addToFrameworks)
                {
                    List<ProjectRestoreReference> references;
                    if (frameworkGroups.TryGetValue(framework, out references))
                    {
                        // Ensure unique
                        if (!references
                            .Any(e => e.ProjectUniqueName
                            .Equals(frameworkPair.Item2.ProjectUniqueName, StringComparison.OrdinalIgnoreCase)))
                        {
                            references.Add(frameworkPair.Item2);
                        }
                    }
                }
            }

            // Add groups to spec
            foreach (var frameworkPair in frameworkGroups)
            {
                spec.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(frameworkPair.Key)
                {
                    ProjectReferences = frameworkPair.Value
                });
            }
        }

        private static Tuple<List<NuGetFramework>, ProjectRestoreReference> GetProjectRestoreReference(IMSBuildItem item)
        {
            var frameworks = GetFrameworks(item).ToList();

            var reference = new ProjectRestoreReference()
            {
                ProjectPath = item.GetProperty("ProjectPath"),
                ProjectUniqueName = item.GetProperty("ProjectReferenceUniqueName"),
            };

            ApplyIncludeFlags(reference, item.GetProperty("IncludeAssets"), item.GetProperty("ExcludeAssets"), item.GetProperty("PrivateAssets"));

            return new Tuple<List<NuGetFramework>, ProjectRestoreReference>(frameworks, reference);
        }

        private static bool AddDependencyIfNotExist(PackageSpec spec, LibraryDependency dependency)
        {
            if (!spec.Dependencies
                   .Select(d => d.Name)
                   .Contains(dependency.Name, StringComparer.OrdinalIgnoreCase))
            {
                spec.Dependencies.Add(dependency);

                return true;
            }

            return false;
        }

        private static bool AddDependencyIfNotExist(PackageSpec spec, NuGetFramework framework, LibraryDependency dependency)
        {
            var frameworkInfo = spec.GetTargetFramework(framework);

            if (!spec.Dependencies
                            .Concat(frameworkInfo.Dependencies)
                            .Select(d => d.Name)
                            .Contains(dependency.Name, StringComparer.OrdinalIgnoreCase))
            {
                frameworkInfo.Dependencies.Add(dependency);

                return true;
            }

            return false;
        }

        private static void AddPackageReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            foreach (var item in GetItemByType(items, "Dependency"))
            {
                var dependency = new LibraryDependency();

                dependency.LibraryRange = new LibraryRange(
                    name: item.GetProperty("Id"),
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Package);

                ApplyIncludeFlags(dependency, item);

                var frameworks = GetFrameworks(item);

                if (frameworks.Count == 0)
                {
                    AddDependencyIfNotExist(spec, dependency);
                }
                else
                {
                    foreach (var framework in frameworks)
                    {
                        AddDependencyIfNotExist(spec, framework, dependency);
                    }
                }
            }
        }

        private static void ApplyIncludeFlags(LibraryDependency dependency, IMSBuildItem item)
        {
            ApplyIncludeFlags(dependency, item.GetProperty("IncludeAssets"), item.GetProperty("ExcludeAssets"), item.GetProperty("PrivateAssets"));
        }

        private static LibraryIncludeFlags GetIncludeFlags(string value, LibraryIncludeFlags defaultValue)
        {
            var parts = MSBuildStringUtility.Split(value);

            if (parts.Length > 0)
            {
                return LibraryIncludeFlagUtils.GetFlags(parts);
            }
            else
            {
                return defaultValue;
            }
        }

        private static void AddFrameworkAssemblies(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            foreach (var item in GetItemByType(items, "FrameworkAssembly"))
            {
                var dependency = new LibraryDependency();

                dependency.LibraryRange = new LibraryRange(
                    name: item.GetProperty("Id"),
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Reference);

                ApplyIncludeFlags(dependency, item);

                var frameworks = GetFrameworks(item);

                if (frameworks.Count == 0)
                {
                    AddDependencyIfNotExist(spec, dependency);
                }
                else
                {
                    foreach (var framework in frameworks)
                    {
                        AddDependencyIfNotExist(spec, framework, dependency);
                    }
                }
            }
        }

        private static VersionRange GetVersionRange(IMSBuildItem item)
        {
            var rangeString = item.GetProperty("VersionRange");

            if (!string.IsNullOrEmpty(rangeString))
            {
                return VersionRange.Parse(rangeString);
            }

            return VersionRange.All;
        }

        private static PackageSpec GetUAPSpec(IMSBuildItem specItem)
        {
            PackageSpec result;
            var projectPath = specItem.GetProperty("ProjectPath");
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectJsonPath = specItem.GetProperty("ProjectJsonPath");

            // Read project.json
            result = JsonPackageSpecReader.GetPackageSpec(projectName, projectJsonPath);

            result.RestoreMetadata = new ProjectRestoreMetadata();
            result.RestoreMetadata.ProjectJsonPath = projectJsonPath;
            result.RestoreMetadata.ProjectName = projectName;
            return result;
        }

        private static PackageSpec GetBaseSpec(IMSBuildItem specItem)
        {
            var frameworkInfo = GetFrameworks(specItem)
                .Select(framework => new TargetFrameworkInformation()
                {
                    FrameworkName = framework
                })
                .ToList();

            var spec = new PackageSpec(frameworkInfo);
            spec.RestoreMetadata = new ProjectRestoreMetadata();
            spec.FilePath = specItem.GetProperty("ProjectPath");
            spec.RestoreMetadata.ProjectName = specItem.GetProperty("ProjectName");

            return spec;
        }

        private static HashSet<NuGetFramework> GetFrameworks(IMSBuildItem item, PackageSpec spec)
        {
            var frameworks = GetFrameworks(item);

            if (frameworks.Count == 0)
            {
                frameworks.UnionWith(spec.TargetFrameworks.Select(e => e.FrameworkName));
            }

            return frameworks;
        }

        private static HashSet<NuGetFramework> GetFrameworks(IMSBuildItem item)
        {
            return new HashSet<NuGetFramework>(
                GetFrameworksStrings(item).Select(NuGetFramework.Parse));
        }

        private static HashSet<string> GetFrameworksStrings(IMSBuildItem item)
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var frameworksString = item.GetProperty("TargetFrameworks");
            if (!string.IsNullOrEmpty(frameworksString))
            {
                frameworks.UnionWith(MSBuildStringUtility.Split(frameworksString));
            }

            return frameworks;
        }

        private static IEnumerable<IMSBuildItem> GetItemByType(IEnumerable<IMSBuildItem> items, string type)
        {
            return items.Where(e => type.Equals(e.GetProperty("Type"), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Return the parsed version or 1.0.0 if the property does not exist.
        /// </summary>
        private static NuGetVersion GetVersion(IMSBuildItem item)
        {
            var versionString = item.GetProperty("Version");
            NuGetVersion version = null;

            if (string.IsNullOrEmpty(versionString))
            {
                // Default to 1.0.0 if the property does not exist
                version = new NuGetVersion(1, 0, 0);
            }
            else
            {
                // Snapshot versions are not allowed in .NETCore
                version = NuGetVersion.Parse(versionString);
            }

            return version;
        }

        public static void Dump(IEnumerable<IMSBuildItem> items, ILogger log)
        {
            foreach (var item in items)
            {
                log.LogDebug($"Item: {item.Identity}");

                foreach (var key in item.Properties)
                {
                    var val = item.GetProperty(key);

                    if (!string.IsNullOrEmpty(val))
                    {
                        log.LogDebug($"  {key}={val}");
                    }
                }
            }
        }

        /// <summary>
        /// Write the dg file to a temp location if NUGET_PERSIST_DG.
        /// </summary>
        /// <remarks>This is a noop if NUGET_PERSIST_DG is not set to true.</remarks>
        public static void PersistDGFileIfDebugging(DependencyGraphSpec spec, ILogger log)
        {
            if (_isPersistDGSet.Value)
            {
                string path;
                var envPath = Environment.GetEnvironmentVariable("NUGET_PERSIST_DG_PATH");
                if (!string.IsNullOrEmpty(envPath))
                {
                    path = envPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                }
                else
                {
                    path = Path.Combine(
                        NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp),
                        "nuget-dg",
                        $"{Guid.NewGuid()}.dg");
                    DirectoryUtility.CreateSharedDirectory(Path.GetDirectoryName(path));
                }

                log.LogMinimal(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PersistDGFile,
                        path));

                spec.Save(path);
            }
        }

        private static bool IsPropertyTrue(IMSBuildItem item, string propertyName)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(item.GetProperty(propertyName), Boolean.TrueString);
        }

        private static readonly Lazy<bool> _isPersistDGSet = new Lazy<bool>(() => IsPersistDGSet());

        /// <summary>
        /// True if NUGET_PERSIST_DG is set to true.
        /// </summary>
        private static bool IsPersistDGSet()
        {
            var settingValue = Environment.GetEnvironmentVariable("NUGET_PERSIST_DG");

            bool val;
            if (!string.IsNullOrEmpty(settingValue)
                && Boolean.TryParse(settingValue, out val))
            {
                return val;
            }

            return false;
        }
    }
}