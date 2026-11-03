// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.RuntimeModel;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// Writes out a PackageSpec object graph.
    ///
    /// This is non-private only to facilitate unit testing.
    /// </summary>
    public sealed class PackageSpecWriter
    {
        /// <summary>
        /// Caches process-level values that are shared by every package specification in one write operation.
        /// </summary>
        /// <remarks>
        /// A dependency graph can contain thousands of package specifications. Creating this context once for the
        /// complete graph avoids repeatedly reading the macro environment variable and resolving the user-settings
        /// directory for every project, while keeping those values consistent throughout a single Save or GetHash
        /// operation. Single-package entry points create their own context to preserve their existing behavior.
        /// </remarks>
        internal readonly struct WriteContext
        {
            internal WriteContext(bool useMacros, string userSettingsDirectory)
            {
                UseMacros = useMacros;
                UserSettingsDirectory = userSettingsDirectory;
            }

            internal bool UseMacros { get; }

            internal string UserSettingsDirectory { get; }
        }

        /// <summary>
        /// Writes a PackageSpec to an <c>NuGet.Common.IObjectWriter</c> instance.
        /// </summary>
        /// <param name="packageSpec">A <c>PackageSpec</c> instance.</param>
        /// <param name="writer">An <c>NuGet.Common.IObjectWriter</c> instance.</param>
        public static void Write(PackageSpec packageSpec, IObjectWriter writer)
        {
            Write(packageSpec, writer, hashing: false, EnvironmentVariableWrapper.Instance);
        }

        internal static void Write(PackageSpec packageSpec, IObjectWriter writer, bool hashing, IEnvironmentVariableReader environmentVariableReader)
        {
            Write(packageSpec, writer, hashing, environmentVariableReader, useLegacyWriter: false);
        }

        internal static void Write(PackageSpec packageSpec, IObjectWriter writer, bool hashing, IEnvironmentVariableReader environmentVariableReader, bool useLegacyWriter)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            Write(packageSpec, new JsonObjectWriterAdapter(writer), hashing, environmentVariableReader, useLegacyWriter);
        }

        internal static void Write<TWriter>(PackageSpec packageSpec, TWriter writer, bool hashing, IEnvironmentVariableReader environmentVariableReader)
            where TWriter : IJsonObjectWriter
        {
            Write(packageSpec, writer, hashing, environmentVariableReader, useLegacyWriter: false);
        }

        internal static void Write<TWriter>(PackageSpec packageSpec, TWriter writer, bool hashing, WriteContext context)
            where TWriter : IJsonObjectWriter
        {
            Write(packageSpec, writer, hashing, context, useLegacyWriter: false);
        }

        internal static void Write<TWriter>(PackageSpec packageSpec, TWriter writer, bool hashing, IEnvironmentVariableReader environmentVariableReader, bool useLegacyWriter)
            where TWriter : IJsonObjectWriter
        {
            Write(packageSpec, writer, hashing, CreateWriteContext(environmentVariableReader), useLegacyWriter);
        }

        internal static WriteContext CreateWriteContext(IEnvironmentVariableReader environmentVariableReader)
        {
            bool useMacros = MSBuildStringUtility.IsTrue(environmentVariableReader.GetEnvironmentVariable(MacroStringsUtility.NUGET_ENABLE_EXPERIMENTAL_MACROS));
            string userSettingsDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory);
            return new WriteContext(useMacros, userSettingsDirectory);
        }

        private static void Write<TWriter>(PackageSpec packageSpec, TWriter writer, bool hashing, WriteContext context, bool useLegacyWriter)
            where TWriter : IJsonObjectWriter
        {
            if (packageSpec == null)
            {
                throw new ArgumentNullException(nameof(packageSpec));
            }

            if (!packageSpec.IsDefaultVersion)
            {
                SetValue(writer, JsonPropertyNames.Version, packageSpec.Version?.ToFullString());
            }

            SetMSBuildMetadata(writer, packageSpec, hashing, context, useLegacyWriter);

            SetFrameworks(writer, packageSpec.TargetFrameworks, hashing, useLegacyWriter);

            RuntimeGraph runtimeGraph = packageSpec.RuntimeGraph;
            if (runtimeGraph != null && (runtimeGraph.Runtimes.Count > 0 || runtimeGraph.Supports.Count > 0))
            {
                JsonRuntimeFormat.WriteRuntimeGraph(writer, runtimeGraph);
            }
        }

        internal static string NormalizePathForHashing(string path, bool hashing)
        {
            return hashing && PathUtility.IsFileSystemCaseInsensitive ? path?.ToUpperInvariant() : path;
        }

        private static bool IsMetadataValid(ProjectRestoreMetadata msbuildMetadata)
        {
            if (msbuildMetadata == null)
            {
                return false;
            }

            if (msbuildMetadata.ProjectUniqueName == null && msbuildMetadata.ProjectName == null
                && msbuildMetadata.ProjectPath == null && msbuildMetadata.ProjectJsonPath == null
                && msbuildMetadata.PackagesPath == null && msbuildMetadata.OutputPath == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// This method sets the msbuild metadata that's important for restore. Ensures that frameworks regardless of which way they're stores in the metadata(full name or short tfm name) are written out the same.
        /// </summary>
        private static void SetMSBuildMetadata<TWriter>(TWriter writer, PackageSpec packageSpec, bool hashing, WriteContext context, bool useTargetFrameworkAsKey)
            where TWriter : IJsonObjectWriter
        {
            var msbuildMetadata = packageSpec.RestoreMetadata;

            if (!IsMetadataValid(msbuildMetadata))
            {
                return;
            }

            bool useMacros = context.UseMacros;
            string userSettingsDirectory = context.UserSettingsDirectory;

            writer.WriteObjectStart(JsonPropertyNames.Restore);

            SetValue(writer, JsonPropertyNames.ProjectUniqueName, NormalizePathForHashing(msbuildMetadata.ProjectUniqueName, hashing));
            SetValue(writer, JsonPropertyNames.ProjectName, msbuildMetadata.ProjectName);
            SetValue(writer, JsonPropertyNames.ProjectPath, NormalizePathForHashing(msbuildMetadata.ProjectPath, hashing));
            SetValue(writer, JsonPropertyNames.ProjectJsonPath, msbuildMetadata.ProjectJsonPath);
            SetValue(writer, JsonPropertyNames.PackagesPath, ApplyMacro(msbuildMetadata.PackagesPath, userSettingsDirectory, useMacros));
            SetValue(writer, JsonPropertyNames.OutputPath, NormalizePathForHashing(msbuildMetadata.OutputPath, hashing));

            if (msbuildMetadata.ProjectStyle != ProjectStyle.Unknown)
            {
                SetValue(writer, JsonPropertyNames.ProjectStyle, msbuildMetadata.ProjectStyle.ToString());
            }

            WriteMetadataBooleans(writer, msbuildMetadata);

            if (useMacros)
            {
                var fallbackFolderCopy = msbuildMetadata.FallbackFolders.ToList();
                var configFilePathsCopy = msbuildMetadata.ConfigFilePaths.ToList();
                MacroStringsUtility.ApplyMacros(fallbackFolderCopy, userSettingsDirectory, MacroStringsUtility.UserMacro, PathUtility.GetStringComparisonBasedOnOS());
                MacroStringsUtility.ApplyMacros(configFilePathsCopy, userSettingsDirectory, MacroStringsUtility.UserMacro, PathUtility.GetStringComparisonBasedOnOS());

                SetArrayValue(writer, JsonPropertyNames.FallbackFolders, fallbackFolderCopy);
                SetArrayValue(writer, JsonPropertyNames.ConfigFilePaths, configFilePathsCopy);
            }
            else
            {
                SetArrayValue(writer, JsonPropertyNames.FallbackFolders, msbuildMetadata.FallbackFolders);
                SetArrayValue(writer, JsonPropertyNames.ConfigFilePaths, msbuildMetadata.ConfigFilePaths);
            }

            // This need to stay the original strings because the nuget.g.targets have conditional imports based on the original framework name
            IEnumerable<string> originalTargetFrameworks = msbuildMetadata.OriginalTargetFrameworks.Count <= 1
                ? msbuildMetadata.OriginalTargetFrameworks
                : msbuildMetadata.OriginalTargetFrameworks.OrderBy(c => c, StringComparer.Ordinal);
            SetArrayValue(writer, JsonPropertyNames.OriginalTargetFrameworks, originalTargetFrameworks);

            WriteMetadataSources(writer, msbuildMetadata);
            WriteMetadataFiles(writer, msbuildMetadata);
            WriteMetadataTargetFrameworks(writer, msbuildMetadata, hashing, useTargetFrameworkAsKey);
            SetWarningProperties(writer, msbuildMetadata);

            WriteNuGetLockFileProperties(writer, msbuildMetadata);
            WriteNuGetAuditProperties(writer, msbuildMetadata.RestoreAuditProperties);

            if (msbuildMetadata is PackagesConfigProjectRestoreMetadata pcMsbuildMetadata)
            {
                SetValue(writer, JsonPropertyNames.PackagesConfigPath, pcMsbuildMetadata.PackagesConfigPath);
            }

            if (packageSpec.RestoreMetadata.SdkAnalysisLevel is not null)
            {
                SetValue(writer, JsonPropertyNames.SdkAnalysisLevel, packageSpec.RestoreMetadata.SdkAnalysisLevel.ToString());
            }

            writer.WriteObjectEnd();
        }

        private static string ApplyMacro(string value, string userSettingsDirectory, bool useMacros)
        {
            if (useMacros)
            {
                return MacroStringsUtility.ApplyMacro(value, userSettingsDirectory, MacroStringsUtility.UserMacro, PathUtility.GetStringComparisonBasedOnOS());
            }
            return value;
        }

        private static void WriteMetadataBooleans<TWriter>(TWriter writer, ProjectRestoreMetadata msbuildMetadata)
            where TWriter : IJsonObjectWriter
        {
            SetValueIfTrue(writer, JsonPropertyNames.CrossTargeting, msbuildMetadata.CrossTargeting);
            SetValueIfTrue(writer, JsonPropertyNames.LegacyPackagesDirectory, msbuildMetadata.LegacyPackagesDirectory);
            SetValueIfTrue(writer, JsonPropertyNames.ValidateRuntimeAssets, msbuildMetadata.ValidateRuntimeAssets);
            SetValueIfTrue(writer, JsonPropertyNames.SkipContentFileWrite, msbuildMetadata.SkipContentFileWrite);
            SetValueIfTrue(writer, JsonPropertyNames.CentralPackageVersionsManagementEnabled, msbuildMetadata.CentralPackageVersionsEnabled);
            SetValueIfTrue(writer, JsonPropertyNames.CentralPackageFloatingVersionsEnabled, msbuildMetadata.CentralPackageFloatingVersionsEnabled);
            SetValueIfTrue(writer, JsonPropertyNames.CentralPackageVersionOverrideDisabled, msbuildMetadata.CentralPackageVersionOverrideDisabled);
            SetValueIfTrue(writer, JsonPropertyNames.CentralPackageTransitivePinningEnabled, msbuildMetadata.CentralPackageTransitivePinningEnabled);
            SetValueIfFalse(writer, JsonPropertyNames.UsingMicrosoftNETSdk, msbuildMetadata.UsingMicrosoftNETSdk);
            SetValueIfTrue(writer, JsonPropertyNames.RestoreEnableAnalyzerAssets, msbuildMetadata.RestoreEnableAnalyzerAssets);
            SetValueIfTrue(writer, JsonPropertyNames.RestoreUseLegacyDependencyResolver, msbuildMetadata.UseLegacyDependencyResolver);
            SetValueIfTrue(writer, JsonPropertyNames.RestoreDoNotWriteDependencyGraphSpec, msbuildMetadata.RestoreDoNotWriteDependencyGraphSpec);
        }


        private static void WriteNuGetLockFileProperties<TWriter>(TWriter writer, ProjectRestoreMetadata msbuildMetadata)
            where TWriter : IJsonObjectWriter
        {
            if (msbuildMetadata.RestoreLockProperties != null &&
                (!string.IsNullOrEmpty(msbuildMetadata.RestoreLockProperties.RestorePackagesWithLockFile) ||
                 !string.IsNullOrEmpty(msbuildMetadata.RestoreLockProperties.NuGetLockFilePath) ||
                 msbuildMetadata.RestoreLockProperties.RestoreLockedMode))
            {
                writer.WriteObjectStart(JsonPropertyNames.RestoreLockProperties);

                SetValue(writer, JsonPropertyNames.RestorePackagesWithLockFile, msbuildMetadata.RestoreLockProperties.RestorePackagesWithLockFile);
                SetValue(writer, JsonPropertyNames.NuGetLockFilePath, msbuildMetadata.RestoreLockProperties.NuGetLockFilePath);
                SetValueIfTrue(writer, JsonPropertyNames.RestoreLockedMode, msbuildMetadata.RestoreLockProperties.RestoreLockedMode);

                writer.WriteObjectEnd();
            }
        }

        private static void WriteNuGetAuditProperties<TWriter>(TWriter writer, RestoreAuditProperties auditProperties)
            where TWriter : IJsonObjectWriter
        {
            if (auditProperties == null) return;

            writer.WriteObjectStart(JsonPropertyNames.RestoreAuditProperties);

            SetValueIfNotNull(writer, JsonPropertyNames.EnableAudit, auditProperties.EnableAudit);
            SetValueIfNotNull(writer, JsonPropertyNames.AuditLevel, auditProperties.AuditLevel);
            SetValueIfNotNull(writer, JsonPropertyNames.AuditMode, auditProperties.AuditMode);

            if (auditProperties.SuppressedAdvisories?.Count > 0)
            {
                writer.WriteObjectStart(JsonPropertyNames.SuppressedAdvisories);

                foreach (string advisory in auditProperties.SuppressedAdvisories)
                {
                    writer.WriteNameValue(advisory, null);
                }

                writer.WriteObjectEnd();
            }

            writer.WriteObjectEnd();
        }

        private static void WriteMetadataTargetFrameworks<TWriter>(TWriter writer, ProjectRestoreMetadata msbuildMetadata, bool hashing, bool useTargetFrameworkAsKey)
            where TWriter : IJsonObjectWriter
        {
            if (msbuildMetadata.TargetFrameworks?.Count > 0)
            {
                writer.WriteObjectStart(JsonPropertyNames.Frameworks);

                IEnumerable<ProjectRestoreMetadataFrameworkInfo> frameworks = msbuildMetadata.TargetFrameworks.Count == 1
                    ? msbuildMetadata.TargetFrameworks
                    : msbuildMetadata.TargetFrameworks.OrderBy(c => c.TargetAlias, StringComparer.OrdinalIgnoreCase);

                HashSet<string> frameworkNames = msbuildMetadata.TargetFrameworks.Count == 1 ? null : new HashSet<string>();
                foreach (var framework in frameworks)
                {
                    string frameworkName = framework.FrameworkName.GetShortFolderName();
                    string frameworkHeader = useTargetFrameworkAsKey || string.IsNullOrEmpty(framework.TargetAlias)
                        ? frameworkName
                        : framework.TargetAlias;

                    if (frameworkNames == null || frameworkNames.Add(frameworkHeader))
                    {
                        writer.WriteObjectStart(frameworkHeader);

                        SetValue(writer, JsonPropertyNames.Framework, frameworkName);
                        SetValueIfNotNull(writer, JsonPropertyNames.TargetAlias, framework.TargetAlias);
                        writer.WriteObjectStart(JsonPropertyNames.ProjectReferences);

                        IEnumerable<ProjectRestoreReference> projectReferences = framework.ProjectReferences.Count <= 1
                            ? framework.ProjectReferences
                            : framework.ProjectReferences.OrderBy(e => e.ProjectPath, PathUtility.GetStringComparerBasedOnOS());

                        foreach (var project in projectReferences)
                        {
                            writer.WriteObjectStart(NormalizePathForHashing(project.ProjectUniqueName, hashing));

                            writer.WriteNameValue(JsonPropertyNames.ProjectPath, NormalizePathForHashing(project.ProjectPath, hashing));

                            if (project.IncludeAssets != LibraryIncludeFlags.All)
                            {
                                writer.WriteNameValue(JsonPropertyNames.IncludeAssets, LibraryIncludeFlagUtils.GetFlagString(project.IncludeAssets));
                            }

                            if (project.ExcludeAssets != LibraryIncludeFlags.None)
                            {
                                writer.WriteNameValue(JsonPropertyNames.ExcludeAssets, LibraryIncludeFlagUtils.GetFlagString(project.ExcludeAssets));
                            }

                            if (project.PrivateAssets != LibraryIncludeFlagUtils.DefaultSuppressParent)
                            {
                                writer.WriteNameValue(JsonPropertyNames.PrivateAssets, LibraryIncludeFlagUtils.GetFlagString(project.PrivateAssets));
                            }

                            writer.WriteObjectEnd();
                        }

                        writer.WriteObjectEnd();
                        writer.WriteObjectEnd();
                    }
                }

                writer.WriteObjectEnd();
            }
        }

        private static void WriteMetadataFiles<TWriter>(TWriter writer, ProjectRestoreMetadata msbuildMetadata)
            where TWriter : IJsonObjectWriter
        {
            if (msbuildMetadata.Files?.Count > 0)
            {
                writer.WriteObjectStart(JsonPropertyNames.Files);

                foreach (var file in msbuildMetadata.Files)
                {
                    SetValue(writer, file.PackagePath, file.AbsolutePath);
                }

                writer.WriteObjectEnd();
            }
        }

        private static void WriteMetadataSources<TWriter>(TWriter writer, ProjectRestoreMetadata msbuildMetadata)
            where TWriter : IJsonObjectWriter
        {
            if (msbuildMetadata.Sources?.Count > 0)
            {
                writer.WriteObjectStart(JsonPropertyNames.Sources);

                if (msbuildMetadata.Sources.Count == 1)
                {
                    PackageSource source = msbuildMetadata.Sources[0];
                    writer.WriteObjectStart(source.Source);
                    writer.WriteObjectEnd();
                }
                else
                {
                    foreach (var source in msbuildMetadata.Sources.OrderBy(e => e.Source, StringComparer.Ordinal))
                    {
                        writer.WriteObjectStart(source.Source);
                        writer.WriteObjectEnd();
                    }
                }

                writer.WriteObjectEnd();
            }
        }

        private static void SetWarningProperties<TWriter>(TWriter writer, ProjectRestoreMetadata msbuildMetadata)
            where TWriter : IJsonObjectWriter
        {
            if (msbuildMetadata.ProjectWideWarningProperties != null &&
                (msbuildMetadata.ProjectWideWarningProperties.AllWarningsAsErrors ||
                 msbuildMetadata.ProjectWideWarningProperties.NoWarn.Count > 0 ||
                 msbuildMetadata.ProjectWideWarningProperties.WarningsAsErrors.Count > 0))
            {
                writer.WriteObjectStart(JsonPropertyNames.WarningProperties);

                SetValueIfTrue(writer, JsonPropertyNames.AllWarningsAsErrors, msbuildMetadata.ProjectWideWarningProperties.AllWarningsAsErrors);

                if (msbuildMetadata.ProjectWideWarningProperties.NoWarn.Count > 0)
                {
                    SetArrayValue(writer, JsonPropertyNames.NoWarn, msbuildMetadata
                       .ProjectWideWarningProperties
                       .NoWarn
                       .OrderBy(c => c)
                       .Select(c => c.GetName())
                       .Where(c => !string.IsNullOrEmpty(c)));
                }

                if (msbuildMetadata.ProjectWideWarningProperties.WarningsAsErrors.Count > 0)
                {
                    SetArrayValue(writer, JsonPropertyNames.WarnAsError, msbuildMetadata
                        .ProjectWideWarningProperties
                        .WarningsAsErrors
                        .OrderBy(c => c)
                        .Select(c => c.GetName())
                        .Where(c => !string.IsNullOrEmpty(c)));
                }

                if (msbuildMetadata.ProjectWideWarningProperties.WarningsNotAsErrors.Count > 0)
                {
                    SetArrayValue(writer, JsonPropertyNames.WarnNotAsError, msbuildMetadata
                        .ProjectWideWarningProperties
                        .WarningsNotAsErrors
                        .OrderBy(c => c)
                        .Select(c => c.GetName())
                        .Where(c => !string.IsNullOrEmpty(c)));
                }

                writer.WriteObjectEnd();
            }
        }

        private static void SetDependencies<TWriter>(TWriter writer, ImmutableArray<LibraryDependency> libraryDependencies)
            where TWriter : IJsonObjectWriter
        {
            SetDependencies(writer, JsonPropertyNames.Dependencies, libraryDependencies, writeFrameworkAssemblies: false);
            SetDependencies(writer, JsonPropertyNames.FrameworkAssemblies, libraryDependencies, writeFrameworkAssemblies: true);
        }

        private static void SetDependencies<TWriter>(
            TWriter writer,
            JsonEncodedText name,
            ImmutableArray<LibraryDependency> libraryDependencies,
            bool writeFrameworkAssemblies)
            where TWriter : IJsonObjectWriter
        {
            bool hasDependencies = false;
            bool isSorted = true;
            string previousName = null;

            foreach (LibraryDependency dependency in libraryDependencies)
            {
                bool isFrameworkAssembly = dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference;
                if (isFrameworkAssembly != writeFrameworkAssemblies)
                {
                    continue;
                }

                hasDependencies = true;
                if (previousName != null && StringComparer.Ordinal.Compare(previousName, dependency.Name) > 0)
                {
                    isSorted = false;
                    break;
                }

                previousName = dependency.Name;
            }

            if (!hasDependencies)
            {
                return;
            }

            writer.WriteObjectStart(name);

            IEnumerable<LibraryDependency> dependencies = isSorted
                ? libraryDependencies
                : libraryDependencies
                    .Where(dependency => (dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference) == writeFrameworkAssemblies)
                    .OrderBy(dependency => dependency.Name, StringComparer.Ordinal);

            foreach (LibraryDependency dependency in dependencies)
            {
                bool isFrameworkAssembly = dependency.LibraryRange.TypeConstraint == LibraryDependencyTarget.Reference;
                if (isFrameworkAssembly == writeFrameworkAssemblies)
                {
                    WriteDependency(writer, dependency);
                }
            }

            writer.WriteObjectEnd();
        }

        /// <summary>
        /// This method sorts the libraries based on the name
        /// This method also writes out the normalized versions to avoid cases where original string is set because it was gotten through project system vs being installed from PM UI
        /// </summary>
        internal static void SetDependencies<TWriter>(TWriter writer, string name, IEnumerable<LibraryDependency> libraryDependencies)
            where TWriter : IJsonObjectWriter
        {
            if (!libraryDependencies.Any())
            {
                return;
            }

            writer.WriteObjectStart(name);

            foreach (var dependency in libraryDependencies.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                WriteDependency(writer, dependency);
            }

            writer.WriteObjectEnd();
        }

        private static void WriteDependency<TWriter>(TWriter writer, LibraryDependency dependency)
            where TWriter : IJsonObjectWriter
        {
            var expandedMode = dependency.IncludeType != LibraryIncludeFlags.All
                || dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent
                || dependency.AutoReferenced
                || (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference
                    && dependency.LibraryRange.TypeConstraint != (LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference))
                || !string.IsNullOrEmpty(dependency.Aliases)
                || dependency.GeneratePathProperty
                || dependency.VersionCentrallyManaged;

            var versionRange = dependency.LibraryRange.VersionRange ?? VersionRange.All;
            var versionString = versionRange.ToNormalizedString();

            if (expandedMode)
            {
                writer.WriteObjectStart(dependency.Name);

                if (dependency.IncludeType != LibraryIncludeFlags.All)
                {
                    SetValue(writer, JsonPropertyNames.Include, dependency.IncludeType.AsString());
                }

                if (dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
                {
                    SetValue(writer, JsonPropertyNames.SuppressParent, dependency.SuppressParent.AsString());
                }

                if (dependency.LibraryRange.TypeConstraint != LibraryDependencyTarget.Reference
                    && dependency.LibraryRange.TypeConstraint != (LibraryDependencyTarget.All & ~LibraryDependencyTarget.Reference))
                {
                    SetValue(writer, JsonPropertyNames.Target, dependency.LibraryRange.TypeConstraint.AsString());
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
                    SetValue(writer, JsonPropertyNames.Version, versionString);
                }

                if (dependency.VersionOverride != null)
                {
                    SetValue(writer, JsonPropertyNames.VersionOverride, dependency.VersionOverride.ToNormalizedString());
                }

                SetValueIfTrue(writer, JsonPropertyNames.AutoReferenced, dependency.AutoReferenced);

                if (dependency.NoWarn.Length > 0)
                {
                    SetArrayValue(writer, JsonPropertyNames.NoWarn, dependency
                        .NoWarn
                        .Distinct()
                        .OrderBy(c => c)
                        .Select(code => code.GetName())
                        .Where(s => !string.IsNullOrEmpty(s)));
                }

                SetValueIfTrue(writer, JsonPropertyNames.GeneratePathProperty, dependency.GeneratePathProperty);
                SetValueIfTrue(writer, JsonPropertyNames.VersionCentrallyManaged, dependency.VersionCentrallyManaged);
                SetValueIfNotNull(writer, JsonPropertyNames.Aliases, dependency.Aliases);
                writer.WriteObjectEnd();
            }
            else
            {
                writer.WriteNameValue(dependency.Name, versionString);
            }
        }

        /// <summary>
        /// The central transitive dependecy groups are used for pack operation.
        /// The metadata needed for pack is composed from:
        ///     Name, IncludeType, SuppressParent and Version
        /// </summary>
        internal static void SetCentralTransitveDependencyGroup<TWriter>(TWriter writer, string name, IEnumerable<LibraryDependency> libraryDependencies)
            where TWriter : IJsonObjectWriter
        {
            if (!libraryDependencies.Any())
            {
                return;
            }

            writer.WriteObjectStart(name);

            foreach (var dependency in libraryDependencies.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                var versionRange = dependency.LibraryRange.VersionRange ?? VersionRange.All;
                var versionString = versionRange.ToNormalizedString();

                writer.WriteObjectStart(dependency.Name);

                if (dependency.IncludeType != LibraryIncludeFlags.All)
                {
                    SetValue(writer, JsonPropertyNames.Include, dependency.IncludeType.ToString());
                }

                if (dependency.SuppressParent != LibraryIncludeFlagUtils.DefaultSuppressParent)
                {
                    SetValue(writer, JsonPropertyNames.SuppressParent, dependency.SuppressParent.ToString());
                }

                SetValue(writer, JsonPropertyNames.Version, versionString);

                writer.WriteObjectEnd();
            }

            writer.WriteObjectEnd();
        }

        private static void SetImports<TWriter>(TWriter writer, ImmutableArray<NuGetFramework> frameworks)
            where TWriter : IJsonObjectWriter
        {
            if (frameworks.Length > 0)
            {
                var imports = frameworks.Select(framework => framework.GetShortFolderName());

                writer.WriteNameArray(JsonPropertyNames.Imports, imports);
            }
        }

        private static void SetDownloadDependencies<TWriter>(TWriter writer, ImmutableArray<DownloadDependency> downloadDependencies)
            where TWriter : IJsonObjectWriter
        {
            if (downloadDependencies.Length == 0)
            {
                return;
            }

            writer.WriteArrayStart(JsonPropertyNames.DownloadDependencies);

            foreach (var dependency in downloadDependencies.GroupBy(dep => dep.Name).OrderBy(dep => dep.Key))
            {
                var version = string.Join(";", dependency.Select(dep => dep.VersionRange).OrderBy(dep => dep.MinVersion).Select(dep => dep.ToNormalizedString()));

                writer.WriteObjectStart();
                SetValue(writer, JsonPropertyNames.Name, dependency.Key);
                SetValue(writer, JsonPropertyNames.Version, version);
                writer.WriteObjectEnd();

            }
            writer.WriteArrayEnd();
        }

        private static void SetFrameworks<TWriter>(TWriter writer, IList<TargetFrameworkInformation> frameworks, bool hashing, bool useTargetFrameworkAsKey)
            where TWriter : IJsonObjectWriter
        {
            if (frameworks.Count > 0)
            {
                writer.WriteObjectStart(JsonPropertyNames.Frameworks);
                var frameworkSorter = NuGetFrameworkSorter.Instance;
                IEnumerable<TargetFrameworkInformation> orderedFrameworks = frameworks.Count == 1
                    ? frameworks
                    : frameworks.OrderBy(c => c.TargetAlias, StringComparer.OrdinalIgnoreCase);

                foreach (var framework in orderedFrameworks)
                {
                    string frameworkName = framework.FrameworkName.GetShortFolderName();
                    string frameworkHeader = useTargetFrameworkAsKey || string.IsNullOrEmpty(framework.TargetAlias)
                        ? frameworkName
                        : framework.TargetAlias;

                    writer.WriteObjectStart(frameworkHeader);
                    SetValue(writer, JsonPropertyNames.Framework, frameworkName);
                    SetValueIfNotNull(writer, JsonPropertyNames.TargetAlias, framework.TargetAlias);
                    SetDependencies(writer, framework.Dependencies);
                    SetCentralDependencies(writer, framework.CentralPackageVersions.Count, framework.CentralPackageVersions.Values, hashing);
                    SetImports(writer, framework.Imports);
                    SetValueIfTrue(writer, JsonPropertyNames.AssetTargetFallback, framework.AssetTargetFallback);
                    SetValueIfNotNull(writer, JsonPropertyNames.SecondaryFramework,
                        (DeconstructFallbackFrameworks(framework.FrameworkName) as DualCompatibilityFramework)?.SecondaryFramework.GetShortFolderName());
                    SetValueIfTrue(writer, JsonPropertyNames.Warn, framework.Warn);
                    SetDownloadDependencies(writer, framework.DownloadDependencies);
                    SetFrameworkReferences(writer, framework.FrameworkReferences);
                    SetValueIfNotNull(writer, JsonPropertyNames.RuntimeIdentifierGraphPath, framework.RuntimeIdentifierGraphPath);
                    SetPackagesToPrune(writer, framework.PackagesToPrune, hashing);
                    writer.WriteObjectEnd();
                }

                writer.WriteObjectEnd();
            }
        }

        // An AssetTargetFallbackFramework or FallbackFramework *might* have a DualCompatibilityFramework as it's root.
        private static NuGetFramework DeconstructFallbackFrameworks(NuGetFramework nuGetFramework)
        {
            if (nuGetFramework is AssetTargetFallbackFramework assetTargetFallbackFramework)
            {
                return assetTargetFallbackFramework.RootFramework;
            }

            if (nuGetFramework is FallbackFramework fallbackFramework)
            {
                return fallbackFramework;
            }

            return nuGetFramework;
        }

        private static void SetFrameworkReferences<TWriter>(TWriter writer, IReadOnlyCollection<FrameworkDependency> frameworkReferences)
            where TWriter : IJsonObjectWriter
        {
            if (frameworkReferences?.Count > 0)
            {
                writer.WriteObjectStart(JsonPropertyNames.FrameworkReferences);

                foreach (var dependency in frameworkReferences.OrderBy(dep => dep))
                {
                    writer.WriteObjectStart(dependency.Name);
                    SetValue(writer, JsonPropertyNames.PrivateAssets, FrameworkDependencyFlagsUtils.GetFlagString(dependency.PrivateAssets));
                    writer.WriteObjectEnd();
                }
                writer.WriteObjectEnd();
            }
        }

        private static void SetCentralDependencies<TWriter>(TWriter writer, int count, IEnumerable<CentralPackageVersion> centralPackageVersions, bool hashing)
            where TWriter : IJsonObjectWriter
        {
            if (count == 0)
            {
                return;
            }

            writer.WriteObjectStart(JsonPropertyNames.CentralPackageVersions);

            foreach (var dependency in centralPackageVersions.OrderBy(dep => dep.Name, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteNameValue(name: dependency.Name, value: dependency.VersionRange.OriginalString ?? dependency.VersionRange.ToNormalizedString());
            }

            writer.WriteObjectEnd();
        }

        private static void SetPackagesToPrune<TWriter>(TWriter writer, IReadOnlyDictionary<string, PrunePackageReference> packagesToPrune, bool hashing)
            where TWriter : IJsonObjectWriter
        {
            if (packagesToPrune.Count == 0)
            {
                return;
            }

            writer.WriteObjectStart(JsonPropertyNames.PackagesToPrune);

            foreach (var dependency in packagesToPrune.OrderBy(dep => dep.Key, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteNameValue(name: dependency.Key, value: dependency.Value.VersionRange.OriginalString ?? dependency.Value.VersionRange.ToNormalizedString());
            }

            writer.WriteObjectEnd();
        }

        private static void SetValueIfTrue<TWriter>(TWriter writer, JsonEncodedText name, bool value)
            where TWriter : IJsonObjectWriter
        {
            if (value)
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetValueIfFalse<TWriter>(TWriter writer, JsonEncodedText name, bool value)
            where TWriter : IJsonObjectWriter
        {
            if (!value)
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetValueIfNotNull<TWriter>(TWriter writer, JsonEncodedText name, string value)
            where TWriter : IJsonObjectWriter
        {
            if (!string.IsNullOrEmpty(value))
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetValue<TWriter>(TWriter writer, string name, string value)
            where TWriter : IJsonObjectWriter
        {
            if (value != null)
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetValue<TWriter>(TWriter writer, JsonEncodedText name, string value)
            where TWriter : IJsonObjectWriter
        {
            if (value != null)
            {
                writer.WriteNameValue(name, value);
            }
        }

        private static void SetArrayValue<TWriter>(TWriter writer, JsonEncodedText name, IEnumerable<string> values)
            where TWriter : IJsonObjectWriter
        {
            if (values != null)
            {
                writer.WriteNonEmptyNameArray(name, values);
            }
        }
    }
}
