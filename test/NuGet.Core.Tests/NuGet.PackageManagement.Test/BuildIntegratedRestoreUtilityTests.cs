// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v2;
using NuGet.Protocol.Core.v3;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class BuildIntegratedRestoreUtilityTests
    {
        [Fact]
        public async Task BuildIntegratedRestoreUtility_RestoreProjectNameProjectJson()
        {
            // Arrange
            var projectName = "testproj";

            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "testproj.project.json"));

            CreateConfigJson(projectConfig.FullName);

            var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

            var projectTargetFramework = NuGetFramework.Parse("uap10.0");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                new TestNuGetProjectContext());
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var effectiveGlobalPackagesFolder =
                BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                    null,
                    Configuration.NullSettings.Instance);

            // Act
            var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                Logging.NullLogger.Instance,
                sources,
                effectiveGlobalPackagesFolder,
                CancellationToken.None);

            // Assert
            Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "testproj.project.lock.json")));
            Assert.True(result.Success);
            Assert.False(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredDependencyChanged()
        {
            // Arrange
            var projectName = "testproj";

            var packagesFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            CreateConfigJson(projectConfig.FullName);

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
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                project,
                Logging.NullLogger.Instance,
                sources,
                packagesFolder,
                CancellationToken.None);

            var projects = new List<BuildIntegratedNuGetProject>() { project };

            var resolver = new VersionFolderPathResolver(packagesFolder);

            JsonConfigUtility.AddDependency(json, new NuGet.Packaging.Core.PackageDependency("nuget.core", VersionRange.Parse("2.8.3")));

            using (var writer = new StreamWriter(projectConfig.FullName))
            {
                writer.Write(json.ToString());
            }

            // Act
            var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, resolver);

            // Assert
            Assert.True(b);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder, packagesFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredChangedSha512()
        {
            // Arrange
            var projectName = "testproj";

            var packagesFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            CreateConfigJson(projectConfig.FullName);

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
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                project,
                Logging.NullLogger.Instance,
                sources,
                packagesFolder,
                CancellationToken.None);

            var projects = new List<BuildIntegratedNuGetProject>() { project };

            var resolver = new VersionFolderPathResolver(packagesFolder);

            var hashPath = resolver.GetHashPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));

            using (var writer = new StreamWriter(hashPath))
            {
                writer.Write("ANAWESOMELYWRONGHASH!!!");
            }

            // Act
            var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, resolver);

            // Assert
            Assert.True(b);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder, packagesFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredMissingPackage()
        {
            // Arrange
            var projectName = "testproj";

            var packagesFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            CreateConfigJson(projectConfig.FullName);

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
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var result = await BuildIntegratedRestoreUtility.RestoreAsync(
                project,
                Logging.NullLogger.Instance,
                sources,
                packagesFolder,
                CancellationToken.None);

            var projects = new List<BuildIntegratedNuGetProject>() { project };

            var resolver = new VersionFolderPathResolver(packagesFolder);

            // Act
            try
            {
                Directory.Delete(resolver.GetInstallPath("nuget.versioning", NuGetVersion.Parse("1.0.7")), true);
            }
            catch
            {
                // Ignore failures a file is open
            }

            var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, resolver);

            // Assert
            Assert.True(b);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder, packagesFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredWithFloatingVersion()
        {
            // Arrange
            var projectName = "testproj";

            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            CreateConfigJson(projectConfig.FullName);

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
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var effectiveGlobalPackagesFolder =
                BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                    null,
                    Configuration.NullSettings.Instance);

            var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                Logging.NullLogger.Instance,
                sources,
                effectiveGlobalPackagesFolder,
                CancellationToken.None);

            var projects = new List<BuildIntegratedNuGetProject>() { project };

            var resolver = new VersionFolderPathResolver(effectiveGlobalPackagesFolder);

            // Act
            var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, resolver);

            // Assert
            Assert.True(b);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequiredWithNoChanges()
        {
            // Arrange
            var projectName = "testproj";

            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            CreateConfigJson(projectConfig.FullName);

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
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var effectiveGlobalPackagesFolder =
                BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                    null,
                    Configuration.NullSettings.Instance);

            var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                Logging.NullLogger.Instance,
                sources,
                effectiveGlobalPackagesFolder,
                CancellationToken.None);

            var projects = new List<BuildIntegratedNuGetProject>() { project };

            var resolver = new VersionFolderPathResolver(effectiveGlobalPackagesFolder);

            // Act
            var b = BuildIntegratedRestoreUtility.IsRestoreRequired(projects, resolver);

            // Assert
            Assert.False(b);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_CacheDiffersOnClosure()
        {
            // Arrange
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var randomProjectFolderPath2 = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
            CreateConfigJson(randomConfig);
            CreateConfigJson(randomConfig2);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();

            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath,
                "project1");

            var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
            project1.ProjectClosure = new List<BuildIntegratedProjectReference>()
            {
                new BuildIntegratedProjectReference("a", "a/project.json", new string[] { "b/project.json" }),
                new BuildIntegratedProjectReference("b", "b/project.json", new string[] { }),
            };

            var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath2,
                "project2");

            var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
            project2.ProjectClosure = new List<BuildIntegratedProjectReference>() { };

            var projects = new List<BuildIntegratedNuGetProject>();
            projects.Add(project1);
            projects.Add(project2);

            var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);

            var projects2 = new List<BuildIntegratedNuGetProject>();
            project1.ProjectClosure = new List<BuildIntegratedProjectReference>() {
                new BuildIntegratedProjectReference("a", "a/project.json", new string[] { "d/project.json" }),
                new BuildIntegratedProjectReference("d", "d/project.json", new string[] { }),
            };

            projects2.Add(project1);
            projects2.Add(project2);
            var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects2);

            // Act
            var b = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

            // Assert
            Assert.True(b);

            // Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(randomProjectFolderPath, randomProjectFolderPath2);
        }

        [Fact]
        public async Task CacheHasChanges_ReturnsTrue_IfSupportProfilesDiffer()
        {
            // Arrange
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var randomProjectFolderPath2 = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
            var supports = new JObject
            {
                ["uap.app"] = new JObject()
            };
            var configJson = new JObject
            {
                ["frameworks"] = new JObject
                {
                    ["uap10.0"] = new JObject()
                },
                ["supports"] = supports
            };

            File.WriteAllText(randomConfig, configJson.ToString());
            File.WriteAllText(randomConfig2, configJson.ToString());

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();

            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath,
                "project1");

            var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
            project1.ProjectClosure = new List<BuildIntegratedProjectReference>()
            {
                new BuildIntegratedProjectReference("a", "a/project.json", new string[] { "b/project.json" }),
                new BuildIntegratedProjectReference("b", "b/project.json", new string[] { }),
            };

            var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath2,
                "project2");

            var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
            project2.ProjectClosure = new List<BuildIntegratedProjectReference>() { };

            var projects = new List<BuildIntegratedNuGetProject>();
            projects.Add(project1);
            projects.Add(project2);

            var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);
            supports["net46"] = new JObject();
            File.WriteAllText(randomConfig, configJson.ToString());
            var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);

            // Act 1
            var result1 = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

            // Assert 1
            Assert.True(result1);

            // Act 2
            var cache3 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);
            var result2 = BuildIntegratedRestoreUtility.CacheHasChanges(cache2, cache3);

            // Assert 2
            Assert.False(result2);

            // Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(randomProjectFolderPath, randomProjectFolderPath2);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_CacheDiffersOnProjects()
        {
            // Arrange
            // Arrange
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var randomProjectFolderPath2 = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
            CreateConfigJson(randomConfig);
            CreateConfigJson(randomConfig2);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();

            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath,
                "project1");

            var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
            project1.ProjectClosure = new List<BuildIntegratedProjectReference>()
            {
                new BuildIntegratedProjectReference("a", "a/project.json", new string[] { "b/project.json" }),
                new BuildIntegratedProjectReference("b", "b/project.json", new string[] { }),
            };

            var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath2,
                "project2");

            var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
            project2.ProjectClosure = new List<BuildIntegratedProjectReference>() { };

            var projects = new List<BuildIntegratedNuGetProject>();
            projects.Add(project1);

            var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);

            // Add a new project to the second cache
            projects.Add(project2);
            var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);

            // Act
            var b = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

            // Assert
            Assert.True(b);

            // Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(randomProjectFolderPath, randomProjectFolderPath2);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_SameCache()
        {
            // Arrange
            // Arrange
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var randomProjectFolderPath2 = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
            CreateConfigJson(randomConfig);
            CreateConfigJson(randomConfig2);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();

            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath,
                "project1");

            var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
            project1.ProjectClosure = new List<BuildIntegratedProjectReference>()
            {
                new BuildIntegratedProjectReference("a", "a/project.json", new string[] { "b/project.json" }),
                new BuildIntegratedProjectReference("b", "b/project.json", new string[] { }),
            };

            var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath2,
                "project2");

            var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
            project2.ProjectClosure = new List<BuildIntegratedProjectReference>() { };

            var projects = new List<BuildIntegratedNuGetProject>();
            projects.Add(project1);
            projects.Add(project2);

            var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);
            var cache2 = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);

            // Act
            var b = BuildIntegratedRestoreUtility.CacheHasChanges(cache, cache2);

            // Assert
            Assert.False(b);

            // Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(randomProjectFolderPath, randomProjectFolderPath2);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_CreateCache()
        {
            // Arrange
            // Arrange
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var randomProjectFolderPath2 = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig2 = Path.Combine(randomProjectFolderPath2, "project.json");
            CreateConfigJson(randomConfig);
            CreateConfigJson(randomConfig2);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();

            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath,
                "project1");

            var project1 = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);
            project1.ProjectClosure = new List<BuildIntegratedProjectReference>()
            {
                new BuildIntegratedProjectReference("a", "a/project.json", new string[] { "b/project.json" }),
                new BuildIntegratedProjectReference("b", "b/project.json", new string[] { }),
            };

            var msBuildNuGetProjectSystem2 = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath2,
                "project2");

            var project2 = new TestBuildIntegratedNuGetProject(randomConfig2, msBuildNuGetProjectSystem2);
            project2.ProjectClosure = new List<BuildIntegratedProjectReference>() { };

            var projects = new List<BuildIntegratedNuGetProject>();
            projects.Add(project1);
            projects.Add(project2);

            // Act
            var cache = await BuildIntegratedRestoreUtility.CreateBuildIntegratedProjectStateCache(projects);

            // Assert
            Assert.Equal(2, cache.Count);
            Assert.Equal(2, cache["project1"].PackageSpecClosure.Count);
            Assert.Equal(0, cache["project2"].PackageSpecClosure.Count);
            Assert.Equal("a/project.json|b/project.json", string.Join("|", cache["project1"].PackageSpecClosure));

            // Clean up
            TestFilesystemUtility.DeleteRandomTestFolders(randomProjectFolderPath, randomProjectFolderPath2);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_BasicRestoreTest()
        {
            // Arrange
            var projectName = "testproj";

            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            CreateConfigJson(projectConfig.FullName);

            var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

            var projectTargetFramework = NuGetFramework.Parse("uap10.0");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                new TestNuGetProjectContext());
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var effectiveGlobalPackagesFolder =
                BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                    null,
                    Configuration.NullSettings.Instance);

            // Act
            var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                Logging.NullLogger.Instance,
                sources,
                effectiveGlobalPackagesFolder,
                CancellationToken.None);

            // Assert
            Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
            Assert.True(result.Success);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_StayLocked()
        {
            // Arrange
            var projectName = "testproj";

            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
            CreateConfigJson(projectConfig.FullName);

            var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

            var projectTargetFramework = NuGetFramework.Parse("uap10.0");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                new TestNuGetProjectContext());
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var effectiveGlobalPackagesFolder =
                BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                    null,
                    Configuration.NullSettings.Instance);

            var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                Logging.NullLogger.Instance,
                sources,
                effectiveGlobalPackagesFolder,
                CancellationToken.None);

            var format = new LockFileFormat();

            var path = Path.Combine(projectFolder.FullName, "project.lock.json");

            // Set the lock file to locked=true
            var lockFile = result.LockFile;
            lockFile.IsLocked = true;
            format.Write(path, lockFile);

            // Act
            result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                Logging.NullLogger.Instance,
                sources,
                effectiveGlobalPackagesFolder,
                CancellationToken.None);

            // Assert
            Assert.True(result.LockFile.IsLocked);
            Assert.True(result.Success);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder);
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_RestoreToRelativePathGlobalPackagesFolder()
        {
            // Arrange
            var projectName = "testproj";

            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            File.WriteAllText(projectConfig.FullName, ProjectJsonWithPackage);

            var sources = new List<SourceRepository>
            {
                Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
            };

            var projectTargetFramework = NuGetFramework.Parse("uap10.0");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                new TestNuGetProjectContext());
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<config>
<add key=""globalPackagesFolder"" value=""..\NuGetPackages"" />
</config>
</configuration>";

            var configFolder = TestFilesystemUtility.CreateRandomTestFolder();
            File.WriteAllText(Path.Combine(configFolder, "nuget.config"), configContents);

            var settings = new Configuration.Settings(configFolder);

            var solutionFolderParent = TestFilesystemUtility.CreateRandomTestFolder();
            var solutionFolder = new DirectoryInfo(Path.Combine(solutionFolderParent, "solutionFolder"));
            solutionFolder.Create();

            var effectiveGlobalPackagesFolder =
                BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                    solutionFolder.FullName,
                    settings);

            // Act
            var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                Logging.NullLogger.Instance,
                sources,
                effectiveGlobalPackagesFolder,
                CancellationToken.None);

            // Assert
            Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));

            var packagesFolder = Path.Combine(solutionFolderParent, "NuGetPackages");

            Assert.True(Directory.Exists(packagesFolder));
            Assert.True(File.Exists(Path.Combine(
                packagesFolder,
                "EntityFramework",
                "5.0.0",
                "EntityFramework.5.0.0.nupkg")));

            Assert.True(result.Success);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder, configFolder, solutionFolderParent);
        }

        private static void CreateConfigJson(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfig.ToString());
            }
        }

        private static JObject BasicConfig
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["uap10.0"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }

        private const string ProjectJsonWithPackage = @"{
  'dependencies': {
    'EntityFramework': '5.0.0'
  },
  'frameworks': {
                'netcore50': { }
            }
}";


        private class TestBuildIntegratedNuGetProject : BuildIntegratedNuGetProject
        {
            public IReadOnlyList<BuildIntegratedProjectReference> ProjectClosure { get; set; }

            public TestBuildIntegratedNuGetProject(string jsonConfig, IMSBuildNuGetProjectSystem msbuildProjectSystem)
                : base(jsonConfig, msbuildProjectSystem)
            {
                InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, msbuildProjectSystem.ProjectName);
            }

            public override Task<IReadOnlyList<BuildIntegratedProjectReference>> GetProjectReferenceClosureAsync(NuGet.Logging.ILogger logger)
            {
                return Task.FromResult(ProjectClosure);
            }
        }
    }
}
