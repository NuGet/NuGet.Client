// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectManagement.Projects
{
    /// <summary>
    /// A NuGet integrated MSBuild project.
    /// These projects contain a nuget.json
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

        public override async Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, Stream packageStream, INuGetProjectContext nuGetProjectContext, CancellationToken token)
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
        /// Add non-build time items such as content, install.ps1, and targets.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801")]
        public async Task<bool> InstallPackageContentAsync(PackageIdentity identity, Stream packageStream, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            var changesMade = false;

            var zipArchive = new ZipArchive(packageStream);

            var reader = new PackageReader(zipArchive);

            var projectFramework = GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);

            var compatibleContentFilesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(projectFramework, reader.GetContentItems());
            var compatibleBuildFileGroups =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(projectFramework, reader.GetBuildItems());

            var toolItemGroups = reader.GetToolItems();

            var compatibleToolItemGroups =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(projectFramework, toolItemGroups);

            // Step-8.3: Add Content Files
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup))
            {
                changesMade = true;

                MSBuildNuGetProjectSystemUtility.AddFiles(MSBuildNuGetProjectSystem,
                    zipArchive, compatibleContentFilesGroup, FileTransformers);
            }

            // Step-8.4: Add Build imports
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleBuildFileGroups))
            {
                foreach (var buildImportFile in compatibleBuildFileGroups.Items)
                {
                    var fullImportFilePath = Path.Combine(Path.GetDirectoryName(BuildIntegratedProjectUtility.GetNupkgPathFromGlobalSource(identity)), buildImportFile);
                    MSBuildNuGetProjectSystem.AddImport(fullImportFilePath,
                        fullImportFilePath.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ? ImportLocation.Top : ImportLocation.Bottom);
                }
            }

            // Step-12: Execute powershell script - install.ps1
            var packageInstallPath = Path.GetDirectoryName(BuildIntegratedProjectUtility.GetNupkgPathFromGlobalSource(identity));
            var anyFrameworkToolsGroup = toolItemGroups.Where(g => g.TargetFramework.Equals(NuGetFramework.AnyFramework)).FirstOrDefault();
            if (anyFrameworkToolsGroup != null)
            {
                var initPS1RelativePath = anyFrameworkToolsGroup.Items.Where(p =>
                    p.StartsWith(PowerShellScripts.InitPS1RelativePath, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (!string.IsNullOrEmpty(initPS1RelativePath))
                {
                    initPS1RelativePath = PathUtility.ReplaceAltDirSeparatorWithDirSeparator(initPS1RelativePath);
                    await MSBuildNuGetProjectSystem.ExecuteScriptAsync(packageInstallPath, initPS1RelativePath, zipArchive, this, throwOnFailure: true);
                }
            }

            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleToolItemGroups))
            {
                var installPS1RelativePath = compatibleToolItemGroups.Items.Where(p =>
                    p.EndsWith(Path.DirectorySeparatorChar + PowerShellScripts.Install, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (!string.IsNullOrEmpty(installPS1RelativePath))
                {
                    await MSBuildNuGetProjectSystem.ExecuteScriptAsync(packageInstallPath, installPS1RelativePath, zipArchive, this, throwOnFailure: true);
                }
            }

            return await Task.FromResult(changesMade);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public Task<bool> UninstallPackageContentAsync(Stream packageStream)
        {
            var changesMade = false;

            var zipArchive = new ZipArchive(packageStream);

            var reader = new PackageReader(zipArchive);

            var projectFramework = GetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework);

            var compatibleContentFilesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(projectFramework, reader.GetContentItems());

            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup))
            {
                changesMade = true;

                MSBuildNuGetProjectSystemUtility.DeleteFiles(MSBuildNuGetProjectSystem,
                    zipArchive,
                    Enumerable.Empty<string>(),
                    compatibleContentFilesGroup,
                    FileTransformers);
            }

            return Task.FromResult(changesMade);
        }

        /// <summary>
        /// nuget.json path
        /// </summary>
        public virtual string JsonConfigPath
        {
            get { return _jsonConfig.FullName; }
        }

        /// <summary>
        /// Project name
        /// </summary>
        public virtual string ProjectName
        {
            get
            {
                var file = new FileInfo(JsonConfigPath);
                return file.Directory.Name;
            }
        }

        /// <summary>
        /// The underlying msbuild project system
        /// </summary>
        public IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; }

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

        private readonly IDictionary<FileTransformExtensions, IPackageFileTransformer> FileTransformers =
            new Dictionary<FileTransformExtensions, IPackageFileTransformer>
                {
                    { new FileTransformExtensions(".transform", ".transform"), new XmlTransformer(GetConfigMappings()) },
                    { new FileTransformExtensions(".pp", ".pp"), new Preprocessor() },
                    { new FileTransformExtensions(".install.xdt", ".uninstall.xdt"), new XdtTransformer() }
                };

        private static IDictionary<XName, Action<XElement, XElement>> GetConfigMappings()
        {
            // REVIEW: This might be an edge case, but we're setting this rule for all xml files.
            // If someone happens to do a transform where the xml file has a configSections node
            // we will add it first. This is probably fine, but this is a config specific scenario
            return new Dictionary<XName, Action<XElement, XElement>>
                {
                    { "configSections", (parent, element) => parent.AddFirst(element) }
                };
        }
    }
}
