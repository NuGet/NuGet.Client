﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using NuGet.LibraryModel;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.VisualStudio
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

        public static async Task<DeferredProjectRestoreData> GetDeferredProjectsData(
            IDeferredProjectWorkspaceService deferredWorkspaceService,
            IEnumerable<string> deferredProjectsPath,
            CancellationToken token)
        {
            var packageReferencesDict = new Dictionary<PackageReference, List<string>>(new PackageReferenceComparer());
            var packageSpecs = new List<PackageSpec>();

            foreach (var projectPath in deferredProjectsPath)
            {
                // packages.config
                var packagesConfigFilePath = Path.Combine(Path.GetDirectoryName(projectPath), "packages.config");
                var packagesConfigFileExists = await deferredWorkspaceService.EntityExists(packagesConfigFilePath);

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
                    var packageSpec = await GetPackageSpecForPackagesConfigAsync(deferredWorkspaceService, projectPath);
                    if (packageSpec != null)
                    {
                        packageSpecs.Add(packageSpec);
                    }
                }
                else
                {

                    // project.json
                    var projectJsonFilePath = Path.Combine(Path.GetDirectoryName(projectPath), "project.json");
                    var projectJsonFileExists = await deferredWorkspaceService.EntityExists(projectJsonFilePath);

                    if (projectJsonFileExists)
                    {
                        // create package spec for project.json based project
                        var packageSpec = await GetPackageSpecForProjectJsonAsync(deferredWorkspaceService, projectPath, projectJsonFilePath);
                        packageSpecs.Add(packageSpec);
                    }
                    else
                    {
                        // package references (CPS or Legacy CSProj)
                        var packageSpec = await GetPackageSpecForPackageReferencesAsync(deferredWorkspaceService, projectPath);
                        if (packageSpec != null)
                        {
                            packageSpecs.Add(packageSpec);
                        }
                    }
                }
            }

            return new DeferredProjectRestoreData(packageReferencesDict, packageSpecs);
        }

        private static async Task<PackageSpec> GetPackageSpecForPackagesConfigAsync(IDeferredProjectWorkspaceService deferredWorkspaceService, string projectPath)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            var msbuildProjectDataService = await deferredWorkspaceService.GetMSBuildProjectDataServiceAsync(projectPath);

            var nuGetFramework = await SolutionWorkspaceUtility.GetNuGetFrameworkAsync(msbuildProjectDataService, projectPath);

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

            await AddProjectReferencesAsync(deferredWorkspaceService, metadata, projectPath);

            return packageSpec;
        }

        private static async Task<PackageSpec> GetPackageSpecForProjectJsonAsync(
            IDeferredProjectWorkspaceService deferredWorkspaceService,
            string projectPath,
            string projectJsonFilePath)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var packageSpec = JsonPackageSpecReader.GetPackageSpec(projectName, projectJsonFilePath);

            var metadata = new ProjectRestoreMetadata();
            packageSpec.RestoreMetadata = metadata;

            metadata.ProjectStyle = ProjectStyle.ProjectJson;
            metadata.ProjectPath = projectPath;
            metadata.ProjectJsonPath = packageSpec.FilePath;
            metadata.ProjectName = packageSpec.Name;
            metadata.ProjectUniqueName = projectPath;

            foreach (var framework in packageSpec.TargetFrameworks.Select(e => e.FrameworkName))
            {
                metadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(framework));
            }

            await AddProjectReferencesAsync(deferredWorkspaceService, metadata, projectPath);

            return packageSpec;
        }

        private static async Task<PackageSpec> GetPackageSpecForPackageReferencesAsync(
            IDeferredProjectWorkspaceService deferredWorkspaceService,
            string projectPath)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectDirectory = Path.GetDirectoryName(projectPath);

            var msbuildProjectDataService = await deferredWorkspaceService.GetMSBuildProjectDataServiceAsync(projectPath);

            var packageReferences = (await msbuildProjectDataService.GetProjectItems(PackageReference)).ToList();
            if (packageReferences.Count == 0)
            {
                return null;
            }

            var targetFrameworks = await GetNuGetFrameworks(msbuildProjectDataService, projectPath);
            if (targetFrameworks.Count == 0)
            {
                return null;
            }

            var outputPath = Path.GetFullPath(
                Path.Combine(
                    projectDirectory,
                    await GetProjectPropertyOrDefault(deferredWorkspaceService, msbuildProjectDataService, BaseIntermediateOutputPath)));

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
                    msbuildProjectDataService = await deferredWorkspaceService.GetMSBuildProjectDataServiceAsync(
                        projectPath,
                        targetFramework.GetShortFolderName());

                    packageReferences = (await msbuildProjectDataService.GetProjectItems(PackageReference)).ToList();
                }

                // Package target fallback per target framework
                var ptf = await SolutionWorkspaceUtility.GetProjectPropertyAsync(msbuildProjectDataService, PackageTargetFallback);
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
                var projectReferences = (await msbuildProjectDataService.GetProjectItems(ProjectReference))
                    .Select(item => ToProjectRestoreReference(item, projectDirectory));
                projectsByFramework.Add(tfi.FrameworkName, projectReferences);

                // Runtimes, Supports per target framework
                runtimes.AddRange(await GetRuntimeIdentifiers(deferredWorkspaceService, msbuildProjectDataService));
                supports.AddRange(await GetRuntimeSupports(deferredWorkspaceService, msbuildProjectDataService));

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
                    CrossTargeting = crossTargeting
                },
                RuntimeGraph = new RuntimeGraph(
                    runtimes.Distinct(StringComparer.Ordinal).Select(rid => new RuntimeDescription(rid)),
                    supports.Distinct(StringComparer.Ordinal).Select(s => new CompatibilityProfile(s)))
            };

            return packageSpec;
        }

        private static async Task<List<string>> GetRuntimeIdentifiers(
            IDeferredProjectWorkspaceService deferredWorkspaceService,
            IMSBuildProjectDataService dataService)
        {
            var runtimeIdentifier = await GetProjectPropertyOrDefault(deferredWorkspaceService, dataService, RuntimeIdentifier);
            var runtimeIdentifiers = await GetProjectPropertyOrDefault(deferredWorkspaceService, dataService, RuntimeIdentifiers);

            var runtimes = (new[] { runtimeIdentifier, runtimeIdentifiers })
                .SelectMany(s => s.Split(';'))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return runtimes;
        }

        private static async Task<List<string>> GetRuntimeSupports(
            IDeferredProjectWorkspaceService deferredWorkspaceService,
            IMSBuildProjectDataService dataService)
        {
            var supports = (await GetProjectPropertyOrDefault(deferredWorkspaceService, dataService, RuntimeSupports))
                .Split(';')
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return supports;
        }

        private static async Task<string> GetProjectPropertyOrDefault(
            IDeferredProjectWorkspaceService deferredWorkspaceService,
            IMSBuildProjectDataService dataService,
            string projectPropertyName,
            string defaultValue = "")
        {
            var propertyValue = await SolutionWorkspaceUtility.GetProjectPropertyAsync(dataService, projectPropertyName);

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
            IMSBuildProjectDataService dataService,
            string projectPath)
        {
            var targetFrameworks = await SolutionWorkspaceUtility.GetProjectPropertyAsync(dataService, TargetFrameworks);
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                return targetFrameworks
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(NuGetFramework.Parse).ToList();
            }

            var targetFramework = await SolutionWorkspaceUtility.GetProjectPropertyAsync(dataService, TargetFramework);
            if (!string.IsNullOrEmpty(targetFramework))
            {
                return new List<NuGetFramework> { NuGetFramework.Parse(targetFramework) };
            }

            // old packages.config style or legacy PackageRef
            return new List<NuGetFramework>
            {
                await SolutionWorkspaceUtility.GetNuGetFrameworkAsync(dataService, projectPath)
            };
        }

        private static async Task AddProjectReferencesAsync(
            IDeferredProjectWorkspaceService deferredWorkspaceService,
            ProjectRestoreMetadata metadata,
            string projectPath)
        {
            var references = await deferredWorkspaceService.GetProjectReferencesAsync(projectPath);

            foreach (var reference in references)
            {
                var restoreReference = new ProjectRestoreReference()
                {
                    ProjectPath = reference,
                    ProjectUniqueName = reference
                };

                foreach (var frameworkInfo in metadata.TargetFrameworks)
                {
                    frameworkInfo.ProjectReferences.Add(restoreReference);
                }
            }
        }
    }
}