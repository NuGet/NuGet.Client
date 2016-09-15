// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.Test
{
    public class BuildIntegratedNuGetProjectTests
    {
        [Fact]
        public async Task BuildIntegratedNuGetProject_IsRestoreRequiredDependencyChanged()
        {
            // Arrange
            var projectName = "testproj";

            using (var packagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                    project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                JsonConfigUtility.AddDependency(json, new PackageDependency("nuget.core", VersionRange.Parse("2.8.3")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var actual = project.IsRestoreRequired(
                    new[] { new VersionFolderPathResolver(packagesFolder) },
                    new HashSet<PackageIdentity>(),
                    context);

                // Assert
                Assert.True(actual);
            }
        }

        [Fact]
        public async Task BuildIntegratedNuGetProject_IsRestoreRequiredChangedSha512()
        {
            // Arrange
            var projectName = "testproj";

            using (var packagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                    project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var resolver = new VersionFolderPathResolver(packagesFolder);
                var hashPath = resolver.GetHashPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));

                using (var writer = new StreamWriter(hashPath))
                {
                    writer.Write("ANAWESOMELYWRONGHASH!!!");
                }

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var actual = project.IsRestoreRequired(
                    new[] { resolver },
                    new HashSet<PackageIdentity>(),
                    context);

                // Assert
                Assert.True(actual);
            }
        }

        [Fact]
        public async Task BuildIntegratedNuGetProject_IsRestoreRequiredMissingPackage()
        {
            // Arrange
            var projectName = "testproj";

            using (var packagesFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                    project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    packagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var resolver = new VersionFolderPathResolver(packagesFolder);
                var pathToDelete = resolver.GetInstallPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                TestFileSystemUtility.DeleteRandomTestFolder(pathToDelete);

                // Act
                var actual = project.IsRestoreRequired(
                    new[] { resolver },
                    new HashSet<PackageIdentity>(),
                    context);

                // Assert
                Assert.True(actual);
            }
        }

        [Fact]
        public async Task BuildIntegratedNuGetProject_IsRestoreNotRequiredWithFloatingVersion()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                json.Add("dependencies", JObject.Parse("{ \"nuget.versioning\": \"1.0.*\" }"));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var actual = project.IsRestoreRequired(
                    new[] { new VersionFolderPathResolver(effectiveGlobalPackagesFolder) },
                    new HashSet<PackageIdentity>(),
                    context);

                // Assert
                Assert.False(actual);
            }
        }

        [Fact]
        public async Task BuildIntegratedNuGetProject_IsRestoreRequiredWithNoChanges()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var actual = project.IsRestoreRequired(
                    new[] { new VersionFolderPathResolver(effectiveGlobalPackagesFolder) },
                    new HashSet<PackageIdentity>(),
                    context);

                // Assert
                Assert.False(actual);
            }
        }

        [Fact]
        public async Task BuildIntegratedNuGetProject_IsRestoreRequiredWithNoChangesFallbackFolder()
        {
            // Arrange
            var projectName = "testproj";

            using (var globalFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                // Restore to the fallback folder
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    fallbackFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var packageFolders = new List<string> { globalFolder, fallbackFolder };

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var actual = project.IsRestoreRequired(
                    packageFolders.Select(p => new VersionFolderPathResolver(p)),
                    new HashSet<PackageIdentity>(),
                    context);

                // Assert
                Assert.False(actual);
            }
        }

        [Fact]
        public async Task BuildIntegratedNuGetProject_IsRestoreRequiredWithNoChangesFallbackFolderIgnoresOtherHashes()
        {
            // Arrange
            var projectName = "testproj";

            using (var globalFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var fallbackFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var json = JObject.Parse(File.ReadAllText(projectConfig.FullName));

                JsonConfigUtility.AddDependency(json, new PackageDependency("nuget.versioning", VersionRange.Parse("1.0.7")));

                using (var writer = new StreamWriter(projectConfig.FullName))
                {
                    writer.Write(json.ToString());
                }

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                // Restore to the fallback folder
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    fallbackFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Restore to global folder
                result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    globalFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                var resolver = new VersionFolderPathResolver(fallbackFolder);
                var hashPath = resolver.GetHashPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));
                File.WriteAllText(hashPath, "AA00F==");

                var packageFolders = new List<string>() { globalFolder, fallbackFolder };

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();

                // Act
                var actual = project.IsRestoreRequired(
                    packageFolders.Select(p => new VersionFolderPathResolver(p)),
                    new HashSet<PackageIdentity>(),
                    context);

                // Assert
                Assert.False(actual);
            }
        }
    }
}
