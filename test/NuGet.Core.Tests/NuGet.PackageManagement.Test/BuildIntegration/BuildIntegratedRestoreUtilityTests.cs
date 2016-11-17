// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackageManagement.Test;
using NuGet.ProjectManagement;
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

            using (var rootFolder = TestDirectory.Create())
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
                var project = new ProjectJsonBuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var solutionManager = new TestSolutionManager(false);
                solutionManager.NuGetProjects.Add(project);

                var testLogger = new TestLogger();

                var restoreContext = new DependencyGraphCacheContext(testLogger);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sources,
                    NullSettings.Instance,
                    testLogger,
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "testproj.project.lock.json")));
                Assert.True(testLogger.Errors == 0);
                Assert.False(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_BasicRestoreTest()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestDirectory.Create())
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
                var project = new ProjectJsonBuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

                var solutionManager = new TestSolutionManager(false);
                solutionManager.NuGetProjects.Add(project);

                var testLogger = new TestLogger();

                var restoreContext = new DependencyGraphCacheContext(testLogger);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sources,
                    NullSettings.Instance,
                    testLogger,
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
                Assert.True(testLogger.Errors == 0);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_RestoreToRelativePathGlobalPackagesFolder()
        {
            // Arrange
            var projectName = "testproj";

            using (var rootFolder = TestDirectory.Create())
            using (var configFolder = TestDirectory.Create())
            using (var solutionFolderParent = TestDirectory.Create())
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
                var project = new ProjectJsonBuildIntegratedNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName, msBuildNuGetProjectSystem);

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

                var solutionManager = new TestSolutionManager(false);
                solutionManager.NuGetProjects.Add(project);

                var testLogger = new TestLogger();

                var restoreContext = new DependencyGraphCacheContext(testLogger);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sources,
                    settings,
                    testLogger,
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

                Assert.True(testLogger.Errors == 0, testLogger.ShowErrors());
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_RestoreDeferredProjectLoad()
        {
            // Arrange
            var projectName = "testproj";

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                  ""dependencies"": {
                    ""packageA"": ""1.0.0""
                  }
                }
              }
            }";

            using (var packageSource = TestDirectory.Create())
            using (var rootFolder = TestDirectory.Create())
            {
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(
                    new List<PackageSource>()
                    {
                        new PackageSource(packageSource.Path)
                    });

                var packageContext = new SimpleTestPackageContext("packageA");
                packageContext.AddFile("lib/net45/a.dll");
                SimpleTestPackageUtility.CreateOPCPackage(packageContext, packageSource);

                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = Path.Combine(projectFolder.FullName, "project.json");
                var projectPath = Path.Combine(projectFolder.FullName, $"{projectName}.csproj");

                var packageSpec = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", projectConfig);
                var metadata = new ProjectRestoreMetadata();
                packageSpec.RestoreMetadata = metadata;

                metadata.OutputType = RestoreOutputType.UAP;
                metadata.ProjectPath = projectPath;
                metadata.ProjectJsonPath = packageSpec.FilePath;
                metadata.ProjectName = packageSpec.Name;
                metadata.ProjectUniqueName = projectPath;
                foreach (var framework in packageSpec.TargetFrameworks.Select(e => e.FrameworkName))
                {
                    metadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(framework));
                }

                var solutionManager = new TestSolutionManager(false);

                var testLogger = new TestLogger();

                var restoreContext = new DependencyGraphCacheContext(testLogger);
                restoreContext.DeferredPackageSpecs.Add(packageSpec);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sourceRepositoryProvider.GetRepositories(),
                    NullSettings.Instance,
                    testLogger,
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
                Assert.True(testLogger.Errors == 0);
            }
        }

        [Fact]
        public async Task BuildIntegratedRestoreUtility_IsRestoreRequired_DeferredProjectLoad()
        {
            // Arrange
            var projectName = "testproj";

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var rootFolder = TestDirectory.Create())
            {
                var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
                projectFolder.Create();
                var projectConfig = Path.Combine(projectFolder.FullName, "project.json");
                var projectPath = Path.Combine(projectFolder.FullName, $"{projectName}.csproj");

                var packageSpec = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", projectConfig);
                var metadata = new ProjectRestoreMetadata();
                packageSpec.RestoreMetadata = metadata;

                metadata.OutputType = RestoreOutputType.UAP;
                metadata.ProjectPath = projectPath;
                metadata.ProjectJsonPath = packageSpec.FilePath;
                metadata.ProjectName = packageSpec.Name;
                metadata.ProjectUniqueName = projectPath;

                foreach (var framework in packageSpec.TargetFrameworks.Select(e => e.FrameworkName))
                {
                    metadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(framework));
                }

                var solutionManager = new TestSolutionManager(false);

                var testLogger = new TestLogger();

                var restoreContext = new DependencyGraphCacheContext(testLogger);
                restoreContext.DeferredPackageSpecs.Add(packageSpec);

                var pathContext = NuGetPathContext.Create(NullSettings.Instance);

                var oldHash = restoreContext.SolutionSpecHash;

                // Act
                var result = await DependencyGraphRestoreUtility.IsRestoreRequiredAsync(
                    solutionManager,
                    forceRestore: false,
                    pathContext: pathContext,
                    cacheContext: restoreContext,
                    oldDependencyGraphSpecHash: oldHash);

                // Assert
                Assert.Equal(true, result);
                Assert.Equal(0, testLogger.Errors);
                Assert.Equal(0, testLogger.Warnings);
            }
        }
    }
}
