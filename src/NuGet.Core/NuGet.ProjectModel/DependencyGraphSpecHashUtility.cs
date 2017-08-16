using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class DependencyGraphSpecHashUtility
    {
        public static string GetSmartHash(DependencyGraphSpec dependencyGraphSpec)
        {
            using (var hashFunc = new Sha512HashFunction())
            using (var writer = new HashObjectWriter(hashFunc))
            {
                AddRelevantObjects(dependencyGraphSpec, writer);

                return writer.GetHash();
            }
        }

        private static void AddRelevantObjects(DependencyGraphSpec dependencyGraphSpec, RuntimeModel.IObjectWriter writer)
        {
            writer.WriteNameValue("format", 1);

            // Preserve default sort order
            foreach (var restoreName in dependencyGraphSpec._restore)
            {
                writer.WriteObjectStart(restoreName);
                writer.WriteObjectEnd();
            }

            // Preserve default sort order
            foreach (var pair in dependencyGraphSpec._projects)
            {
                var project = pair.Value;

                writer.WriteObjectStart(project.RestoreMetadata.ProjectUniqueName);
                GetHashRelevantObjects(project, writer);
                writer.WriteObjectEnd();
            }
        }


        public static void GetHashRelevantObjects(PackageSpec packageSpec, RuntimeModel.IObjectWriter writer)
        {
            if (packageSpec == null)
            {
                throw new ArgumentNullException(nameof(packageSpec));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            SetValue(writer, "title", packageSpec.Title);

            if (!packageSpec.IsDefaultVersion)
            {
                SetValue(writer, "version", packageSpec.Version?.ToFullString());
            }

            SetArrayValue(writer, "contentFiles", packageSpec.ContentFiles.OrderBy(e => e));
            //TODO NK - Does this make sense? Do we really need the pack options and the pack include options?
            SetDictionaryValue(writer, "packInclude", packageSpec.PackInclude);
            // If the assets file is changed bsed on these pack options then we need to include them
//            SetPackOptions(writer, packageSpec);

            SetRestoreSettings(writer, packageSpec);
            SetMSBuildMetadata(writer, packageSpec);
            SetDictionaryValues(writer, "scripts", packageSpec.Scripts);

            if (packageSpec.Dependencies.Any())
            {
                SetDependencies(writer, packageSpec.Dependencies);
            }

            SetFrameworks(writer, packageSpec.TargetFrameworks);

            JsonRuntimeFormat.WriteRuntimeGraph(writer, packageSpec.RuntimeGraph);
        }

        private static void SetRestoreSettings(IObjectWriter writer, PackageSpec packageSpec)
        {
            var restoreSettings = packageSpec.RestoreSettings;

            // Do not write Restore Setting if the HideWarningsAndErrors is false
            // This should be changed in the future as more properties are added to ProjectRestoreSettings
            if (restoreSettings == null || !restoreSettings.HideWarningsAndErrors)
            {
                return;
            }
            writer.WriteObjectStart(JsonPackageSpecReader.RestoreSettings);

            SetValueIfTrue(writer, JsonPackageSpecReader.HideWarningsAndErrors, restoreSettings.HideWarningsAndErrors);

            writer.WriteObjectEnd();
        }

        private static void SetMSBuildMetadata(IObjectWriter writer, PackageSpec packageSpec)
        {
            var msbuildMetadata = packageSpec.RestoreMetadata;
            if (msbuildMetadata == null)
            {
                return;
            }

            if (msbuildMetadata.ProjectUniqueName == null && msbuildMetadata.ProjectName == null
                && msbuildMetadata.ProjectPath == null && msbuildMetadata.ProjectJsonPath == null
                && msbuildMetadata.PackagesPath == null && msbuildMetadata.OutputPath == null)
            {
                return;
            }

            writer.WriteObjectStart(JsonPackageSpecReader.RestoreOptions);

            SetValue(writer, "projectUniqueName", msbuildMetadata.ProjectUniqueName);
            SetValue(writer, "projectName", msbuildMetadata.ProjectName);
            SetValue(writer, "projectPath", msbuildMetadata.ProjectPath);
            SetValue(writer, "projectJsonPath", msbuildMetadata.ProjectJsonPath);
            SetValue(writer, "packagesPath", msbuildMetadata.PackagesPath);
            SetValue(writer, "outputPath", msbuildMetadata.OutputPath);

            if (msbuildMetadata.ProjectStyle != ProjectStyle.Unknown)
            {
                SetValue(writer, "projectStyle", msbuildMetadata.ProjectStyle.ToString());
            }

            SetValueIfTrue(writer, "crossTargeting", msbuildMetadata.CrossTargeting);

            SetValueIfTrue(
                    writer,
                    "legacyPackagesDirectory",
                    msbuildMetadata.LegacyPackagesDirectory);

            SetValueIfTrue(
                    writer,
                    "validateRuntimeAssets",
                    msbuildMetadata.ValidateRuntimeAssets);

            SetValueIfTrue(
                    writer,
                    "skipContentFileWrite",
                    msbuildMetadata.SkipContentFileWrite);

            SetArrayValue(writer, "fallbackFolders", msbuildMetadata.FallbackFolders);
            SetArrayValue(writer, "configFilePaths", msbuildMetadata.ConfigFilePaths);
            // TODO NK - Null check
            SetArrayValue(writer, "originalTargetFrameworks", msbuildMetadata.OriginalTargetFrameworks.Select(e => NuGetFramework.Parse(e).GetShortFolderName()));

            if (msbuildMetadata.Sources?.Count > 0)
            {
                writer.WriteObjectStart("sources");

                foreach (var source in msbuildMetadata.Sources.OrderBy(e => e.Source, StringComparer.Ordinal))
                {
                    // "source": {}
                    writer.WriteObjectStart(source.Source);
                    writer.WriteObjectEnd();
                }

                writer.WriteObjectEnd();
            }

            if (msbuildMetadata.Files?.Count > 0)
            {
                writer.WriteObjectStart("files");

                foreach (var file in msbuildMetadata.Files)
                {
                    SetValue(writer, file.PackagePath, file.AbsolutePath);
                }

                writer.WriteObjectEnd();
            }

            if (msbuildMetadata.TargetFrameworks?.Count > 0)
            {
                writer.WriteObjectStart("frameworks");

                var frameworkNames = new HashSet<string>();

                foreach (var framework in msbuildMetadata.TargetFrameworks)
                {
                    var frameworkName = framework.FrameworkName.GetShortFolderName();

                    if (!frameworkNames.Contains(frameworkName))
                    {
                        frameworkNames.Add(frameworkName);

                        writer.WriteObjectStart(frameworkName);

                        writer.WriteObjectStart("projectReferences");

                        foreach (var project in framework.ProjectReferences)
                        {
                            writer.WriteObjectStart(project.ProjectUniqueName);

                            writer.WriteNameValue("projectPath", project.ProjectPath);

                            if (project.IncludeAssets != LibraryIncludeFlags.All)
                            {
                                writer.WriteNameValue("includeAssets", LibraryIncludeFlagUtils.GetFlagString(project.IncludeAssets));
                            }

                            if (project.ExcludeAssets != LibraryIncludeFlags.None)
                            {
                                writer.WriteNameValue("excludeAssets", LibraryIncludeFlagUtils.GetFlagString(project.ExcludeAssets));
                            }

                            if (project.PrivateAssets != LibraryIncludeFlagUtils.DefaultSuppressParent)
                            {
                                writer.WriteNameValue("privateAssets", LibraryIncludeFlagUtils.GetFlagString(project.PrivateAssets));
                            }

                            writer.WriteObjectEnd();
                        }

                        writer.WriteObjectEnd();
                        writer.WriteObjectEnd();
                    }
                }

                writer.WriteObjectEnd();
            }

            SetWarningProperties(writer, msbuildMetadata);

            writer.WriteObjectEnd();
        }

        private static void SetWarningProperties(IObjectWriter writer, ProjectRestoreMetadata msbuildMetadata)
        {
            if (msbuildMetadata.ProjectWideWarningProperties != null &&
                (msbuildMetadata.ProjectWideWarningProperties.AllWarningsAsErrors ||
                 msbuildMetadata.ProjectWideWarningProperties.NoWarn.Count > 0 ||
                 msbuildMetadata.ProjectWideWarningProperties.WarningsAsErrors.Count > 0))
            {
                writer.WriteObjectStart("warningProperties");

                SetValueIfTrue(writer, "allWarningsAsErrors", msbuildMetadata.ProjectWideWarningProperties.AllWarningsAsErrors);

                if (msbuildMetadata.ProjectWideWarningProperties.NoWarn.Count > 0)
                {
                    SetArrayValue(writer, "noWarn", msbuildMetadata
                       .ProjectWideWarningProperties
                       .NoWarn
                       .ToArray()
                       .OrderBy(c => c)
                       .Select(c => c.GetName())
                       .Where(c => !string.IsNullOrEmpty(c)));
                }

                if (msbuildMetadata.ProjectWideWarningProperties.WarningsAsErrors.Count > 0)
                {
                    SetArrayValue(writer, "warnAsError", msbuildMetadata
                        .ProjectWideWarningProperties
                        .WarningsAsErrors
                        .ToArray()
                        .OrderBy(c => c)
                        .Select(c => c.GetName())
                        .Where(c => !string.IsNullOrEmpty(c)));

                }

                writer.WriteObjectEnd();
            }
        }

        private static void SetDependencies(IObjectWriter writer, IList<LibraryDependency> libraryDependencies)
        {
            SetDependencies(writer, "dependencies", libraryDependencies.Where(dependency => dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference));
            SetDependencies(writer, "frameworkAssemblies", libraryDependencies.Where(dependency => dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference));
        }

        private static void SetDependencies(IObjectWriter writer, string name, IEnumerable<LibraryDependency> libraryDependencies)
        {
            if (!libraryDependencies.Any())
            {
                return;
            }

            writer.WriteObjectStart(name);

            foreach (var dependency in libraryDependencies)
            {
                var expandedMode = dependency.IncludeType != LibraryIncludeFlags.All
                    || dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent
                    || dependency.Type != LibraryDependencyType.Default
                    || dependency.AutoReferenced
                    || (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference
                        && dependency.LibraryRange.TypeConstraint != (LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference));

                var versionRange = dependency.LibraryRange.VersionRange ?? VersionRange.All;
                var versionString = versionRange.OriginalString;

                if (string.IsNullOrEmpty(versionString))
                {
                    versionString = versionRange.ToNormalizedString();
                }

                if (expandedMode)
                {
                    writer.WriteObjectStart(dependency.Name);

                    if (dependency.IncludeType != LibraryIncludeFlags.All)
                    {
                        SetValue(writer, "include", dependency.IncludeType.ToString());
                    }

                    if (dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
                    {
                        SetValue(writer, "suppressParent", dependency.SuppressParent.ToString());
                    }

                    if (dependency.Type != LibraryDependencyType.Default)
                    {
                        SetValue(writer, "type", dependency.Type.ToString());
                    }

                    if (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference
                        && dependency.LibraryRange.TypeConstraint != (LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference))
                    {
                        SetValue(writer, "target", dependency.LibraryRange.TypeConstraint.ToString());
                    }

                    if (VersionRange.All.Equals(versionRange)
                        && !dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)
                        && !dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Reference)
                        && !dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.ExternalProject))
                    {
                        // Allow this specific case to skip the version property
                    }
                    else
                    {
                        SetValue(writer, "version", versionString);
                    }

                    SetValueIfTrue(writer, "autoReferenced", dependency.AutoReferenced);

                    if (dependency.NoWarn.Count > 0)
                    {
                        SetArrayValue(writer, "noWarn", dependency
                            .NoWarn
                            .OrderBy(c => c)
                            .Distinct()
                            .Select(code => code.GetName())
                            .Where(s => !string.IsNullOrEmpty(s)));
                    }

                    writer.WriteObjectEnd();
                }
                else
                {
                    writer.WriteNameValue(dependency.Name, versionString);
                }
            }

            writer.WriteObjectEnd();
        }

        private static void SetImports(IObjectWriter writer, IList<NuGetFramework> frameworks)
        {
            if (frameworks?.Any() == true)
            {
                var imports = frameworks.Select(framework => framework.GetShortFolderName());

                writer.WriteNameArray("imports", imports);
            }
        }

        private static void SetFrameworks(IObjectWriter writer, IList<TargetFrameworkInformation> frameworks)
        {
            if (frameworks.Any())
            {
                writer.WriteObjectStart("frameworks");

                foreach (var framework in frameworks)
                {
                    writer.WriteObjectStart(framework.FrameworkName.GetShortFolderName());

                    SetDependencies(writer, framework.Dependencies);
                    SetImports(writer, framework.Imports);
                    SetValueIfTrue(writer, "assetTargetFallback", framework.AssetTargetFallback);
                    SetValueIfTrue(writer, "warn", framework.Warn);

                    writer.WriteObjectEnd();
                }

                writer.WriteObjectEnd();
            }
        }

        private static void SetValueIfTrue(IObjectWriter writer, string name, bool value)
        {
            if (value)
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetValue(IObjectWriter writer, string name, string value)
        {
            if (value != null)
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetArrayValue(IObjectWriter writer, string name, IEnumerable<string> values)
        {
            if (values != null && values.Any())
            {
                writer.WriteNameArray(name, values);
            }
        }

        private static void SetDictionaryValue(IObjectWriter writer, string name, IDictionary<string, string> values)
        {
            if (values != null && values.Any())
            {
                writer.WriteObjectStart(name);

                var sortedValues = values.OrderBy(pair => pair.Key, StringComparer.Ordinal);

                foreach (var pair in sortedValues)
                {
                    writer.WriteNameValue(pair.Key, pair.Value);
                }

                writer.WriteObjectEnd();
            }
        }

        private static void SetDictionaryValues(IObjectWriter writer, string name, IDictionary<string, IEnumerable<string>> values)
        {
            if (values != null && values.Any())
            {
                writer.WriteObjectStart(name);

                var sortedValues = values.OrderBy(pair => pair.Key, StringComparer.Ordinal);

                foreach (var pair in sortedValues)
                {
                    writer.WriteNameArray(pair.Key, pair.Value);
                }

                writer.WriteObjectEnd();
            }
        }
    }
}
