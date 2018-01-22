// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                var project = new ProjectJsonNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName);

                var solutionManager = new TestSolutionManager(false);
                solutionManager.NuGetProjects.Add(project);

                var testLogger = new TestLogger();

                var restoreContext = new DependencyGraphCacheContext(testLogger, NullSettings.Instance);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sources,
                    Guid.Empty,
                    false,
                    await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(solutionManager, restoreContext),
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
                var project = new ProjectJsonNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName);

                var solutionManager = new TestSolutionManager(false);
                solutionManager.NuGetProjects.Add(project);

                var testLogger = new TestLogger();

                var restoreContext = new DependencyGraphCacheContext(testLogger, NullSettings.Instance);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sources,
                    Guid.Empty,
                    false,
                    await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(solutionManager, restoreContext),
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
                var project = new ProjectJsonNuGetProject(projectConfig.FullName, msbuildProjectPath.FullName);

                var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                    <config>
                    <add key=""globalPackagesFolder"" value=""..\NuGetPackages"" />
                    </config>
                    <packageSources>
                        <add key=""nuget.org.v2"" value=""https://www.nuget.org/api/v2/"" />
                    </packageSources>
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

                var restoreContext = new DependencyGraphCacheContext(testLogger, settings);

                // Act
                await DependencyGraphRestoreUtility.RestoreAsync(
                    solutionManager,
                    restoreContext,
                    new RestoreCommandProvidersCache(),
                    (c) => { },
                    sources,
                    Guid.Empty,
                    false,
                    await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(solutionManager, restoreContext),
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
    }
}
