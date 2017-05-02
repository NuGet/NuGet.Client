// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using static NuGet.Frameworks.FrameworkConstants;

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Implementation of the <see cref="IVsSolutionRestoreService"/>.
    /// Provides extension API for project restore nomination triggered by 3rd party component.
    /// Configured as a single-instance MEF part.
    /// </summary>
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IVsSolutionRestoreService))]
    public sealed class VsSolutionRestoreService : IVsSolutionRestoreService
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

        private static readonly Version Version20 = new Version(2, 0, 0, 0);

        private readonly IProjectSystemCache _projectSystemCache;
        private readonly ISolutionRestoreWorker _restoreWorker;
        private readonly NuGet.Common.ILogger _logger;

        [ImportingConstructor]
        public VsSolutionRestoreService(
            IProjectSystemCache projectSystemCache,
            ISolutionRestoreWorker restoreWorker,
            [Import(typeof(VisualStudioActivityLogger))]
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

        public Task<bool> NominateProjectAsync(string projectUniqueName, IVsProjectRestoreInfo projectRestoreInfo, CancellationToken token)
        {
            if (string.IsNullOrEmpty(projectUniqueName))
            {
                throw new ArgumentException(ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(projectUniqueName));
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
                // Infer tool's TFM version from the current project TFM
                var projectTfms = projectRestoreInfo
                    .TargetFrameworks
                    .Cast<IVsTargetFrameworkInfo>()
                    .Select(tfi => NuGetFramework.Parse(tfi.TargetFrameworkMoniker))
                    .ToList();

                var isNetCore20 = projectTfms
                    .Where(tfm => tfm.Framework == FrameworkIdentifiers.NetCoreApp || tfm.Framework == FrameworkIdentifiers.NetStandard)
                    .Any(tfm => tfm.Version >= Version20);
                var toolFramework = isNetCore20 ? CommonFrameworks.NetCoreApp20 : CommonFrameworks.NetCoreApp10;

                projectRestoreInfo
                    .ToolReferences
                    .Cast<IVsReferenceItem>()
                    .Select(r => ToToolPackageSpec(projectNames, r, toolFramework))
                    .ToList()
                    .ForEach(ts =>
                    {
                        dgSpec.AddRestore(ts.RestoreMetadata.ProjectUniqueName);
                        dgSpec.AddProject(ts);
                    });
            }

            return dgSpec;
        }

        private static PackageSpec ToToolPackageSpec(ProjectNames projectNames, IVsReferenceItem item, NuGetFramework toolFramework)
        {
            return ToolRestoreUtility.GetSpec(projectNames.FullName, item.Name, GetVersionRange(item), toolFramework);
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

            var packageSpec = new PackageSpec(tfis)
            {
                Name = GetPackageId(projectNames, projectRestoreInfo.TargetFrameworks),
                Version = GetPackageVersion(projectRestoreInfo.TargetFrameworks),
                FilePath = projectFullPath,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectName = projectNames.ShortName,
                    ProjectUniqueName = projectFullPath,
                    ProjectPath = projectFullPath,
                    OutputPath = Path.GetFullPath(
                        Path.Combine(
                            projectDirectory,
                            projectRestoreInfo.BaseIntermediatePath)),
                    ProjectStyle = ProjectStyle.PackageReference,
                    TargetFrameworks = projectRestoreInfo.TargetFrameworks
                        .Cast<IVsTargetFrameworkInfo>()
                        .Select(item => ToProjectRestoreMetadataFrameworkInfo(item, projectDirectory))
                        .ToList(),
                    OriginalTargetFrameworks = originalTargetFrameworks,
                    CrossTargeting = crossTargeting
                },
                RuntimeGraph = GetRuntimeGraph(projectRestoreInfo)
            };

            return packageSpec;
        }

        private static string GetPackageId(ProjectNames projectNames, IVsTargetFrameworks tfms)
        {
            var packageId = GetNonEvaluatedPropertyOrNull(tfms, PackageId, v => v);
            return packageId ?? projectNames.ShortName;
        }

        private static NuGetVersion GetPackageVersion(IVsTargetFrameworks tfms)
        {
            // $(PackageVersion) property if set overrides the $(Version)
            var versionPropertyValue =
                GetNonEvaluatedPropertyOrNull(tfms, PackageVersion, NuGetVersion.Parse)
                ?? GetNonEvaluatedPropertyOrNull(tfms, Version, NuGetVersion.Parse);

            return versionPropertyValue ?? PackageSpec.DefaultVersion;
        }

        // Trying to fetch a property value from tfm property bags.
        // If defined the property should have identical values in all of the occurances.
        private static TValue GetNonEvaluatedPropertyOrNull<TValue>(
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
                .Distinct()
                .SingleOrDefault();
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

            var ptf = GetPropertyValueOrNull(targetFrameworkInfo.Properties, PackageTargetFallback);
            if (!string.IsNullOrEmpty(ptf))
            {
                var fallbackList = MSBuildStringUtility.Split(ptf)
                    .Select(NuGetFramework.Parse)
                    .ToList();

                tfi.Imports = fallbackList;

                // Update the PackageSpec framework to include fallback frameworks
                if (tfi.Imports.Count != 0)
                {
                    tfi.FrameworkName = new FallbackFramework(tfi.FrameworkName, fallbackList);
                }
            }

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
                AutoReferenced = GetPropertyBoolOrFalse(item, "IsImplicitlyDefined")
            };

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
    }
}
