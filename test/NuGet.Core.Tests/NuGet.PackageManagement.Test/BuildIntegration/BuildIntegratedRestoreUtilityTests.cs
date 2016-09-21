// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.Test;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Test.Utility;
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

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "testproj.project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                BuildIntegrationTestUtility.CreateConfigJson(projectConfig.FullName);

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                    new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                // Act
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "testproj.project.lock.json")));
                Assert.True(result.Success);
                Assert.False(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_BasicRestoreTest()
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

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                    new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                // Act
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext(),
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_RestoreToRelativePathGlobalPackagesFolder()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var configFolder = TestFileSystemUtility.CreateRandomTestFolder())
            using (var solutionFolderParent = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));
                var msbuildProjectPath = new FileInfo(Path.Combine(projectFolder.FullName, $"{projectName}.csproj"));

                File.WriteAllText(projectConfig.FullName, BuildIntegrationTestUtility.ProjectJsonWithPackage);

                var sources = new List<SourceRepository>
                {
                    Repository.Factory.GetVisualStudio("https://www.nuget.org/api/v2/")
                };

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                    new TestNuGetProjectContext());
                var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<config>
<add key=""globalPackagesFolder"" value=""..\NuGetPackages"" />
</config>
</configuration>";

                var configSubFolder = Path.Combine(configFolder, "sub");
                Directory.CreateDirectory(configSubFolder);

                File.WriteAllText(Path.Combine(configFolder, "sub", "nuget.config"), configContents);

                var settings = new Configuration.Settings(configSubFolder);

                var solutionFolder = new DirectoryInfo(Path.Combine(solutionFolderParent, "solutionFolder"));
                solutionFolder.Create();

                var effectiveGlobalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);

                var context = BuildIntegrationTestUtility.GetExternalProjectReferenceContext();
                var logger = (TestLogger)context.Logger;

                // Act
                var result = await BuildIntegratedRestoreUtility.RestoreAsync(project,
                    context,
                    sources,
                    effectiveGlobalPackagesFolder,
                    Enumerable.Empty<string>(),
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));

                var packagesFolder = Path.Combine(configFolder, "NuGetPackages");

                Assert.True(Directory.Exists(packagesFolder));
                Assert.True(File.Exists(Path.Combine(
                    packagesFolder,
                    "EntityFramework",
                    "5.0.0",
                    "EntityFramework.5.0.0.nupkg")));

                Assert.True(result.Success, logger.ShowErrors());
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_GetChildProjectsInClosure_MultipleChildHierarchy()
        {           
            // Arrange
            using (var randomProjectFolderPathProject1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathA = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathB = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathC = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectConfig = new FileInfo(Path.Combine(randomProjectFolderPathProject1, "project.json"));
                var projectConfigA = new FileInfo(Path.Combine(randomProjectFolderPathA, "project.json"));
                var projectConfigB = new FileInfo(Path.Combine(randomProjectFolderPathB, "project.json"));
                var projectConfigC = new FileInfo(Path.Combine(randomProjectFolderPathC, "project.json"));
                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathProject1,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference(projectConfigA.FullName, "a/project.json", new string[] { }),
                    BuildIntegrationTestUtility.CreateReference(projectConfigB.FullName, "b/project.json", new string[] { }),
                    BuildIntegrationTestUtility.CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                // Project A
                var msBuildNuGetProjectSystemA = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathA,
                    "a");

                var projectA = new TestBuildIntegratedNuGetProject(projectConfigA.FullName, msBuildNuGetProjectSystemA);
                projectA.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference(projectConfigB.FullName, "b/project.json", new string[] { }),
                    BuildIntegrationTestUtility.CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                // Project B
                var msBuildNuGetProjectSystemB = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathB,
                    "b");

                var projectB = new TestBuildIntegratedNuGetProject(projectConfigB.FullName, msBuildNuGetProjectSystemB);
                projectB.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystemC = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathC,
                    "c");

                var projectC = new TestBuildIntegratedNuGetProject(projectConfigC.FullName, msBuildNuGetProjectSystemC);
                projectC.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(projectA);
                projects.Add(projectB);
                projects.Add(projectC);

                var orderedChilds = new List<BuildIntegratedNuGetProject>();
                var uniqueProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                uniqueProjects.Add(project1.ProjectName);

                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Act
                BuildIntegratedRestoreUtility.GetChildProjectsInClosure(project1, projects, orderedChilds,
                        uniqueProjects, cache);

                // Assert
                Assert.Equal(4, orderedChilds.Count);
                Assert.Equal(projectC.ProjectName, orderedChilds[0].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(projectB.ProjectName, orderedChilds[1].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(projectA.ProjectName, orderedChilds[2].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(project1.ProjectName, orderedChilds[3].ProjectName, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_GetChildProjectsInClosure_SingleChild()
        {
            // Arrange
            using (var randomProjectFolderPathProject1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathA = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathB = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathC = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectConfig = new FileInfo(Path.Combine(randomProjectFolderPathProject1, "project.json"));
                var projectConfigA = new FileInfo(Path.Combine(randomProjectFolderPathA, "project.json"));
                var projectConfigB = new FileInfo(Path.Combine(randomProjectFolderPathB, "project.json"));
                var projectConfigC = new FileInfo(Path.Combine(randomProjectFolderPathC, "project.json"));
                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathProject1,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference(projectConfigA.FullName, "a/project.json", new string[] { }),
                };

                // Project A
                var msBuildNuGetProjectSystemA = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathA,
                    "a");

                var projectA = new TestBuildIntegratedNuGetProject(projectConfigA.FullName, msBuildNuGetProjectSystemA);
                projectA.ProjectClosure = new List<ExternalProjectReference>() {};

                // Project B
                var msBuildNuGetProjectSystemB = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathB,
                    "b");

                var projectB = new TestBuildIntegratedNuGetProject(projectConfigB.FullName, msBuildNuGetProjectSystemB);
                projectB.ProjectClosure = new List<ExternalProjectReference>()
                {
                    BuildIntegrationTestUtility.CreateReference(projectConfigC.FullName, "c/project.json", new string[] { }),
                };

                var msBuildNuGetProjectSystemC = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathC,
                    "c");

                var projectC = new TestBuildIntegratedNuGetProject(projectConfigC.FullName, msBuildNuGetProjectSystemC);
                projectC.ProjectClosure = new List<ExternalProjectReference>() { };

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(projectA);
                projects.Add(projectB);
                projects.Add(projectC);

                var orderedChilds = new List<BuildIntegratedNuGetProject>();
                var uniqueProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                uniqueProjects.Add(project1.ProjectName);

                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Act
                BuildIntegratedRestoreUtility.GetChildProjectsInClosure(project1, projects, orderedChilds,
                        uniqueProjects, cache);

                // Assert
                Assert.Equal(2, orderedChilds.Count);
                Assert.Equal(projectA.ProjectName, orderedChilds[0].ProjectName, StringComparer.OrdinalIgnoreCase);
                Assert.Equal(project1.ProjectName, orderedChilds[1].ProjectName, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_GetChildProjectsInClosure_NoChild()
        {
            // Arrange
            using (var randomProjectFolderPathProject1 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathA = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPathB = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var projectConfig = new FileInfo(Path.Combine(randomProjectFolderPathProject1, "project.json"));
                var projectConfigA = new FileInfo(Path.Combine(randomProjectFolderPathA, "project.json"));
                var projectConfigB = new FileInfo(Path.Combine(randomProjectFolderPathB, "project.json"));

                var projectTargetFramework = NuGetFramework.Parse("uap10.0");
                var testNuGetProjectContext = new TestNuGetProjectContext();

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathProject1,
                    "project1");

                var project1 = new TestBuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);
                project1.ProjectClosure = new List<ExternalProjectReference>() {};

                // Project A
                var msBuildNuGetProjectSystemA = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathA,
                    "a");

                var projectA = new TestBuildIntegratedNuGetProject(projectConfigA.FullName, msBuildNuGetProjectSystemA);
                projectA.ProjectClosure = new List<ExternalProjectReference>() { };

                // Project B
                var msBuildNuGetProjectSystemB = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPathB,
                    "b");

                var projectB = new TestBuildIntegratedNuGetProject(projectConfigB.FullName, msBuildNuGetProjectSystemB);
                projectB.ProjectClosure = new List<ExternalProjectReference>() {};

                var projects = new List<BuildIntegratedNuGetProject>();
                projects.Add(project1);
                projects.Add(projectA);
                projects.Add(projectB);

                var orderedChilds = new List<BuildIntegratedNuGetProject>();
                var uniqueProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                uniqueProjects.Add(project1.ProjectName);

                var cache = await DependencyGraphProjectCacheUtility.CreateDependencyGraphProjectCache(
                    projects,
                    BuildIntegrationTestUtility.GetExternalProjectReferenceContext());

                // Act
                BuildIntegratedRestoreUtility.GetChildProjectsInClosure(project1, projects, orderedChilds,
                        uniqueProjects, cache);

                // Assert
                Assert.Equal(1, orderedChilds.Count);
                Assert.Equal(project1.ProjectName, orderedChilds[0].ProjectName, StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
