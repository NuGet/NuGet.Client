using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    /// <summary>
    /// Helpers for dealing with dg files and processing msbuild related inputs.
    /// </summary>
    public static class MSBuildRestoreUtility
    {
        /// <summary>
        /// Adds a dependency to a package spec. This handles scenarios
        /// where the reference already exists.
        /// </summary>
        public static void AddMSBuildProjectReference(
            PackageSpec spec,
            ProjectRestoreReference restoreReference,
            LibraryDependency libraryDependency)
        {
            AddMSBuildProjectReference(
                spec,
                restoreReference,
                libraryDependency,
                Enumerable.Empty<NuGetFramework>());
        }

        /// <summary>
        /// Adds a dependency to a package spec. This handles scenarios
        /// where the reference already exists.
        /// </summary>
        public static void AddMSBuildProjectReference(
            PackageSpec spec,
            ProjectRestoreReference restoreReference,
            LibraryDependency libraryDependency,
            IEnumerable<NuGetFramework> frameworks)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (restoreReference == null)
            {
                throw new ArgumentNullException(nameof(restoreReference));
            }

            if (libraryDependency == null)
            {
                throw new ArgumentNullException(nameof(libraryDependency));
            }

            if (frameworks == null)
            {
                throw new ArgumentNullException(nameof(frameworks));
            }

            // Add to dependencies
            if (frameworks.Count() == 0)
            {
                AddDependencyIfNotExist(spec, libraryDependency);
            }
            else
            {
                foreach (var framework in frameworks)
                {
                    AddDependencyIfNotExist(spec, framework, libraryDependency);
                }
            }

            // Add to restore section
            spec.RestoreMetadata.ProjectReferences.Add(restoreReference);
        }

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
            foreach (var spec in itemsById.Values.Select(GetPackageSpec))
            {
                if (spec.RestoreMetadata.OutputType == RestoreOutputType.NETCore
                    || spec.RestoreMetadata.OutputType == RestoreOutputType.UAP
                    || spec.RestoreMetadata.OutputType == RestoreOutputType.DotnetCliTool
                    || spec.RestoreMetadata.OutputType == RestoreOutputType.Standalone)
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
        /// Convert MSBuild items to a PackageSpec.
        /// </summary>
        public static PackageSpec GetPackageSpec(IEnumerable<IMSBuildItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            PackageSpec result = null;

            var specItem = items.SingleOrDefault(item =>
                "projectSpec".Equals(item.GetProperty("Type"),
                StringComparison.OrdinalIgnoreCase));

            if (specItem != null)
            {
                var typeString = specItem.GetProperty("OutputType");
                var restoreType = RestoreOutputType.Unknown;

                if (!string.IsNullOrEmpty(typeString))
                {
                    Enum.TryParse<RestoreOutputType>(typeString, ignoreCase: true, result: out restoreType);
                }

                // Get base spec
                if (restoreType == RestoreOutputType.UAP)
                {
                    result = GetUAPSpec(specItem);
                }
                else
                {
                    // Read msbuild data for both non-nuget and .NET Core
                    result = GetBaseSpec(specItem);
                }

                // Applies to all types
                result.RestoreMetadata.OutputType = restoreType;
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
                if (restoreType == RestoreOutputType.NETCore
                    || restoreType == RestoreOutputType.Standalone
                    || restoreType == RestoreOutputType.DotnetCliTool)
                {
                    AddFrameworkAssemblies(result, items);
                    AddPackageReferences(result, items);
                    result.RestoreMetadata.OutputPath = specItem.GetProperty("OutputPath");

                    foreach (var source in Split(specItem.GetProperty("Sources")))
                    {
                        result.RestoreMetadata.Sources.Add(new PackageSource(source));
                    }

                    foreach (var folder in Split(specItem.GetProperty("FallbackFolders")))
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
            }

            return result;
        }

        private static void AddProjectReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            var flatReferences = new HashSet<ProjectRestoreReference>();

            foreach (var item in GetItemByType(items, "ProjectReference"))
            {
                var dependency = new LibraryDependency();
                var projectReferenceUniqueName = item.GetProperty("ProjectReferenceUniqueName");
                var projectPath = item.GetProperty("ProjectPath");

                dependency.LibraryRange = new LibraryRange(
                    name: projectReferenceUniqueName,
                    versionRange: VersionRange.All,
                    typeConstraint: LibraryDependencyTarget.ExternalProject);

                ApplyIncludeFlags(dependency, item);

                var msbuildDependency = new ProjectRestoreReference()
                {
                    ProjectPath = projectPath,
                    ProjectUniqueName = projectReferenceUniqueName,
                };

                AddMSBuildProjectReference(spec, msbuildDependency, dependency, GetFrameworks(item, spec));
            }

            // Add project paths
            foreach (var msbuildDependency in flatReferences)
            {
                spec.RestoreMetadata.ProjectReferences.Add(msbuildDependency);
            }
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
            var includeFlags = GetIncludeFlags(item.GetProperty("IncludeAssets"), LibraryIncludeFlags.All);
            var excludeFlags = GetIncludeFlags(item.GetProperty("ExcludeAssets"), LibraryIncludeFlags.None);

            dependency.IncludeType = includeFlags & ~excludeFlags;
            dependency.SuppressParent = GetIncludeFlags(item.GetProperty("PrivateAssets"), LibraryIncludeFlagUtils.DefaultSuppressParent);
        }

        private static LibraryIncludeFlags GetIncludeFlags(string value, LibraryIncludeFlags defaultValue)
        {
            var parts = Split(value);

            if (parts.Length > 0)
            {
                return LibraryIncludeFlagUtils.GetFlags(parts);
            }
            else
            {
                return defaultValue;
            }
        }

        private static string[] Split(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                return s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }

            return new string[0];
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
                frameworks.UnionWith(frameworksString.Split(';'));
            }

            return frameworks;
        }

        private static IEnumerable<IMSBuildItem> GetItemByType(IEnumerable<IMSBuildItem> items, string type)
        {
            return items.Where(e => type.Equals(e.GetProperty("Type"), StringComparison.OrdinalIgnoreCase));
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
                var path = Path.Combine(
                    NuGetEnvironment.GetFolderPath(NuGetFolderPath.Temp),
                    "nuget-dg",
                    $"{Guid.NewGuid()}.dg");

                var envPath = Environment.GetEnvironmentVariable("NUGET_PERSIST_DG_PATH");

                if (!string.IsNullOrEmpty(envPath))
                {
                    path = envPath;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path));

                log.LogMinimal(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.PersistDGFile,
                        path));

                spec.Save(path);
            }
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