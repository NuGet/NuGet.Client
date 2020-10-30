// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace NuGet.Commands.Test
{
    public static class ProjectJsonTestHelpers
    {
        /// <summary>
        /// Create a restore request for the specs. Restore only the first one.
        /// </summary>
        public static async Task<RestoreRequest> GetRequestAsync(
            RestoreArgs restoreContext,
            params PackageSpec[] projects)
        {
            var dgSpec = GetDGSpec(projects);

            var dgProvider = new DependencyGraphSpecRequestProvider(
                new RestoreCommandProvidersCache(),
                dgSpec);

            var requests = await dgProvider.CreateRequests(restoreContext);
            return requests.Single().Request;
        }

        /// <summary>
        /// Create a dg file for the specs. Restore only the first one.
        /// </summary>
        public static DependencyGraphSpec GetDGSpec(params PackageSpec[] projects)
        {
            var dgSpec = new DependencyGraphSpec();

            var project = EnsureRestoreMetadata(projects.First());

            dgSpec.AddProject(project);
            dgSpec.AddRestore(project.RestoreMetadata.ProjectUniqueName);

            foreach (var child in projects.Skip(1))
            {
                dgSpec.AddProject(EnsureRestoreMetadata(child));
            }

            return dgSpec;
        }

        /// <summary>
        /// Creates a dg specs with all PackageReference and project.json projects to be restored.
        /// </summary>
        /// <param name="projects"></param>
        /// <returns></returns>
        public static DependencyGraphSpec GetDGSpecFromPackageSpecs(params PackageSpec[] projects)
        {
            var dgSpec = new DependencyGraphSpec();
            foreach (var project in projects)
            {
                dgSpec.AddProject(project);
                if (project.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference ||
                    project.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson)
                {
                    dgSpec.AddRestore(project.RestoreMetadata.ProjectUniqueName);
                }
            }
            return dgSpec;
        }

        /// <summary>
        /// Add restore metadata only if not already set.
        /// Sets the project style to PackageReference.
        /// </summary>
        public static PackageSpec EnsureRestoreMetadata(this PackageSpec spec)
        {
            if (string.IsNullOrEmpty(spec.RestoreMetadata?.ProjectUniqueName))
            {
                return spec.WithTestRestoreMetadata();
            }

            return spec;
        }

        /// <summary>
        /// Add restore metadata only if not already set.
        /// Sets the project style to PackageReference.
        /// </summary>
        public static PackageSpec EnsureProjectJsonRestoreMetadata(this PackageSpec spec)
        {
            if (string.IsNullOrEmpty(spec.RestoreMetadata?.ProjectUniqueName))
            {
                return spec.WithProjectJsonTestRestoreMetadata();
            }

            return spec;
        }

        public static PackageSpec WithTestProjectReference(this PackageSpec parent, PackageSpec child, params NuGetFramework[] frameworks)
        {
            var spec = parent.Clone();

            if (frameworks.Length == 0)
            {
                // Use all frameworks if none were given
                frameworks = spec.TargetFrameworks.Select(e => e.FrameworkName).ToArray();
            }

            foreach (var framework in spec
                .RestoreMetadata
                .TargetFrameworks
                .Where(e => frameworks.Contains(e.FrameworkName)))
            {
                framework.ProjectReferences.Add(new ProjectRestoreReference()
                {
                    ProjectUniqueName = child.RestoreMetadata.ProjectUniqueName,
                    ProjectPath = child.RestoreMetadata.ProjectPath
                });
            }

            return spec;
        }

        /// <summary>
        /// Add fake PackageReference restore metadata.
        /// This resembles the .NET Core based projects (<see cref="ProjectRestoreSettings"/>.
        /// </summary>
        public static PackageSpec WithTestRestoreMetadata(this PackageSpec spec)
        {
            var updated = spec.Clone();
            var packageSpecFile = new FileInfo(spec.FilePath);
            var projectDir = packageSpecFile.Directory.FullName;

            var projectPath = Path.Combine(projectDir, spec.Name + ".csproj");
            updated.FilePath = projectPath;

            updated.RestoreMetadata = new ProjectRestoreMetadata();
            updated.RestoreMetadata.CrossTargeting = updated.TargetFrameworks.Count > 1;
            updated.RestoreMetadata.OriginalTargetFrameworks = updated.TargetFrameworks.Select(e => e.FrameworkName.GetShortFolderName()).ToList();
            updated.RestoreMetadata.OutputPath = projectDir;
            updated.RestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
            updated.RestoreMetadata.ProjectName = spec.Name;
            updated.RestoreMetadata.ProjectUniqueName = projectPath;
            updated.RestoreMetadata.ProjectPath = projectPath;
            updated.RestoreMetadata.ConfigFilePaths = new List<string>();
            updated.RestoreMetadata.CentralPackageVersionsEnabled = spec.RestoreMetadata?.CentralPackageVersionsEnabled ?? false;

            // Update the Target Alias.
            foreach (var framework in updated.TargetFrameworks)
            {
                if (string.IsNullOrEmpty(framework.TargetAlias))
                {
                    framework.TargetAlias = framework.FrameworkName.GetShortFolderName();
                }
            }
            foreach (var framework in updated.TargetFrameworks)
            {
                updated.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(framework.FrameworkName) { TargetAlias = framework.TargetAlias });
            }
            return updated;
        }

        private static PackageSpec WithProjectJsonTestRestoreMetadata(this PackageSpec spec)
        {
            var updated = spec.Clone();
            var metadata = new ProjectRestoreMetadata();
            updated.RestoreMetadata = metadata;

            var msbuildProjectFilePath = Path.Combine(Path.GetDirectoryName(spec.FilePath), spec.Name + ".csproj");
            var msbuildProjectExtensionsPath = Path.Combine(Path.GetDirectoryName(spec.FilePath), "obj");
            metadata.ProjectStyle = ProjectStyle.ProjectJson;
            metadata.OutputPath = msbuildProjectExtensionsPath;
            metadata.ProjectPath = msbuildProjectFilePath;
            metadata.ProjectJsonPath = spec.FilePath;
            metadata.ProjectName = spec.Name;
            metadata.ProjectUniqueName = msbuildProjectFilePath;
            metadata.CacheFilePath = NoOpRestoreUtilities.GetProjectCacheFilePath(msbuildProjectExtensionsPath);
            metadata.ConfigFilePaths = new List<string>();

            foreach (var framework in updated.TargetFrameworks)
            {
                metadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(framework.FrameworkName) { });
            }

            return updated;
        }
    }
}
