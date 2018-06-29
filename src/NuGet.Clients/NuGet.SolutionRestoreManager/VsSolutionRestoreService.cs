// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Shared;
using NuGet.Versioning;
using NuGet.VisualStudio;
using static NuGet.Frameworks.FrameworkConstants;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Implementation of the <see cref="IVsSolutionRestoreService"/> and <see cref="IVsSolutionRestoreService2"/>.
    /// Provides extension API for project restore nomination triggered by 3rd party component.
    /// Configured as a single-instance MEF part.
    /// </summary>
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IVsSolutionRestoreService))]
    [Export(typeof(IVsSolutionRestoreService2))]
    public sealed class VsSolutionRestoreService : IVsSolutionRestoreService, IVsSolutionRestoreService2
    {
        private const string PackageId = nameof(PackageId);
        private const string PackageVersion = nameof(PackageVersion);
        private const string Version = nameof(Version);
        private const string IncludeAssets = "IncludeAssets";
        private const string ExcludeAssets = "ExcludeAssets";
        private const string PrivateAssets = "PrivateAssets";
        private const string PackageTargetFallback = "PackageTargetFallback";
        private const string RuntimeIdentifier = "RuntimeIdentifier";
        private const string RuntimeIdentifiers = "RuntimeIdentifiers";
        private const string RuntimeSupports = "RuntimeSupports";
        private const string Clear = nameof(Clear);
        private const string RestorePackagesPath = nameof(RestorePackagesPath);
        private const string RestoreSources = nameof(RestoreSources);
        private const string RestoreFallbackFolders = nameof(RestoreFallbackFolders);
        private const string AssetTargetFallback = nameof(AssetTargetFallback);
        private const string RestoreAdditionalProjectFallbackFoldersExcludes = nameof(RestoreAdditionalProjectFallbackFoldersExcludes);
        private const string RestoreAdditionalProjectFallbackFolders = nameof(RestoreAdditionalProjectFallbackFolders);
        private const string RestoreAdditionalProjectSources = nameof(RestoreAdditionalProjectSources);
        private const string TreatWarningsAsErrors = nameof(TreatWarningsAsErrors);
        private const string WarningsAsErrors = nameof(WarningsAsErrors);
        private const string NoWarn = nameof(NoWarn);


        private static readonly Version Version20 = new Version(2, 0, 0, 0);

        private readonly IProjectSystemCache _projectSystemCache;
        private readonly ISolutionRestoreWorker _restoreWorker;
        private readonly NuGet.Common.ILogger _logger;

        [ImportingConstructor]
        public VsSolutionRestoreService(
            IProjectSystemCache projectSystemCache,
            ISolutionRestoreWorker restoreWorker,
            [Import("VisualStudioActivityLogger")]
            NuGet.Common.ILogger logger)
        {
            if (projectSystemCache == null)
            {
                throw new ArgumentNullException(nameof(projectSystemCache));
            }

            if (restoreWorker == null)
            {
                throw new ArgumentNullException(nameof(restoreWorker));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _projectSystemCache = projectSystemCache;
            _restoreWorker = restoreWorker;
            _logger = logger;
        }

        public Task<bool> CurrentRestoreOperation => _restoreWorker.CurrentRestoreOperation;

        public Task<bool> NominateProjectAsync(string projectUniqueName, CancellationToken token)
        {
            Assumes.NotNullOrEmpty(projectUniqueName);

            // returned task completes when scheduled restore operation completes.
            var restoreTask = _restoreWorker.ScheduleRestoreAsync(
                SolutionRestoreRequest.OnUpdate(),
                token);

            return restoreTask;
        }

        public Task<bool> NominateProjectAsync(string projectUniqueName, IVsProjectRestoreInfo projectRestoreInfo, CancellationToken token)
        {
            if (string.IsNullOrEmpty(projectUniqueName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(projectUniqueName));
            }

            if (projectRestoreInfo == null)
            {
                throw new ArgumentNullException(nameof(projectRestoreInfo));
            }

            if (projectRestoreInfo.TargetFrameworks == null)
            {
                throw new InvalidOperationException("TargetFrameworks cannot be null.");
            }

            try
            {
                _logger.LogInformation(
                    $"The nominate API is called for '{projectUniqueName}'.");

                var projectNames = ProjectNames.FromFullProjectPath(projectUniqueName);

                var dgSpec = ToDependencyGraphSpec(projectNames, projectRestoreInfo);
#if DEBUG
                DumpProjectRestoreInfo(projectUniqueName, dgSpec);
#endif
                _projectSystemCache.AddProjectRestoreInfo(projectNames, dgSpec);

                // returned task completes when scheduled restore operation completes.
                var restoreTask = _restoreWorker.ScheduleRestoreAsync(
                    SolutionRestoreRequest.OnUpdate(),
                    token);

                return restoreTask;
            }
            catch (Exception e)
            when (e is InvalidOperationException || e is ArgumentException || e is FormatException)
            {
                _logger.LogError(e.ToString());
                return Task.FromResult(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                throw;
            }
        }

#if DEBUG
        private void DumpProjectRestoreInfo(string projectUniqueName, DependencyGraphSpec projectRestoreInfo)
        {
            try
            {
                var packageSpec = projectRestoreInfo.GetProjectSpec(projectUniqueName);
                var outputPath = packageSpec.RestoreMetadata.OutputPath;
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                var dgPath = Path.Combine(outputPath, $"{Guid.NewGuid()}.dg");
                projectRestoreInfo.Save(dgPath);
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }
        }
#endif

        private static DependencyGraphSpec ToDependencyGraphSpec(ProjectNames projectNames, IVsProjectRestoreInfo projectRestoreInfo)
        {
            var dgSpec = new DependencyGraphSpec();

            var packageSpec = ToPackageSpec(projectNames, projectRestoreInfo);
            dgSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);
            dgSpec.AddProject(packageSpec);

            if (projectRestoreInfo.ToolReferences != null)
            {
                var toolFramework = GetSingleNonEvaluatedPropertyOrNull(
                    projectRestoreInfo.TargetFrameworks,
                    ProjectBuildProperties.DotnetCliToolTargetFramework,
                    NuGetFramework.Parse) ?? CommonFrameworks.NetCoreApp10;

                var packagesPath = GetRestoreProjectPath(projectRestoreInfo.TargetFrameworks);
                var fallbackFolders = GetRestoreFallbackFolders(projectRestoreInfo.TargetFrameworks).AsList();
                var sources = GetRestoreSources(projectRestoreInfo.TargetFrameworks)
                    .Select(e => new PackageSource(e))
                    .ToList();

                projectRestoreInfo
                    .ToolReferences
                    .Cast<IVsReferenceItem>()
                    .Select(r => ToolRestoreUtility.GetSpec(
                        projectNames.FullName,
                        r.Name,
                        GetVersionRange(r),
                        toolFramework,
                        packagesPath,
                        fallbackFolders,
                        sources,
                        projectWideWarningProperties: null))
                    .ForEach(ts =>
                    {
                        dgSpec.AddRestore(ts.RestoreMetadata.ProjectUniqueName);
                        dgSpec.AddProject(ts);
                    });
            }

            return dgSpec;
        }

        private static PackageSpec ToPackageSpec(ProjectNames projectNames, IVsProjectRestoreInfo projectRestoreInfo)
        {
            var tfis = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .Select(ToTargetFrameworkInformation)
                .ToArray();

            var projectFullPath = Path.GetFullPath(projectNames.FullName);
            var projectDirectory = Path.GetDirectoryName(projectFullPath);

            // TODO: Remove temporary integration code NuGet/Home#3810
            // Initialize OTF and CT values when original value of OTF property is not provided.
            var originalTargetFrameworks = tfis
                .Select(tfi => tfi.FrameworkName.GetShortFolderName())
                .ToArray();
            var crossTargeting = originalTargetFrameworks.Length > 1;

            // if "TargetFrameworks" property presents in the project file prefer the raw value.
            if (!string.IsNullOrWhiteSpace(projectRestoreInfo.OriginalTargetFrameworks))
            {
                originalTargetFrameworks = MSBuildStringUtility.Split(
                    projectRestoreInfo.OriginalTargetFrameworks);
                // cross-targeting is always ON even in case of a single tfm in the list.
                crossTargeting = true;
            }


            var outputPath = Path.GetFullPath(
                                Path.Combine(
                                    projectDirectory,
                                    projectRestoreInfo.BaseIntermediatePath));

            var projectName = GetPackageId(projectNames, projectRestoreInfo.TargetFrameworks);

            var packageSpec = new PackageSpec(tfis)
            {
                Name = projectName,
                Version = GetPackageVersion(projectRestoreInfo.TargetFrameworks),
                FilePath = projectFullPath,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectName = projectName,
                    ProjectUniqueName = projectFullPath,
                    ProjectPath = projectFullPath,
                    OutputPath = outputPath,
                    ProjectStyle = ProjectStyle.PackageReference,
                    TargetFrameworks = projectRestoreInfo.TargetFrameworks
                        .Cast<IVsTargetFrameworkInfo>()
                        .Select(item => ToProjectRestoreMetadataFrameworkInfo(item, projectDirectory))
                        .ToList(),
                    OriginalTargetFrameworks = originalTargetFrameworks,
                    CrossTargeting = crossTargeting,

                    // Read project properties for settings. ISettings values will be applied later since
                    // this value is put in the nomination cache and ISettings could change.
                    PackagesPath = GetRestoreProjectPath(projectRestoreInfo.TargetFrameworks),
                    FallbackFolders = GetRestoreFallbackFolders(projectRestoreInfo.TargetFrameworks).AsList(),
                    Sources = GetRestoreSources(projectRestoreInfo.TargetFrameworks)
                                    .Select(e => new PackageSource(e))
                                    .ToList(),
                    ProjectWideWarningProperties = WarningProperties.GetWarningProperties(
                        treatWarningsAsErrors: GetSingleOrDefaultPropertyValue(projectRestoreInfo.TargetFrameworks, TreatWarningsAsErrors, e => e),
                        warningsAsErrors: GetSingleOrDefaultNuGetLogCodes(projectRestoreInfo.TargetFrameworks, WarningsAsErrors, e => MSBuildStringUtility.GetNuGetLogCodes(e)),
                        noWarn: GetSingleOrDefaultNuGetLogCodes(projectRestoreInfo.TargetFrameworks, NoWarn, e => MSBuildStringUtility.GetNuGetLogCodes(e))),
                    CacheFilePath = NoOpRestoreUtilities.GetProjectCacheFilePath(cacheRoot: outputPath, projectPath: projectFullPath)
                },
                RuntimeGraph = GetRuntimeGraph(projectRestoreInfo),
                RestoreSettings = new ProjectRestoreSettings() { HideWarningsAndErrors = true }
            };

            return packageSpec;
        }

        private static string GetPackageId(ProjectNames projectNames, IVsTargetFrameworks tfms)
        {
            var packageId = GetSingleNonEvaluatedPropertyOrNull(tfms, PackageId, v => v);
            return packageId ?? projectNames.ShortName;
        }

        private static NuGetVersion GetPackageVersion(IVsTargetFrameworks tfms)
        {
            // $(PackageVersion) property if set overrides the $(Version)
            var versionPropertyValue =
                GetSingleNonEvaluatedPropertyOrNull(tfms, PackageVersion, NuGetVersion.Parse)
                ?? GetSingleNonEvaluatedPropertyOrNull(tfms, Version, NuGetVersion.Parse);

            return versionPropertyValue ?? PackageSpec.DefaultVersion;
        }

        private static string GetRestoreProjectPath(IVsTargetFrameworks tfms)
        {
            return GetSingleNonEvaluatedPropertyOrNull(tfms, RestorePackagesPath, e => e);
        }

        /// <summary>
        /// The result will contain CLEAR and no sources specified in RestoreSources if the clear keyword is in it.
        /// If there are additional sources specified, the value AdditionalValue will be set in the result and then all the additional sources will follow
        /// </summary>
        private static IEnumerable<string> GetRestoreSources(IVsTargetFrameworks tfms)
        {
            var sources = HandleClear(MSBuildStringUtility.Split(GetSingleNonEvaluatedPropertyOrNull(tfms, RestoreSources, e => e)));

            // Read RestoreAdditionalProjectSources from the inner build, these may be different between frameworks.
            // Exclude is not allowed for sources
            var additional = MSBuildRestoreUtility.AggregateSources(
                values: GetAggregatePropertyValues(tfms, RestoreAdditionalProjectSources),
                excludeValues: Enumerable.Empty<string>());

            return VSRestoreSettingsUtilities.GetEntriesWithAdditional(sources, additional.ToArray());
        }

        /// <summary>
        /// The result will contain CLEAR and no sources specified in RestoreFallbackFolders if the clear keyword is in it.
        /// If there are additional fallback folders specified, the value AdditionalValue will be set in the result and then all the additional fallback folders will follow
        /// </summary>
        private static IEnumerable<string> GetRestoreFallbackFolders(IVsTargetFrameworks tfms)
        {
            var folders = HandleClear(MSBuildStringUtility.Split(GetSingleNonEvaluatedPropertyOrNull(tfms, RestoreFallbackFolders, e => e)));

            // Read RestoreAdditionalProjectFallbackFolders from the inner build.
            // Remove all excluded fallback folders listed in RestoreAdditionalProjectFallbackFoldersExcludes.
            var additional = MSBuildRestoreUtility.AggregateSources(
                values: GetAggregatePropertyValues(tfms, RestoreAdditionalProjectFallbackFolders),
                excludeValues: GetAggregatePropertyValues(tfms, RestoreAdditionalProjectFallbackFoldersExcludes));

            return VSRestoreSettingsUtilities.GetEntriesWithAdditional(folders, additional.ToArray());
        }

        private static string[] HandleClear(string[] input)
        {
            if (input.Any(e => StringComparer.OrdinalIgnoreCase.Equals(Clear, e)))
            {
                return new string[] { Clear };
            }

            return input;
        }

        private static TValue GetSingleOrDefaultPropertyValue<TValue>(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, TValue> valueFactory)
        {
            var properties = GetNonEvaluatedPropertyOrNull(tfms, propertyName, valueFactory);

            return properties.Count() > 1 ? default(TValue) : properties.SingleOrDefault();
        }

        private static IEnumerable<NuGetLogCode> GetSingleOrDefaultNuGetLogCodes(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, IEnumerable<NuGetLogCode>> valueFactory)
        {
            var logCodeProperties = GetNonEvaluatedPropertyOrNull(tfms, propertyName, valueFactory);

            return MSBuildStringUtility.GetDistinctNuGetLogCodesOrDefault(logCodeProperties);
        }

        // Trying to fetch a list of property value from all tfm property bags.
        private static IEnumerable<TValue> GetNonEvaluatedPropertyOrNull<TValue>(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, TValue> valueFactory)
        {
            return tfms
                .Cast<IVsTargetFrameworkInfo>()
                .Select(tfm =>
                {
                    var val = GetPropertyValueOrNull(tfm.Properties, propertyName);
                    return val != null ? valueFactory(val) : default(TValue);
                })
                .Distinct();
        }

        // Trying to fetch a property value from tfm property bags.
        // If defined the property should have identical values in all of the occurances.
        private static TValue GetSingleNonEvaluatedPropertyOrNull<TValue>(
            IVsTargetFrameworks tfms,
            string propertyName,
            Func<string, TValue> valueFactory)
        {
            return GetNonEvaluatedPropertyOrNull(tfms, propertyName, valueFactory).SingleOrDefault();
        }

        /// <summary>
        /// Fetch all property values from each target framework and combine them.
        /// </summary>
        private static IEnumerable<string> GetAggregatePropertyValues(
                IVsTargetFrameworks tfms,
                string propertyName)
        {
            // Only non-null values are added to the list as part of the split.
            return tfms
                .Cast<IVsTargetFrameworkInfo>()
                .SelectMany(tfm => MSBuildStringUtility.Split(GetPropertyValueOrNull(tfm.Properties, propertyName)));
        }

        private static RuntimeGraph GetRuntimeGraph(IVsProjectRestoreInfo projectRestoreInfo)
        {
            var runtimes = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .SelectMany(tfi => new[]
                {
                    GetPropertyValueOrNull(tfi.Properties, RuntimeIdentifier),
                    GetPropertyValueOrNull(tfi.Properties, RuntimeIdentifiers),
                })
                .SelectMany(MSBuildStringUtility.Split)
                .Distinct(StringComparer.Ordinal)
                .Select(rid => new RuntimeDescription(rid))
                .ToList();

            var supports = projectRestoreInfo
                .TargetFrameworks
                .Cast<IVsTargetFrameworkInfo>()
                .Select(tfi => GetPropertyValueOrNull(tfi.Properties, RuntimeSupports))
                .SelectMany(MSBuildStringUtility.Split)
                .Distinct(StringComparer.Ordinal)
                .Select(s => new CompatibilityProfile(s))
                .ToList();

            return new RuntimeGraph(runtimes, supports);
        }

        private static TargetFrameworkInformation ToTargetFrameworkInformation(
            IVsTargetFrameworkInfo targetFrameworkInfo)
        {
            var tfi = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse(targetFrameworkInfo.TargetFrameworkMoniker)
            };

            var ptf = MSBuildStringUtility.Split(GetPropertyValueOrNull(targetFrameworkInfo.Properties, PackageTargetFallback))
                                          .Select(NuGetFramework.Parse)
                                          .ToList();

            var atf = MSBuildStringUtility.Split(GetPropertyValueOrNull(targetFrameworkInfo.Properties, AssetTargetFallback))
                                          .Select(NuGetFramework.Parse)
                                          .ToList();

            // Update TFI with fallback properties
            AssetTargetFallbackUtility.ApplyFramework(tfi, ptf, atf);

            if (targetFrameworkInfo.PackageReferences != null)
            {
                tfi.Dependencies.AddRange(
                    targetFrameworkInfo.PackageReferences
                        .Cast<IVsReferenceItem>()
                        .Select(ToPackageLibraryDependency));
            }

            return tfi;
        }

        private static ProjectRestoreMetadataFrameworkInfo ToProjectRestoreMetadataFrameworkInfo(
            IVsTargetFrameworkInfo targetFrameworkInfo,
            string projectDirectory)
        {
            var tfi = new ProjectRestoreMetadataFrameworkInfo
            {
                FrameworkName = NuGetFramework.Parse(targetFrameworkInfo.TargetFrameworkMoniker)
            };

            if (targetFrameworkInfo.ProjectReferences != null)
            {
                tfi.ProjectReferences.AddRange(
                    targetFrameworkInfo.ProjectReferences
                        .Cast<IVsReferenceItem>()
                        .Where(IsReferenceOutputAssemblyTrueOrEmpty)
                        .Select(item => ToProjectRestoreReference(item, projectDirectory)));
            }

            return tfi;
        }

        private static LibraryDependency ToPackageLibraryDependency(IVsReferenceItem item)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: item.Name,
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Package),

                // Mark packages coming from the SDK as AutoReferenced
                AutoReferenced = GetPropertyBoolOrFalse(item, "IsImplicitlyDefined"),
            };

            // Add warning suppressions
            foreach (var code in MSBuildStringUtility.GetNuGetLogCodes(GetPropertyValueOrNull(item, NoWarn)))
            {
                dependency.NoWarn.Add(code);
            }

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                includeAssets: GetPropertyValueOrNull(item, IncludeAssets),
                excludeAssets: GetPropertyValueOrNull(item, ExcludeAssets),
                privateAssets: GetPropertyValueOrNull(item, PrivateAssets));

            return dependency;
        }

        private static ProjectRestoreReference ToProjectRestoreReference(IVsReferenceItem item, string projectDirectory)
        {
            // The path may be a relative path, to match the project unique name as a
            // string this should be the full path to the project
            // Remove ../../ and any other relative parts of the path that were used in the project file
            var referencePath = Path.GetFullPath(Path.Combine(projectDirectory, item.Name));

            var dependency = new ProjectRestoreReference
            {
                ProjectPath = referencePath,
                ProjectUniqueName = referencePath,
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                includeAssets: GetPropertyValueOrNull(item, IncludeAssets),
                excludeAssets: GetPropertyValueOrNull(item, ExcludeAssets),
                privateAssets: GetPropertyValueOrNull(item, PrivateAssets));

            return dependency;
        }

        private static VersionRange GetVersionRange(IVsReferenceItem item)
        {
            var versionRange = GetPropertyValueOrNull(item, "Version");

            if (versionRange != null)
            {
                return VersionRange.Parse(versionRange);
            }

            return VersionRange.All;
        }

        private static bool GetPropertyBoolOrFalse(
                IVsReferenceItem item, string propertyName)
        {
            try
            {
                return MSBuildStringUtility.IsTrue(item.Properties?.Item(propertyName)?.Value);
            }
            catch (ArgumentException)
            {
            }
            catch (KeyNotFoundException)
            {
            }

            return false;
        }

        private static string GetPropertyValueOrNull(
            IVsReferenceItem item, string propertyName)
        {
            try
            {
                return MSBuildStringUtility.TrimAndGetNullForEmpty(item.Properties?.Item(propertyName)?.Value);
            }
            catch (ArgumentException)
            {
            }
            catch (KeyNotFoundException)
            {
            }

            return null;
        }

        private static string GetPropertyValueOrNull(
            IVsProjectProperties properties, string propertyName)
        {
            try
            {
                return MSBuildStringUtility.TrimAndGetNullForEmpty(properties?.Item(propertyName)?.Value);
            }
            catch (ArgumentException)
            {
            }
            catch (KeyNotFoundException)
            {
            }

            return null;
        }

        /// <summary>
        /// True if ReferenceOutputAssembly is true or empty.
        /// All other values will be false.
        /// </summary>
        private static bool IsReferenceOutputAssemblyTrueOrEmpty(IVsReferenceItem item)
        {
            var value = GetPropertyValueOrNull(item, "ReferenceOutputAssembly");

            return MSBuildStringUtility.IsTrueOrEmpty(value);
        }
    }
}
