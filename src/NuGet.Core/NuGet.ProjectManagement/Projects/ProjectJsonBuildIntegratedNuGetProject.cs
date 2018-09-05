// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.ProjectManagement.Projects
{
    /// <summary>
    /// A NuGet integrated MSBuild project.k
    /// These projects contain a project.json
    /// </summary>
    [DebuggerDisplay("{ProjectName}")]
    public class ProjectJsonBuildIntegratedNuGetProject : BuildIntegratedNuGetProject
    {
        private readonly FileInfo _jsonConfig;
        private readonly string _projectName;

        // TODO: can this be removed?
        public ProjectJsonBuildIntegratedNuGetProject(
            string jsonConfig,
            string msBuildProjectPath,
            IMSBuildNuGetProjectSystem projectSystem)
            : this(jsonConfig, msBuildProjectPath)
        {

        }

        /// <summary>
        /// Project.json based project system.
        /// </summary>
        /// <param name="jsonConfig">Path to project.json.</param>
        /// <param name="msBuildProjectPath">Path to the msbuild project file.</param>
        public ProjectJsonBuildIntegratedNuGetProject(
            string jsonConfig,
            string msBuildProjectPath)
        {
            if (jsonConfig == null)
            {
                throw new ArgumentNullException(nameof(jsonConfig));
            }

            if (msBuildProjectPath == null)
            {
                throw new ArgumentNullException(nameof(msBuildProjectPath));
            }

            _jsonConfig = new FileInfo(jsonConfig);

            MSBuildProjectPath = msBuildProjectPath;

            _projectName = Path.GetFileNameWithoutExtension(msBuildProjectPath);

            if (string.IsNullOrEmpty(_projectName))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, Strings.InvalidProjectName, MSBuildProjectPath));
            }

            JObject projectJson;
            IEnumerable<NuGetFramework> targetFrameworks = Enumerable.Empty<NuGetFramework>();

            try
            {
                projectJson = GetJson();
                targetFrameworks = JsonConfigUtility.GetFrameworks(projectJson);
            }
            catch (InvalidOperationException)
            {
                // Ignore a bad project.json when constructing the project, and treat it as unsupported.
            }

            // Default to unsupported if anything unexpected is returned
            var targetFramework = NuGetFramework.UnsupportedFramework;

            // Having more than one framework is not supported, but we pick the first as fallback
            // We will eventually support more than one framework ala projectK.
            if (targetFrameworks.Count() == 1)
            {
                targetFramework = targetFrameworks.First();
            }

            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, targetFramework);
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, msBuildProjectPath);

            var supported = new List<FrameworkName>
            {
                new FrameworkName(targetFramework.DotNetFrameworkName)
            };

            InternalMetadata.Add(NuGetProjectMetadataKeys.SupportedFrameworks, supported);
        }

        public override Task<string> GetAssetsFilePathAsync()
        {
            return Task.FromResult(ProjectJsonPathUtilities.GetLockFilePath(JsonConfigPath));
        }

        /// <summary>
        /// project.json path
        /// </summary>
        public string JsonConfigPath
        {
            get { return _jsonConfig.FullName; }
        }

        /// <summary>
        /// Parsed project.json file
        /// </summary>
        public PackageSpec JsonPackageSpec
        {
            get
            {
                return JsonPackageSpecReader.GetPackageSpec(ProjectName, JsonConfigPath);
            }
        }

        public override string MSBuildProjectPath { get; }
        /// <summary>
        /// Project name
        /// </summary>
        public override string ProjectName
        {
            get
            {
                return _projectName;
            }
        }

        /// <summary>
        /// Script executor hook
        /// </summary>
        public override Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            bool throwOnFailure)
        {
            return Task.FromResult(false);
        }

        public virtual Task<IReadOnlyList<ProjectRestoreReference>> GetDirectProjectReferencesAsync(
            DependencyGraphCacheContext context)
        {
            return Task.FromResult<IReadOnlyList<ProjectRestoreReference>>(
                Enumerable.Empty<ProjectRestoreReference>().ToList());
        }

        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            var packages = new List<PackageReference>();

            //  Find all dependencies and convert them into packages.config style references
            foreach (var dependency in JsonConfigUtility.GetDependencies(await GetJsonAsync()))
            {
                // Use the minimum version of the range for the identity
                var identity = new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion);

                // Pass the actual version range as the allowed range
                packages.Add(new PackageReference(identity,
                    targetFramework: null,
                    userInstalled: true,
                    developmentDependency: false,
                    requireReinstallation: false,
                    allowedVersions: dependency.VersionRange));
            }

            return packages;
        }

        public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
        {
            PackageSpec packageSpec = null;
            if (context == null || !context.PackageSpecCache.TryGetValue(MSBuildProjectPath, out packageSpec))
            {
                packageSpec = JsonPackageSpec;
                if (packageSpec == null)
                {
                    throw new InvalidOperationException(
                        string.Format(Strings.ProjectNotLoaded_RestoreFailed, ProjectName));
                }
                var metadata = new ProjectRestoreMetadata();
                packageSpec.RestoreMetadata = metadata;

                metadata.ProjectStyle = ProjectStyle.ProjectJson;
                metadata.ProjectPath = MSBuildProjectPath;
                metadata.ProjectJsonPath = packageSpec.FilePath;
                metadata.ProjectName = packageSpec.Name;
                metadata.ProjectUniqueName = MSBuildProjectPath;

                var references = await GetDirectProjectReferencesAsync(context);
                if (references != null && references.Count > 0)
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

                context?.PackageSpecCache.Add(MSBuildProjectPath, packageSpec);
            }

            return new[] { packageSpec };
        }

        public async override Task<bool> InstallPackageAsync(
            string packageId,
            VersionRange range,
            INuGetProjectContext nuGetProjectContext,
            BuildIntegratedInstallationContext installationContext,
            CancellationToken token)
        {
            var dependency = new PackageDependency(packageId, range);

            var json = await GetJsonAsync();

            JsonConfigUtility.AddDependency(json, dependency);

            await SaveJsonAsync(json);

            return true;
        }

        /// <summary>
        /// Uninstall a package from the config file.
        /// </summary>
        public async Task<bool> RemoveDependencyAsync(string packageId,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var json = await GetJsonAsync();

            JsonConfigUtility.RemoveDependency(json, packageId);

            await SaveJsonAsync(json);

            return true;
        }

        public override async Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            return await RemoveDependencyAsync(packageIdentity.Id, nuGetProjectContext, token);
        }
        private JObject GetJson()
        {
            try
            {
                using (var streamReader = new StreamReader(_jsonConfig.OpenRead()))
                {
                    return JObject.Parse(streamReader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format(Strings.ErrorLoadingPackagesConfig, _jsonConfig.FullName, ex.Message), ex);
            }
        }

        private async Task<JObject> GetJsonAsync()
        {
            try
            {
                using (var streamReader = new StreamReader(_jsonConfig.OpenRead()))
                {
                    return JObject.Parse(await streamReader.ReadToEndAsync());
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format(Strings.ErrorLoadingPackagesConfig, _jsonConfig.FullName, ex.Message), ex);
            }
        }
        private async Task SaveJsonAsync(JObject json)
        {
            using (var writer = new StreamWriter(_jsonConfig.FullName, false, Encoding.UTF8))
            {
                await writer.WriteAsync(json.ToString());
            }
        }
    }
}
