// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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
    public class BuildIntegratedNuGetProject : NuGetProject, INuGetIntegratedProject, IDependencyGraphProject
    {
        private readonly FileInfo _jsonConfig;
        private readonly string _projectName;
        
        public string MSBuildProjectPath { get; }

        /// <summary>
        /// Project.json based project system.
        /// </summary>
        /// <param name="jsonConfig">Path to project.json.</param>
        /// <param name="msBuildProjectPath">Path to the msbuild project file.</param>
        /// <param name="msbuildProjectSystem">Underlying msbuild project system.</param>
        public BuildIntegratedNuGetProject(
            string jsonConfig,
            string msBuildProjectPath,
            IMSBuildNuGetProjectSystem msbuildProjectSystem)
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
            MSBuildNuGetProjectSystem = msbuildProjectSystem;

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
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, msbuildProjectSystem.ProjectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, msbuildProjectSystem.ProjectFullPath);

            var supported = new List<FrameworkName>
            {
                new FrameworkName(targetFramework.DotNetFrameworkName)
            };

            InternalMetadata.Add(NuGetProjectMetadataKeys.SupportedFrameworks, supported);
        }

        public bool IsRestoreRequired(
            IEnumerable<VersionFolderPathResolver> pathResolvers,
            ISet<PackageIdentity> packagesChecked,
            ExternalProjectReferenceContext context)
        {
            var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(JsonConfigPath);

            if (!File.Exists(lockFilePath))
            {
                // If the lock file does not exist a restore is needed
                return true;
            }

            var lockFileFormat = new LockFileFormat();
            LockFile lockFile;
            try
            {
                lockFile = lockFileFormat.Read(lockFilePath, context.Logger);
            }
            catch
            {
                // If the lock file is invalid, then restore.
                return true;
            }
            
            var packageSpec = GetPackageSpecForRestore(context);

            if (!lockFile.IsValidForPackageSpec(packageSpec, LockFileFormat.Version))
            {
                // The project.json file has been changed and the lock file needs to be updated.
                return true;
            }

            // Verify all libraries are on disk
            var packages = lockFile.Libraries.Where(library => library.Type == LibraryType.Package);

            foreach (var library in packages)
            {
                var identity = new PackageIdentity(library.Name, library.Version);

                // Each id/version only needs to be checked once
                if (packagesChecked.Add(identity))
                {
                    var found = false;

                    //  Check each package folder. These need to match the order used for restore.
                    foreach (var resolver in pathResolvers)
                    {
                        // Verify the SHA for each package
                        var hashPath = resolver.GetHashPath(library.Name, library.Version);

                        if (File.Exists(hashPath))
                        {
                            found = true;
                            var sha512 = File.ReadAllText(hashPath);

                            if (library.Sha512 != sha512)
                            {
                                // A package has changed
                                return true;
                            }

                            // Skip checking the rest of the package folders
                            break;
                        }
                    }

                    if (!found)
                    {
                        // A package is missing
                        return true;
                    }
                }
            }

            return false;
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
        /// Warnings and errors encountered will be logged.
        /// </summary>
        public virtual Task<IReadOnlyList<ExternalProjectReference>> GetProjectReferenceClosureAsync(
            ExternalProjectReferenceContext context)
        {
            // This cannot be resolved with DTE currently, it is overridden at a higher level
            return Task.FromResult<IReadOnlyList<ExternalProjectReference>>(
                Enumerable.Empty<ExternalProjectReference>().ToList());
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
                return JsonPackageSpecReader.GetPackageSpec(ProjectName, JsonConfigPath);
            }
        }

        public PackageSpec GetPackageSpecForRestore(ExternalProjectReferenceContext referenceContext)
        {
            var packageSpec = PackageSpec;

            var metadata = new ProjectRestoreMetadata();
            packageSpec.RestoreMetadata = metadata;

            metadata.OutputType = RestoreOutputType.UAP;
            metadata.ProjectPath = MSBuildProjectPath;
            metadata.ProjectJsonPath = packageSpec.FilePath;
            metadata.ProjectName = packageSpec.Name;
            metadata.ProjectUniqueName = MSBuildProjectPath;

            IReadOnlyList<ExternalProjectReference> references = null;
            if (referenceContext.DirectReferenceCache.TryGetValue(metadata.ProjectPath, out references))
            {
                foreach (var reference in references)
                {
                    MSBuildRestoreUtility.AddMSBuildProjectReference(
                        packageSpec,
                        new ProjectRestoreReference
                        {
                            ProjectUniqueName = reference.UniqueName,
                            ProjectPath = reference.MSBuildProjectPath
                        },
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                reference.UniqueName,
                                LibraryDependencyTarget.ExternalProject)
                        });
                }
            }


            return packageSpec;
        }

        /// <summary>
        /// Project name
        /// </summary>
        public virtual string ProjectName
        {
            get
            {
                return _projectName;
            }
        }

        /// <summary>
        /// The underlying msbuild project system
        /// </summary>
        public IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; }

        public DateTimeOffset LastModified
        {
            get
            {
                var output = DateTimeOffset.MinValue;

                if (File.Exists(JsonConfigPath))
                {
                    output = File.GetLastWriteTimeUtc(JsonConfigPath);
                }

                return output;
            }
        }

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

        private async Task SaveJsonAsync(JObject json)
        {
            using (var writer = new StreamWriter(_jsonConfig.FullName, false, Encoding.UTF8))
            {
                await writer.WriteAsync(json.ToString());
            }
        }
    }
}
