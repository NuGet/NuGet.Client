// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.ProjectManagement.Projects
{
    /// <summary>
    /// A NuGet integrated MSBuild project.k
    /// These projects contain a project.json
    /// </summary>
    public class BuildIntegratedNuGetProject : NuGetProject, INuGetIntegratedProject
    {
        private readonly FileInfo _jsonConfig;

        public BuildIntegratedNuGetProject(string jsonConfig, IMSBuildNuGetProjectSystem msbuildProjectSystem)
        {
            if (jsonConfig == null)
            {
                throw new ArgumentNullException(nameof(jsonConfig));
            }

            _jsonConfig = new FileInfo(jsonConfig);
            MSBuildNuGetProjectSystem = msbuildProjectSystem;

            var json = GetJson();

            var targetFrameworks = JsonConfigUtility.GetFrameworks(json);

            // Default to unsupported if anything unexpected is returned
            var targetFramework = NuGetFramework.UnsupportedFramework;

            Debug.Assert(targetFrameworks.Count() == 1, "Invalid target framework count");

            if (targetFrameworks.Count() == 1)
            {
                targetFramework = targetFrameworks.First();
            }

            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, targetFramework);
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, msbuildProjectSystem.ProjectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, msbuildProjectSystem.ProjectFullPath);

            var supported = new List<FrameworkName>
                {
                    new FrameworkName(targetFramework.DotNetFrameworkName)
                };

            InternalMetadata.Add(NuGetProjectMetadataKeys.SupportedFrameworks, supported);
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

        public override async Task<bool> InstallPackageAsync(
            PackageIdentity packageIdentity,
            DownloadResourceResult downloadResourceResult,
            INuGetProjectContext nuGetProjectContext,
            CancellationToken token)
        {
            var dependency = new PackageDependency(packageIdentity.Id, new VersionRange(packageIdentity.Version));

            return await AddDependency(dependency, token);
        }

        /// <summary>
        /// Retrieve the full closure of project to project references.
        /// </summary>
        public virtual Task<IReadOnlyList<BuildIntegratedProjectReference>> GetProjectReferenceClosureAsync()
        {
            // This cannot be resolved with DTE currently, it is overridden at a higher level
            return Task.FromResult<IReadOnlyList<BuildIntegratedProjectReference>>(
                Enumerable.Empty<BuildIntegratedProjectReference>().ToList());
        }

        /// <summary>
        /// Install a package using the global packages folder.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801")]
        public async Task<bool> AddDependency(PackageDependency dependency,
            CancellationToken token)
        {
            var json = await GetJsonAsync();

            JsonConfigUtility.AddDependency(json, dependency);

            await SaveJsonAsync(json);

            return true;
        }

        /// <summary>
        /// Uninstall a package from the config file.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801")]
        public async Task<bool> RemoveDependency(string packageId,
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
            return await RemoveDependency(packageIdentity.Id, nuGetProjectContext, token);
        }

        /// <summary>
        /// project.json path
        /// </summary>
        public virtual string JsonConfigPath
        {
            get { return _jsonConfig.FullName; }
        }

        /// <summary>
        /// Parsed project.json file
        /// </summary>
        public virtual PackageSpec PackageSpec
        {
            get
            {
                return JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(JsonConfigPath), ProjectName, JsonConfigPath);
            }
        }

        /// <summary>
        /// Project name
        /// </summary>
        public virtual string ProjectName
        {
            get
            {
                return MSBuildNuGetProjectSystem.ProjectName;
            }
        }

        /// <summary>
        /// The underlying msbuild project system
        /// </summary>
        public IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; }

        /// <summary>
        /// Script executor hook
        /// </summary>
        public virtual Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext, 
            bool throwOnFailure)
        {
            return Task.FromResult(false);
        }

        private async Task<JObject> GetJsonAsync()
        {
            using (var streamReader = new StreamReader(_jsonConfig.OpenRead()))
            {
                return JObject.Parse(await streamReader.ReadToEndAsync());
            }
        }

        private JObject GetJson()
        {
            using (var streamReader = new StreamReader(_jsonConfig.OpenRead()))
            {
                return JObject.Parse(streamReader.ReadToEnd());
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
