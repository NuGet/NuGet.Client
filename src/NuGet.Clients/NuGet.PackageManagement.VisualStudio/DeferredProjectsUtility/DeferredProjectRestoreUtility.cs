// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace.Extensions.MSBuild;
using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.LibraryModel;
using NuGet.Versioning;
using NuGet.Configuration;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Utility class to construct restore data for deferred projects.
    /// </summary>
    public static class DeferredProjectRestoreUtility
    {
        private static readonly string IncludeAssets = "IncludeAssets";
        private static readonly string ExcludeAssets = "ExcludeAssets";
        private static readonly string PrivateAssets = "PrivateAssets";
        private static readonly string BaseIntermediateOutputPath = "BaseIntermediateOutputPath";
        private static readonly string PackageReference = "PackageReference";
        private static readonly string ProjectReference = "ProjectReference";
        private static readonly string RuntimeIdentifier = "RuntimeIdentifier";
        private static readonly string RuntimeIdentifiers = "RuntimeIdentifiers";
        private static readonly string RuntimeSupports = "RuntimeSupports";
        private static readonly string TargetFramework = "TargetFramework";
        private static readonly string TargetFrameworks = "TargetFrameworks";
        private static readonly string PackageTargetFallback = "PackageTargetFallback";
        private static readonly string TargetPlatformIdentifier = "TargetPlatformIdentifier";
        private static readonly string TargetPlatformVersion = "TargetPlatformVersion";
        private static readonly string TargetPlatformMinVersion = "TargetPlatformMinVersion";
        private static readonly string TargetFrameworkMoniker = "TargetFrameworkMoniker";
        private static readonly string RestorePackagesPath = "RestorePackagesPath";
        private static readonly string AssetTargetFallback = "AssetTargetFallback";
        private static readonly string RestoreSources = "RestoreSources";
        private static readonly string RestoreFallbackFolders = "RestoreFallbackFolders";
        private static readonly string RestoreAdditionalProjectSources = nameof(RestoreAdditionalProjectSources);
        private static readonly string RestoreAdditionalProjectFallbackFolders = nameof(RestoreAdditionalProjectFallbackFolders);
        private static readonly string NoWarn = nameof(NoWarn);
        private static readonly string WarningsAsErrors = nameof(WarningsAsErrors);
        private static readonly string TreatWarningsAsErrors = nameof(TreatWarningsAsErrors);

        public static async Task<DeferredProjectRestoreData> GetDeferredProjectsData(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            IEnumerable<string> deferredProjectsPath,
            ISettings settings,
            CancellationToken token)
        {
            var packageReferencesDict = new Dictionary<PackageReference, List<string>>(new PackageReferenceComparer());
            var packageSpecs = new List<PackageSpec>();

            foreach (var projectPath in deferredProjectsPath)
            {
                // packages.config
                var packagesConfigFilePath = Path.Combine(Path.GetDirectoryName(projectPath), "packages.config");
                var packagesConfigFileExists = await lightWeightProjectWorkspaceService.EntityExists(packagesConfigFilePath);

                if (packagesConfigFileExists)
                {
                    // read packages.config and get all package references.
                    var projectName = Path.GetFileNameWithoutExtension(projectPath);
                    using (var stream = new FileStream(packagesConfigFilePath, FileMode.Open, FileAccess.Read))
                    {
                        var reader = new PackagesConfigReader(stream);
                        var packageReferences = reader.GetPackages();

                        foreach (var packageRef in packageReferences)
                        {
                            List<string> projectNames = null;
                            if (!packageReferencesDict.TryGetValue(packageRef, out projectNames))
                            {
                                projectNames = new List<string>();
                                packageReferencesDict.Add(packageRef, projectNames);
                            }

                            projectNames.Add(projectName);
                        }
                    }

                    // create package spec for packages.config based project
                    var packageSpec = await GetPackageSpecForPackagesConfigAsync(lightWeightProjectWorkspaceService, projectPath);
                    if (packageSpec != null)
                    {
                        packageSpecs.Add(packageSpec);
                    }
                }
                else
                {

                    // project.json
                    var projectJsonFilePath = Path.Combine(Path.GetDirectoryName(projectPath), "project.json");
                    var projectJsonFileExists = await lightWeightProjectWorkspaceService.EntityExists(projectJsonFilePath);

                    if (projectJsonFileExists)
                    {
                        // create package spec for project.json based project
                        var packageSpec = await GetPackageSpecForProjectJsonAsync(lightWeightProjectWorkspaceService, projectPath, projectJsonFilePath, settings);
                        packageSpecs.Add(packageSpec);
                    }
                    else
                    {
                        // package references (CPS or Legacy CSProj)
                        var packageSpec = await GetPackageSpecForPackageReferencesAsync(lightWeightProjectWorkspaceService, projectPath, settings);
                        if (packageSpec != null)
                        {
                            packageSpecs.Add(packageSpec);
                        }
                    }
                }
            }

            return new DeferredProjectRestoreData(packageReferencesDict, packageSpecs);
        }

        private static async Task<PackageSpec> GetPackageSpecForPackagesConfigAsync(ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService, string projectPath)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var msbuildProjectDataService = await lightWeightProjectWorkspaceService.GetMSBuildProjectDataService(projectPath);

            var nuGetFramework = await GetNuGetFramework(lightWeightProjectWorkspaceService, msbuildProjectDataService, projectPath);

            var packageSpec = new PackageSpec(
                new List<TargetFrameworkInformation>
                {
                    new TargetFrameworkInformation
                    {
                        FrameworkName = nuGetFramework
                    }
                });

            packageSpec.Name = projectName;
            packageSpec.FilePath = projectPath;

            var metadata = new ProjectRestoreMetadata();
            packageSpec.RestoreMetadata = metadata;

            metadata.ProjectStyle = ProjectStyle.PackagesConfig;
            metadata.ProjectPath = projectPath;
            metadata.ProjectName = projectName;
            metadata.ProjectUniqueName = projectPath;
            metadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(nuGetFramework));

            await AddProjectReferencesAsync(lightWeightProjectWorkspaceService, metadata, packageSpec, projectPath);

            return packageSpec;
        }

        private static async Task<PackageSpec> GetPackageSpecForProjectJsonAsync(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            string projectPath,
            string projectJsonFilePath,
            ISettings settings)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var packageSpec = JsonPackageSpecReader.GetPackageSpec(projectName, projectJsonFilePath);

            var msbuildProjectDataService = await lightWeightProjectWorkspaceService.GetMSBuildProjectDataService(projectPath);
            var projectDirectory = Path.GetDirectoryName(projectPath);

            var outputPath = Path.GetFullPath(
                Path.Combine(
                    projectDirectory,
                    await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, BaseIntermediateOutputPath)));

            var metadata = new ProjectRestoreMetadata();
            packageSpec.RestoreMetadata = metadata;

            metadata.ProjectStyle = ProjectStyle.ProjectJson;
            metadata.OutputPath = outputPath;
            metadata.ProjectPath = projectPath;
            metadata.ProjectJsonPath = packageSpec.FilePath;
            metadata.ProjectName = packageSpec.Name;
            metadata.ProjectUniqueName = projectPath;
            metadata.CacheFilePath = GetCacheFilePath(projectPath, outputPath);

            foreach (var framework in packageSpec.TargetFrameworks.Select(e => e.FrameworkName))
            {
                metadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(framework));
            }

            await AddProjectReferencesAsync(lightWeightProjectWorkspaceService, metadata, packageSpec, projectPath);

            // Write restore settings to the package spec.
            // For project.json these properties may not come from the project file.
            settings = settings ?? NullSettings.Instance;
            packageSpec.RestoreMetadata.PackagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);
            packageSpec.RestoreMetadata.Sources = SettingsUtility.GetEnabledSources(settings).ToList();
            packageSpec.RestoreMetadata.FallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings).ToList();
            packageSpec.RestoreMetadata.ConfigFilePaths = SettingsUtility.GetConfigFilePaths(settings).ToList();

            return packageSpec;
        }

        private static async Task<PackageSpec> GetPackageSpecForPackageReferencesAsync(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            string projectPath,
            ISettings settings)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectDirectory = Path.GetDirectoryName(projectPath);

            var msbuildProjectDataService = await lightWeightProjectWorkspaceService.GetMSBuildProjectDataService(projectPath);

            var packageReferences = (await lightWeightProjectWorkspaceService.GetProjectItemsAsync(msbuildProjectDataService, PackageReference)).ToList();
            if (packageReferences.Count == 0)
            {
                return null;
            }

            var targetFrameworks = await GetNuGetFrameworks(lightWeightProjectWorkspaceService, msbuildProjectDataService, projectPath);
            if (targetFrameworks.Count == 0)
            {
                return null;
            }

            var outputPath = Path.GetFullPath(
                Path.Combine(
                    projectDirectory,
                    await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, BaseIntermediateOutputPath)));

            var restorePackagesPath = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, RestorePackagesPath);

            var restoreSources = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, RestoreSources);

            var restoreAdditionalProjectSources = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, AssetTargetFallback);

            var restoreFallbackFolders = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, RestoreFallbackFolders);

            var restoreAdditionalProjectFallbackFolders = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, RestoreAdditionalProjectFallbackFolders);

            var treatWarningsAsErrors = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, TreatWarningsAsErrors);

            var noWarn = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, NoWarn);

            var warningsAsErrors = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, msbuildProjectDataService, WarningsAsErrors);

            var crossTargeting = targetFrameworks.Count > 1;

            var tfis = new List<TargetFrameworkInformation>();
            var projectsByFramework = new Dictionary<NuGetFramework, IEnumerable<ProjectRestoreReference>>();
            var runtimes = new List<string>();
            var supports = new List<string>();

            foreach (var targetFramework in targetFrameworks)
            {
                var tfi = new TargetFrameworkInformation
                {
                    FrameworkName = targetFramework
                };

                // re-evaluate msbuild project data service if there are multi-targets
                if (targetFrameworks.Count > 1)
                {
                    msbuildProjectDataService = await lightWeightProjectWorkspaceService.GetMSBuildProjectDataService(
                        projectPath,
                        targetFramework.GetShortFolderName());

                    packageReferences = (await lightWeightProjectWorkspaceService.GetProjectItemsAsync(msbuildProjectDataService, PackageReference)).ToList();
                }

                // Package target fallback per target framework
                var ptf = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(msbuildProjectDataService, PackageTargetFallback);
                if (!string.IsNullOrEmpty(ptf))
                {
                    var fallBackList = ptf.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(NuGetFramework.Parse).ToList();

                    if (fallBackList.Count > 0)
                    {
                        tfi.FrameworkName = new FallbackFramework(tfi.FrameworkName, fallBackList);
                    }

                    tfi.Imports = fallBackList;
                }

                // package references per target framework
                tfi.Dependencies.AddRange(
                    packageReferences.Select(ToPackageLibraryDependency));

                // Project references per target framework
                var projectReferences = (await lightWeightProjectWorkspaceService.GetProjectItemsAsync(msbuildProjectDataService, ProjectReference))
                    .Select(item => ToProjectRestoreReference(item, projectDirectory));
                projectsByFramework.Add(tfi.FrameworkName, projectReferences);

                // Runtimes, Supports per target framework
                runtimes.AddRange(await GetRuntimeIdentifiers(lightWeightProjectWorkspaceService, msbuildProjectDataService));
                supports.AddRange(await GetRuntimeSupports(lightWeightProjectWorkspaceService, msbuildProjectDataService));

                tfis.Add(tfi);
            }

            var packageSpec = new PackageSpec(tfis)
            {
                Name = projectName,
                FilePath = projectPath,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    ProjectName = projectName,
                    ProjectUniqueName = projectPath,
                    ProjectPath = projectPath,
                    OutputPath = outputPath,
                    ProjectStyle = ProjectStyle.PackageReference,
                    TargetFrameworks = (projectsByFramework.Select(
                        kvp => new ProjectRestoreMetadataFrameworkInfo(kvp.Key)
                        {
                            ProjectReferences = kvp.Value?.ToList()
                        }
                    )).ToList(),
                    OriginalTargetFrameworks = tfis
                        .Select(tf => tf.FrameworkName.GetShortFolderName())
                        .ToList(),
                    CrossTargeting = crossTargeting,
                    SkipContentFileWrite = true,
                    CacheFilePath = GetCacheFilePath(projectPath, outputPath),
                    PackagesPath = GetPackagesPath(projectPath, restorePackagesPath, settings),
                    Sources = GetSources(restoreSources, restoreAdditionalProjectSources, projectPath, settings),
                    FallbackFolders = GetFallbackFolders(restoreFallbackFolders, restoreAdditionalProjectFallbackFolders, projectPath, settings),
                    ConfigFilePaths = GetConfigFilePaths(settings),
                    ProjectWideWarningProperties = MSBuildRestoreUtility.GetWarningProperties(
                        treatWarningsAsErrors: treatWarningsAsErrors,
                        noWarn: noWarn,
                        warningsAsErrors: warningsAsErrors)
                },
                RuntimeGraph = new RuntimeGraph(
                    runtimes.Distinct(StringComparer.Ordinal).Select(rid => new RuntimeDescription(rid)),
                    supports.Distinct(StringComparer.Ordinal).Select(s => new CompatibilityProfile(s)))
            };

            return packageSpec;
        }

        private static IList<string> GetConfigFilePaths(ISettings settings)
        {
            return SettingsUtility.GetConfigFilePaths(settings).ToList();
        }

        private static IList<string> GetFallbackFolders(string restoreFallbackFolders, string restoreAdditionalProjectFallbackFolders, string projectPath, ISettings settings)
        {
            var fallbackFolders = MSBuildStringUtility.Split(restoreFallbackFolders).AsEnumerable();

            if (ShouldReadFromSettings(fallbackFolders))
            {
                fallbackFolders = SettingsUtility.GetFallbackPackageFolders(settings);
            }
            else
            {
                fallbackFolders = VSRestoreSettingsUtilities.HandleClear(fallbackFolders);
            }

            // Add additional fallback folders
            fallbackFolders = fallbackFolders.Concat(MSBuildStringUtility.Split(restoreAdditionalProjectFallbackFolders));

            return fallbackFolders.Select(e => UriUtility.GetAbsolutePathFromFile(projectPath, e)).ToList();
        }

        private static IList<PackageSource> GetSources(string restoreSources, string restoreAdditionalProjectSources, string projectPath, ISettings settings, bool shouldThrow = true)
        {
            var sources = MSBuildStringUtility.Split(restoreSources).AsEnumerable();

            if (ShouldReadFromSettings(sources))
            {
                sources = SettingsUtility.GetEnabledSources(settings).Select(e => e.Source);
            }
            else
            {
                sources = VSRestoreSettingsUtilities.HandleClear(sources);
            }

            // Add additional sources
            sources = sources.Concat(MSBuildStringUtility.Split(restoreAdditionalProjectSources));

            return sources.Select(e => new PackageSource(UriUtility.GetAbsolutePathFromFile(projectPath, e))).ToList();
        }

        private static bool ShouldReadFromSettings(IEnumerable<string> values)
        {
            return !values.Any() && values.All(e => !StringComparer.OrdinalIgnoreCase.Equals("CLEAR", e));
        }

        private static string GetCacheFilePath(string projectPath, string outputPath)
        {
            return NoOpRestoreUtilities.GetProjectCacheFilePath(cacheRoot: outputPath, projectPath: projectPath);
        }

        private static string GetPackagesPath(string projectPath, string restorePackagesPath, ISettings settings)
        {
            if (string.IsNullOrEmpty(restorePackagesPath))
            {
                return SettingsUtility.GetGlobalPackagesFolder(settings);
            }

            return UriUtility.GetAbsolutePathFromFile(projectPath, restorePackagesPath);
        }

        private static async Task<List<string>> GetRuntimeIdentifiers(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            IMSBuildProjectDataService dataService)
        {
            var runtimeIdentifier = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, dataService, RuntimeIdentifier);
            var runtimeIdentifiers = await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, dataService, RuntimeIdentifiers);

            var runtimes = (new[] { runtimeIdentifier, runtimeIdentifiers })
                .SelectMany(s => s.Split(';'))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return runtimes;
        }

        private static async Task<List<string>> GetRuntimeSupports(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            IMSBuildProjectDataService dataService)
        {
            var supports = (await GetProjectPropertyOrDefault(lightWeightProjectWorkspaceService, dataService, RuntimeSupports))
                .Split(';')
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return supports;
        }

        private static async Task<string> GetProjectPropertyOrDefault(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            IMSBuildProjectDataService dataService,
            string projectPropertyName,
            string defaultValue = "")
        {
            var propertyValue = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(dataService, projectPropertyName);

            if (!string.IsNullOrEmpty(propertyValue))
            {
                return propertyValue;
            }

            return defaultValue;
        }

        private static ProjectRestoreReference ToProjectRestoreReference(MSBuildProjectItemData item, string projectDirectory)
        {
            var referencePath = Path.GetFullPath(
                Path.Combine(
                    projectDirectory, item.EvaluatedInclude));

            var reference = new ProjectRestoreReference()
            {
                ProjectUniqueName = referencePath,
                ProjectPath = referencePath
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                reference,
                includeAssets: GetPropertyValueOrDefault(item, IncludeAssets),
                excludeAssets: GetPropertyValueOrDefault(item, ExcludeAssets),
                privateAssets: GetPropertyValueOrDefault(item, PrivateAssets));

            return reference;
        }

        private static LibraryDependency ToPackageLibraryDependency(MSBuildProjectItemData item)
        {
            var dependency = new LibraryDependency
            {
                LibraryRange = new LibraryRange(
                    name: item.EvaluatedInclude,
                    versionRange: GetVersionRange(item),
                    typeConstraint: LibraryDependencyTarget.Package)
            };

            MSBuildRestoreUtility.ApplyIncludeFlags(
                dependency,
                includeAssets: GetPropertyValueOrDefault(item, IncludeAssets),
                excludeAssets: GetPropertyValueOrDefault(item, ExcludeAssets),
                privateAssets: GetPropertyValueOrDefault(item, PrivateAssets));

            return dependency;
        }

        private static VersionRange GetVersionRange(MSBuildProjectItemData item)
        {
            var versionRange = GetPropertyValueOrDefault(item, "Version");

            if (!string.IsNullOrEmpty(versionRange))
            {
                return VersionRange.Parse(versionRange);
            }

            return VersionRange.All;
        }

        private static string GetPropertyValueOrDefault(
            MSBuildProjectItemData item, string propertyName, string defaultValue = "")
        {
            if (item.Metadata.Keys.Contains(propertyName))
            {
                return item.Metadata[propertyName];
            }

            return defaultValue;
        }

        private static async Task<List<NuGetFramework>> GetNuGetFrameworks(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            IMSBuildProjectDataService dataService,
            string projectPath)
        {
            var targetFrameworks = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(dataService, TargetFrameworks);
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                return targetFrameworks
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(NuGetFramework.Parse).ToList();
            }

            var targetFramework = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(dataService, TargetFramework);
            if (!string.IsNullOrEmpty(targetFramework))
            {
                return new List<NuGetFramework> { NuGetFramework.Parse(targetFramework) };
            }

            // old packages.config style or legacy PackageRef
            return new List<NuGetFramework> { await GetNuGetFramework(lightWeightProjectWorkspaceService, dataService, projectPath) };
        }

        private static async Task<NuGetFramework> GetNuGetFramework(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            IMSBuildProjectDataService dataService,
            string projectPath)
        {
            var targetPlatformIdentifier = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(dataService, TargetPlatformIdentifier);
            var targetPlatformVersion = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(dataService, TargetPlatformVersion);
            var targetPlatformMinVersion = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(dataService, TargetPlatformMinVersion);
            var targetFrameworkMoniker = await lightWeightProjectWorkspaceService.GetProjectPropertyAsync(dataService, TargetFrameworkMoniker);

            var frameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                projectFilePath: projectPath,
                targetFrameworks: null,
                targetFramework: null,
                targetFrameworkMoniker: targetFrameworkMoniker,
                targetPlatformIdentifier: targetPlatformIdentifier,
                targetPlatformVersion: targetPlatformVersion,
                targetPlatformMinVersion: targetPlatformMinVersion);

            var frameworkString = frameworkStrings.FirstOrDefault();

            if (!string.IsNullOrEmpty(frameworkString))
            {
                return NuGetFramework.Parse(frameworkString);
            }

            return NuGetFramework.UnsupportedFramework;
        }

        private static async Task AddProjectReferencesAsync(
            ILightWeightProjectWorkspaceService lightWeightProjectWorkspaceService,
            ProjectRestoreMetadata metadata,
            PackageSpec packageSpec,
            string projectPath)
        {
            var references = (await lightWeightProjectWorkspaceService.GetProjectReferencesAsync(projectPath)).Select(reference => new ProjectRestoreReference
            {
                ProjectPath = reference,
                ProjectUniqueName = reference
            }).ToList();

            if (references != null && references.Any())
            {
                // Add msbuild reference groups for each TFM in the project
                foreach (var framework in packageSpec.TargetFrameworks.Select(e => e.FrameworkName))
                {
                    metadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(framework));
                }

                foreach (var reference in references)
                {
                    // This reference applies to all frameworks
                    // Include/exclude flags may be applied later when merged with project.json
                    // Add the reference for all TFM groups, there are no conditional project
                    // references in UWP. There should also be just one TFM.
                    foreach (var frameworkInfo in metadata.TargetFrameworks)
                    {
                        frameworkInfo.ProjectReferences.Add(reference);
                    }
                }
            }
        }
    }
}
